using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(WeaponPredictionUpdateGroup))]
[BurstCompile]
public partial struct StandardRaycastWeaponSimulationSystem : ISystem
{
    private NativeList<RaycastHit> _hits;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameResources>();
        state.RequireForUpdate<NetworkTime>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<StandardRaycastWeapon, StandardWeaponFiringMecanism>().Build());

        _hits = new NativeList<RaycastHit>(Allocator.Persistent);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_hits.IsCreated)
        {
            _hits.Dispose();
        }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GameResources gameResources = SystemAPI.GetSingleton<GameResources>();
        NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();
        NetworkId localNetworkId = default;
        if (SystemAPI.HasSingleton<NetworkId>())
        {
            localNetworkId = SystemAPI.GetSingleton<NetworkId>();
        }

        StandardRaycastWeaponSimulationJob simulationJob = new StandardRaycastWeaponSimulationJob
        {
            IsServer = state.WorldUnmanaged.IsServer(),
            OldestAllowedVFXRequestsTick = gameResources.GetOldestAllowedTickForPolledEventsTimeout(networkTime.ServerTick),
            NetworkTime = networkTime,
            LocalNetworkId = localNetworkId,
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            PhysicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            Hits = _hits,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
            ShotVFXDataLookup = SystemAPI.GetComponentLookup<StandardRaycastWeaponShotVFXData>(false),
            ShotVFXRequestsBufferLookup = SystemAPI.GetBufferLookup<StandardRaycastWeaponShotVFXRequest>(false),
        };
        simulationJob.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct StandardRaycastWeaponSimulationJob : IJobEntity
    {
        public bool IsServer;
        public uint OldestAllowedVFXRequestsTick;
        public NetworkTime NetworkTime;
        public NetworkId LocalNetworkId;
        [ReadOnly] 
        public PhysicsWorld PhysicsWorld;
        [ReadOnly]
        public PhysicsWorldHistorySingleton PhysicsWorldHistory;
        public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public ComponentLookup<Parent> ParentLookup;
        [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        public BufferLookup<StandardRaycastWeaponShotVFXRequest> ShotVFXRequestsBufferLookup;
        public ComponentLookup<StandardRaycastWeaponShotVFXData> ShotVFXDataLookup;
        public NativeList<RaycastHit> Hits;

        void Execute(
            Entity entity,
            ref StandardRaycastWeapon weapon,
            in GhostOwner ghostOwner,
            in InterpolationDelay interpolationDelay,
            in StandardWeaponFiringMecanism mecanism,
            in WeaponShotSimulationOriginOverride shotSimulationOriginOverride,
            in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
        {
            PhysicsWorldHistory.GetCollisionWorldFromTick(NetworkTime.ServerTick, interpolationDelay.Value, ref PhysicsWorld, out var collisionWorld);
            bool isOwner = ghostOwner.NetworkId == LocalNetworkId.Value;

            StandardRaycastWeaponShotVFXData vfxData = default;
            DynamicBuffer<StandardRaycastWeaponShotVFXRequest> vfxRequestsBuffer = default;
            switch (weapon.ProjectileVisualsSyncMode)
            {
                case RaycastProjectileVisualsSyncMode.Precise:
                    vfxRequestsBuffer = ShotVFXRequestsBufferLookup[entity];
                    break;
                case RaycastProjectileVisualsSyncMode.Efficient:
                    vfxData = ShotVFXDataLookup[entity];
                    // Init the data so that a ghost that enters our relevancy area doesn't fire all bullets since start
                    if (vfxData.IsInitialized == 0)
                    {
                        vfxData.LastProjectileSpawnCount = vfxData.ProjectileSpawnCount;
                        vfxData.IsInitialized = 1;
                    }
                    break;
            }

            if (mecanism.ShotsToFire > 0)
            {
                RigidTransform shotSimulationOrigin = WeaponUtilities.GetShotSimulationOrigin(
                    weapon.ShotOrigin,
                    in shotSimulationOriginOverride,
                    ref LocalTransformLookup,
                    ref ParentLookup,
                    ref PostTransformMatrixLookup);

                for (int i = 0; i < mecanism.ShotsToFire; i++)
                {
                    for (int j = 0; j < weapon.ProjectilesCount; j++)
                    {
                        // This needs to always run in prediction, because we modify random state
                        quaternion shotRotationWithSpread = WeaponUtilities.CalculateSpreadRotation(shotSimulationOrigin.rot, weapon.SpreadRadians, ref weapon.Random);
                        
                        if (NetworkTime.IsFirstTimeFullyPredictingTick)
                        {
                            WeaponUtilities.CalculateIndividualRaycastShot(
                                shotSimulationOrigin.pos,
                                shotRotationWithSpread,
                                in collisionWorld,
                                in weapon,
                                ref Hits,
                                in ignoredEntities,
                                out bool hitFound,
                                out float hitDistance,
                                out float3 hitNormal,
                                out Entity hitEntity,
                                out float3 shotDirection);

                            float3 shotEndPoint = shotSimulationOrigin.pos + (shotDirection * hitDistance);

                            // Damage
                            if (IsServer && hitFound)
                            {
                                if (HealthLookup.TryGetComponent(hitEntity, out Health health))
                                {
                                    health.CurrentHealth -= weapon.Damage;
                                    HealthLookup[hitEntity] = health;
                                }
                            }

                            switch (weapon.ProjectileVisualsSyncMode)
                            {
                                case RaycastProjectileVisualsSyncMode.Precise:
                                    // Add VFX request for this shot
                                    vfxRequestsBuffer.Add(new StandardRaycastWeaponShotVFXRequest
                                    {
                                        Tick = NetworkTime.ServerTick.TickIndexForValidTick,
                                        DidHit = hitFound ? (byte)1 : (byte)0,
                                        EndPoint = shotEndPoint,
                                        HitNormal = hitNormal,
                                    });
                                    break;
                                case RaycastProjectileVisualsSyncMode.Efficient:
                                    vfxData.ProjectileSpawnCount++;
                                    break;
                            }
                        }
                    }
                }
            }

            switch (weapon.ProjectileVisualsSyncMode)
            {
                case RaycastProjectileVisualsSyncMode.Precise:
                    // Clear VFX requests that are too old (if server or owner, since owners don't receive syncs from server for this buffer type)
                    if (IsServer || isOwner)
                    {
                        for (int k = vfxRequestsBuffer.Length - 1; k >= 0; k--)
                        {
                            StandardRaycastWeaponShotVFXRequest tmpRequest = vfxRequestsBuffer[k];
                            if (tmpRequest.Tick < OldestAllowedVFXRequestsTick)
                            {
                                vfxRequestsBuffer.RemoveAtSwapBack(k);
                            }
                        }
                    }
                    break;
                case RaycastProjectileVisualsSyncMode.Efficient:
                    ShotVFXDataLookup[entity] = vfxData;
                    break;
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(WeaponShotVisualsGroup))]
[UpdateBefore(typeof(WeaponShotVisualsSpawnECBSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct StandardRaycastWeaponShotVisualsSystem : ISystem
{
    private NativeList<RaycastHit> _hits;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<StandardRaycastWeapon, StandardWeaponFiringMecanism>().Build());

        _hits = new NativeList<RaycastHit>(Allocator.Persistent);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_hits.IsCreated)
        {
            _hits.Dispose();
        }
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StandardRaycastWeaponPreciseShotVisualsJob preciseVisualsJob = new StandardRaycastWeaponPreciseShotVisualsJob
        {
            ECB = SystemAPI.GetSingletonRW<WeaponShotVisualsSpawnECBSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
        };
        state.Dependency = preciseVisualsJob.Schedule(state.Dependency);
        
        StandardRaycastWeaponEfficientShotVisualsJob efficientVisualsJob = new StandardRaycastWeaponEfficientShotVisualsJob
        {
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            ECB = SystemAPI.GetSingletonRW<WeaponShotVisualsSpawnECBSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
            Hits = _hits,
        };
        state.Dependency = efficientVisualsJob.Schedule(state.Dependency);
    }
    
    [BurstCompile]
    public partial struct StandardRaycastWeaponPreciseShotVisualsJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;

        void Execute(
            ref StandardRaycastWeapon weapon, 
            ref DynamicBuffer<StandardRaycastWeaponShotVFXRequest> shotVFXRequestsBuffer)
        {
            // Process projectile VFX requests of ticks that we haven't already processed
            uint highestProcessedTick = weapon.LastProcessedVisualsTick;
            for (int i = 0; i < shotVFXRequestsBuffer.Length; i++)
            {
                StandardRaycastWeaponShotVFXRequest vfxRequest = shotVFXRequestsBuffer[i];
                if (vfxRequest.Tick > weapon.LastProcessedVisualsTick)
                {
                    // Spawn a projectile VFX that will go from current weapon shot origin to vfx request's end point
                    if (LocalToWorldLookup.TryGetComponent(weapon.ShotOrigin, out LocalToWorld originLtW))
                    {
                        float3 startPosition = originLtW.Position;
                        float3 startToEnd = vfxRequest.EndPoint - startPosition;
                        float3 startToEndDirection = math.normalizesafe(startToEnd);
                    
                        Entity shotVisualsEntity = ECB.Instantiate(weapon.ProjectileVisualPrefab);
                        ECB.SetComponent(shotVisualsEntity, LocalTransform.FromPositionRotation(startPosition, quaternion.LookRotationSafe(startToEndDirection, math.up())));
                        ECB.AddComponent(shotVisualsEntity, new StandardRaycastWeaponShotVisualsData
                        {
                            DidHit = vfxRequest.DidHit,
                            StartPoint = startPosition,
                            EndPoint = vfxRequest.EndPoint,
                            HitNormal = vfxRequest.HitNormal,
                        });
                    }
                    
                    highestProcessedTick = math.max(highestProcessedTick, vfxRequest.Tick);
                }
            }
            weapon.LastProcessedVisualsTick = highestProcessedTick;
        }
    }
    
    [BurstCompile]
    public partial struct StandardRaycastWeaponEfficientShotVisualsJob : IJobEntity
    {
        [ReadOnly] public CollisionWorld CollisionWorld;
        public EntityCommandBuffer ECB;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public ComponentLookup<Parent> ParentLookup;
        [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        public NativeList<RaycastHit> Hits;

        void Execute(
            ref StandardRaycastWeapon weapon, 
            ref StandardRaycastWeaponShotVFXData shotVFXData,
            in WeaponShotSimulationOriginOverride shotSimulationOriginOverride,
            in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
        {
            RigidTransform shotSimulationOrigin = WeaponUtilities.GetShotSimulationOrigin(
                weapon.ShotOrigin,
                in shotSimulationOriginOverride,
                ref LocalTransformLookup,
                ref ParentLookup,
                ref PostTransformMatrixLookup);
            
            // Process projectile VFX requests of ticks that we haven't already processed
            for (uint i = shotVFXData.LastProjectileSpawnCount; i < shotVFXData.ProjectileSpawnCount; i++)
            {
                // Spawn a projectile VFX that will go from current weapon shot origin to vfx request's end point
                if (LocalToWorldLookup.TryGetComponent(weapon.ShotOrigin, out LocalToWorld originLtW))
                {
                    quaternion shotRotationWithSpread = WeaponUtilities.CalculateSpreadRotation(shotSimulationOrigin.rot, weapon.SpreadRadians, ref weapon.Random);
                    WeaponUtilities.CalculateIndividualRaycastShot(
                        shotSimulationOrigin.pos,
                        shotRotationWithSpread,
                        in CollisionWorld,
                        in weapon,
                        ref Hits,
                        in ignoredEntities,
                        out bool hitFound,
                        out float hitDistance,
                        out float3 hitNormal,
                        out Entity hitEntity,
                        out float3 shotDirection);

                    float3 shotEndPoint = shotSimulationOrigin.pos + (shotDirection * hitDistance);
                    
                    float3 startPosition = originLtW.Position;
                    float3 startToEnd = shotEndPoint - startPosition;
                    float3 startToEndDirection = math.normalizesafe(startToEnd);

                    Entity shotVisualsEntity = ECB.Instantiate(weapon.ProjectileVisualPrefab);
                    ECB.SetComponent(shotVisualsEntity, LocalTransform.FromPositionRotation(startPosition, quaternion.LookRotationSafe(startToEndDirection, math.up())));
                    ECB.AddComponent(shotVisualsEntity, new StandardRaycastWeaponShotVisualsData
                    {
                        DidHit = hitFound ? (byte)1 : (byte)0,
                        StartPoint = startPosition,
                        EndPoint = shotEndPoint,
                        HitNormal = hitNormal,
                    });
                }
            }

            shotVFXData.LastProjectileSpawnCount = shotVFXData.ProjectileSpawnCount;
        }
    }
}
