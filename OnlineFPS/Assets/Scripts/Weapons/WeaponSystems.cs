using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine.Serialization;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using Random = Unity.Mathematics.Random;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class WeaponPredictionUpdateGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class WeaponVisualsUpdateGroup : ComponentSystemGroup
    {
    }

    [BurstCompile]
    [UpdateInGroup(typeof(WeaponPredictionUpdateGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponsSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameResources>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PhysicsWorldHistorySingleton>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            GameResources gameResources = SystemAPI.GetSingleton<GameResources>();
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            BaseWeaponSimulationJob baseWeaponSimulationJob = new BaseWeaponSimulationJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
            };
            state.Dependency = baseWeaponSimulationJob.ScheduleParallel(state.Dependency);

            if (networkTime.ServerTick.IsValid && networkTime.IsFirstTimeFullyPredictingTick)
            {
                RaycastWeaponSimulationJob raycastWeaponSimulationJob = new RaycastWeaponSimulationJob
                {
                    IsServer = state.WorldUnmanaged.IsServer(),
                    OldestAllowedVFXRequestsTick = math.max((uint)0,
                        networkTime.ServerTick.TickIndexForValidTick - gameResources.PolledEventsTicks),
                    NetworkTime = networkTime,
                    PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                    PhysicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
                    RaycastProjectileLookup = SystemAPI.GetComponentLookup<RaycastProjectile>(true),
                    HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
                };
                state.Dependency = raycastWeaponSimulationJob.Schedule(state.Dependency);

                PrefabWeaponSimulationJob prefabWeaponSimulationJob = new PrefabWeaponSimulationJob
                {
                    IsServer = state.WorldUnmanaged.IsServer(),
                    ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged),
                    PrefabProjectileLookup = SystemAPI.GetComponentLookup<PrefabProjectile>(true),
                };
                state.Dependency = prefabWeaponSimulationJob.Schedule(state.Dependency);
            }
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct BaseWeaponSimulationJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;

            void Execute(
                ref BaseWeapon baseWeapon,
                ref WeaponControl weaponControl,
                ref DynamicBuffer<WeaponProjectileEvent> projectileEvents,
                in WeaponShotSimulationOriginOverride shotSimulationOriginOverride)
            {
                projectileEvents.Clear();

                uint prevTotalShotsCount = baseWeapon.TotalShotsCount;

                // Detect starting to fire
                if (weaponControl.ShootPressed)
                {
                    baseWeapon.IsFiring = true;
                }

                // Handle firing
                if (baseWeapon.FiringRate > 0f)
                {
                    float delayBetweenShots = 1f / baseWeapon.FiringRate;

                    // Clamp shot timer in order to shoot at most the maximum amount of shots that can be shot in one frame based on the firing rate.
                    // This also prevents needlessly dirtying the timer ghostfield (saves bandwidth).
                    float maxUsefulShotTimer = delayBetweenShots + DeltaTime;
                    if (baseWeapon.ShotTimer < maxUsefulShotTimer)
                    {
                        baseWeapon.ShotTimer += DeltaTime;
                    }

                    // This loop is done to allow firing rates that would trigger more than one shot per tick
                    while (baseWeapon.IsFiring && baseWeapon.ShotTimer > delayBetweenShots)
                    {
                        baseWeapon.TotalShotsCount++;

                        // Consume shoot time
                        baseWeapon.ShotTimer -= delayBetweenShots;

                        // Stop firing after initial shot for non-auto fire
                        if (!baseWeapon.Automatic)
                        {
                            baseWeapon.IsFiring = false;
                        }
                    }
                }

                // Detect stopping fire
                if (!baseWeapon.Automatic || weaponControl.ShootReleased)
                {
                    baseWeapon.IsFiring = false;
                }

                uint shotsToFire = baseWeapon.TotalShotsCount - prevTotalShotsCount;
                if (shotsToFire > 0)
                {
                    // Find the world transform of the shot start point
                    RigidTransform shotSimulationOrigin = WeaponUtilities.GetShotSimulationOrigin(
                        baseWeapon.ShotOrigin,
                        in shotSimulationOriginOverride,
                        ref LocalTransformLookup,
                        ref ParentLookup,
                        ref PostTransformMatrixLookup);
                    TransformHelpers.ComputeWorldTransformMatrix(baseWeapon.ShotOrigin, out float4x4 shotVisualsOrigin,
                        ref LocalTransformLookup, ref ParentLookup, ref PostTransformMatrixLookup);

                    for (int i = 0; i < shotsToFire; i++)
                    {
                        for (int j = 0; j < baseWeapon.ProjectilesPerShot; j++)
                        {
                            baseWeapon.TotalProjectilesCount++;

                            Random deterministicRandom = Random.CreateFromIndex(baseWeapon.TotalProjectilesCount);
                            quaternion shotRotationWithSpread =
                                WeaponUtilities.CalculateSpreadRotation(shotSimulationOrigin.rot,
                                    baseWeapon.SpreadRadians,
                                    ref deterministicRandom);

                            projectileEvents.Add(new WeaponProjectileEvent
                            {
                                ID = baseWeapon.TotalProjectilesCount,
                                SimulationPosition = shotSimulationOrigin.pos,
                                SimulationDirection = math.mul(shotRotationWithSpread, math.forward()),
                                VisualPosition = shotVisualsOrigin.Translation(),
                            });
                        }
                    }
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct RaycastWeaponSimulationJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public bool IsServer;
            public NetworkTime NetworkTime;
            public uint OldestAllowedVFXRequestsTick;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public PhysicsWorldHistorySingleton PhysicsWorldHistory;
            [ReadOnly] public ComponentLookup<RaycastProjectile> RaycastProjectileLookup;
            public ComponentLookup<Health> HealthLookup;

            [NativeDisableContainerSafetyRestriction]
            private NativeList<RaycastHit> Hits;

            void Execute(
                in RaycastWeapon raycastWeapon,
                in WeaponControl weaponControl,
                in DynamicBuffer<WeaponProjectileEvent> projectileEvents,
                ref DynamicBuffer<RaycastWeaponVisualProjectileEvent> visualProjectileEvents,
                in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
            {
                if (RaycastProjectileLookup.TryGetComponent(raycastWeapon.ProjectilePrefab,
                        out RaycastProjectile raycastProjectile))
                {
                    PhysicsWorldHistory.GetCollisionWorldFromTick(NetworkTime.ServerTick, 
                        weaponControl.InterpolationDelay, ref PhysicsWorld, out CollisionWorld collisionWorld);

                    for (int i = 0; i < projectileEvents.Length; i++)
                    {
                        WeaponProjectileEvent projectileEvent = projectileEvents[i];

                        WeaponUtilities.CalculateIndividualRaycastShot(
                            projectileEvent.SimulationPosition,
                            projectileEvent.SimulationDirection,
                            raycastProjectile.Range,
                            in collisionWorld,
                            ref Hits,
                            in ignoredEntities,
                            out bool hitFound,
                            out float hitDistance,
                            out float3 hitNormal,
                            out Entity hitEntity,
                            out float3 shotEndPoint);

                        // Visual events
                        if (raycastWeapon.VisualsSyncMode == RaycastWeaponVisualsSyncMode.Precise)
                        {
                            visualProjectileEvents.Add(new RaycastWeaponVisualProjectileEvent
                            {
                                Tick = NetworkTime.ServerTick.TickIndexForValidTick,
                                DidHit = hitFound ? (byte)1 : (byte)0,
                                EndPoint = shotEndPoint,
                                HitNormal = hitNormal,
                            });

                            // Clear VFX requests that are too old (if server or owner, since owners don't receive syncs from server for this buffer type)
                            for (int e = visualProjectileEvents.Length - 1; e >= 0; e--)
                            {
                                RaycastWeaponVisualProjectileEvent tmpRequest = visualProjectileEvents[e];
                                if (tmpRequest.Tick < OldestAllowedVFXRequestsTick)
                                {
                                    visualProjectileEvents.RemoveAtSwapBack(e);
                                }
                            }
                        }

                        // Apply Damage
                        if (IsServer && hitFound)
                        {
                            if (HealthLookup.TryGetComponent(hitEntity, out Health health))
                            {
                                health.CurrentHealth -= raycastProjectile.Damage;
                                HealthLookup[hitEntity] = health;
                            }
                        }
                    }
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Hits.IsCreated)
                {
                    Hits = new NativeList<RaycastHit>(128, Allocator.Temp);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct PrefabWeaponSimulationJob : IJobEntity
        {
            public bool IsServer;
            public EntityCommandBuffer ECB;
            [ReadOnly] public ComponentLookup<PrefabProjectile> PrefabProjectileLookup;

            void Execute(
                Entity entity,
                in PrefabWeapon prefabWeapon,
                in DynamicBuffer<WeaponProjectileEvent> projectileEvents,
                in LocalTransform localTransform,
                in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities,
                in GhostOwner ghostOwner)
            {
                if (PrefabProjectileLookup.TryGetComponent(prefabWeapon.ProjectilePrefab,
                        out PrefabProjectile prefabProjectile))
                {
                    for (int i = 0; i < projectileEvents.Length; i++)
                    {
                        WeaponProjectileEvent projectileEvent = projectileEvents[i];

                        // Projectile spawn
                        Entity spawnedProjectile = ECB.Instantiate(prefabWeapon.ProjectilePrefab);
                        ECB.SetComponent(spawnedProjectile, LocalTransform.FromPositionRotation(
                            projectileEvent.SimulationPosition,
                            quaternion.LookRotationSafe(projectileEvent.SimulationDirection,
                                math.mul(localTransform.Rotation, math.up()))));
                        ECB.SetComponent(spawnedProjectile,
                            new ProjectileSpawnId { WeaponEntity = entity, SpawnId = projectileEvent.ID });
                        for (int k = 0; k < ignoredEntities.Length; k++)
                        {
                            ECB.AppendToBuffer(spawnedProjectile, ignoredEntities[k]);
                        }

                        // Set projectile data
                        {
                            prefabProjectile.Velocity = prefabProjectile.Speed * projectileEvent.SimulationDirection;
                            prefabProjectile.VisualOffset =
                                projectileEvent.VisualPosition - projectileEvent.SimulationPosition;
                            ECB.SetComponent(spawnedProjectile, prefabProjectile);
                        }
                        // Set owner
                        if (IsServer)
                        {
                            ECB.SetComponent(spawnedProjectile, new GhostOwner { NetworkId = ghostOwner.NetworkId });
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(WeaponVisualsUpdateGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct InitializeWeaponLastShotVisualsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            InitializeWeaponLastShotVisualsJob initializeWeaponLastShotVisualsJob =
                new InitializeWeaponLastShotVisualsJob
                    { };
            state.Dependency = initializeWeaponLastShotVisualsJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct InitializeWeaponLastShotVisualsJob : IJobEntity
        {
            void Execute(ref BaseWeapon baseWeapon)
            {
                // This prevents false visual feedbacks when a ghost is re-spawned die to relevancy
                if (baseWeapon.LastVisualTotalShotsCountInitialized == 0)
                {
                    baseWeapon.LastVisualTotalShotsCount = baseWeapon.TotalShotsCount;
                    baseWeapon.LastVisualTotalShotsCountInitialized = 1;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(WeaponVisualsUpdateGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct FinalizeWeaponLastShotVisualsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            FinalizeWeaponLastShotVisualsJob finalizeWeaponLastShotVisualsJob = new FinalizeWeaponLastShotVisualsJob
                { };
            state.Dependency = finalizeWeaponLastShotVisualsJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct FinalizeWeaponLastShotVisualsJob : IJobEntity
        {
            void Execute(ref BaseWeapon baseWeapon)
            {
                baseWeapon.LastVisualTotalShotsCount = baseWeapon.TotalShotsCount;
            }
        }
    }


    [BurstCompile]
    [UpdateInGroup(typeof(WeaponVisualsUpdateGroup))]
    [UpdateBefore(typeof(CharacterWeaponVisualFeedbackSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct BaseWeaponShotVisualsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BaseWeaponShotVisualsJob baseWeaponShotVisualsJob = new BaseWeaponShotVisualsJob
            {
                CharacterWeaponVisualFeedbackLookup =
                    SystemAPI.GetComponentLookup<CharacterWeaponVisualFeedback>(false),
            };
            state.Dependency = baseWeaponShotVisualsJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct BaseWeaponShotVisualsJob : IJobEntity
        {
            public ComponentLookup<CharacterWeaponVisualFeedback> CharacterWeaponVisualFeedbackLookup;

            void Execute(
                ref WeaponVisualFeedback weaponFeedback,
                ref BaseWeapon baseWeapon,
                ref WeaponOwner weaponOwner)
            {
                // Recoil
                if (CharacterWeaponVisualFeedbackLookup.TryGetComponent(weaponOwner.Entity,
                        out CharacterWeaponVisualFeedback characterFeedback))
                {
                    for (uint i = baseWeapon.LastVisualTotalShotsCount; i < baseWeapon.TotalShotsCount; i++)
                    {
                        characterFeedback.CurrentRecoil += weaponFeedback.RecoilStrength;
                        characterFeedback.TargetRecoilFOVKick += weaponFeedback.RecoilFOVKick;
                    }

                    CharacterWeaponVisualFeedbackLookup[weaponOwner.Entity] = characterFeedback;
                }
            }
        }
    }
}