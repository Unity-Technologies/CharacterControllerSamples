using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Rival;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(WeaponPredictionUpdateGroup))]
[BurstCompile]
public partial struct StandardRaycastWeaponPredictionSystem : ISystem
{
    private NativeList<RaycastHit> _hits;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
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
        StandardRaycastWeaponPredictionJob predictionJob = new StandardRaycastWeaponPredictionJob
        {
            IsServer = state.WorldUnmanaged.IsServer(), 
            NetworkTime = SystemAPI.GetSingleton<NetworkTime>(),
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            PhysicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            StoredKinematicCharacterDataLookup = SystemAPI.GetComponentLookup<StoredKinematicCharacterData>(true),
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            Hits = _hits,
        };
        predictionJob.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct StandardRaycastWeaponPredictionJob : IJobEntity
    {
        public bool IsServer;
        public NetworkTime NetworkTime;
        public PhysicsWorld PhysicsWorld;
        public PhysicsWorldHistorySingleton PhysicsWorldHistory;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly]
        public ComponentLookup<StoredKinematicCharacterData> StoredKinematicCharacterDataLookup;
        public ComponentLookup<Health> HealthLookup;
        public NativeList<RaycastHit> Hits;

        void Execute(
            Entity entity, 
            ref StandardRaycastWeapon weapon, 
            ref WeaponVisualFeedback weaponFeedback,
            ref DynamicBuffer<StandardRaycastWeaponShotVFXRequest> shotVFXRequestsBuffer,
            in InterpolationDelay interpolationDelay,
            in StandardWeaponFiringMecanism mecanism, 
            in WeaponShotSimulationOriginOverride shotSimulationOriginOverride, 
            in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
        {
            PhysicsWorldHistory.GetCollisionWorldFromTick(NetworkTime.ServerTick, interpolationDelay.Value, ref PhysicsWorld, out var collisionWorld);
            
            for (int i = 0; i < mecanism.ShotsToFire; i++)
            {
                WeaponUtilities.ComputeShotDetails(
                    ref weapon, 
                    in shotSimulationOriginOverride,
                    in ignoredEntities,
                    ref Hits,
                    in collisionWorld,
                    in LocalToWorldLookup,
                    in StoredKinematicCharacterDataLookup,
                    out bool hitFound,
                    out RaycastHit closestValidHit,
                    out StandardRaycastWeaponShotVisualsData shotVisualsData);

                // Damage
                if (IsServer && hitFound)
                {
                    if (HealthLookup.TryGetComponent(closestValidHit.Entity, out Health health))
                    {
                        health.CurrentHealth -= weapon.Damage;
                        HealthLookup[closestValidHit.Entity] = health;
                    }
                }

                // Shot visuals request
                if (!IsServer && NetworkTime.IsFirstTimeFullyPredictingTick)
                {
                    shotVFXRequestsBuffer.Add(new StandardRaycastWeaponShotVFXRequest { ShotVisualsData = shotVisualsData});
                }
                
                // Recoil & FOV kick
                if (IsServer)
                {
                    weapon.RemoteShotsCount++;
                }
                else if (NetworkTime.IsFirstTimeFullyPredictingTick)
                {
                    weaponFeedback.ShotFeedbackRequests++;
                }
            }
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct StandardRaycastWeaponVisualsSystem : ISystem
{
    private NativeList<RaycastHit> _hits;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
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
        int localNetId = -1;
        if (SystemAPI.HasSingleton<NetworkIdComponent>())
        {
            localNetId = SystemAPI.GetSingleton<NetworkIdComponent>().Value;
        }
        
        StandardRaycastWeaponRemoteShotsJob remoteShotsJob = new StandardRaycastWeaponRemoteShotsJob
        {
            LocalNetId = localNetId, 
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            StoredKinematicCharacterDataLookup = SystemAPI.GetComponentLookup<StoredKinematicCharacterData>(true),
            Hits = _hits,
        };
        remoteShotsJob.Schedule();

        StandardRaycastWeaponShotVisualsJob visualsJob = new StandardRaycastWeaponShotVisualsJob
        {
            ECB = SystemAPI.GetSingletonRW<PostPredictionPreTransformsECBSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            CharacterWeaponVisualFeedbackLookup = SystemAPI.GetComponentLookup<CharacterWeaponVisualFeedback>(false),
        };
        visualsJob.Schedule();
    }

    [BurstCompile]
    public partial struct StandardRaycastWeaponRemoteShotsJob : IJobEntity
    {
        public int LocalNetId;
        public CollisionWorld CollisionWorld;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly]
        public ComponentLookup<StoredKinematicCharacterData> StoredKinematicCharacterDataLookup;
        public NativeList<RaycastHit> Hits;

        void Execute(
            Entity entity, 
            ref StandardRaycastWeapon weapon, 
            ref WeaponVisualFeedback weaponFeedback,
            ref DynamicBuffer<StandardRaycastWeaponShotVFXRequest> shotVFXRequestsBuffer,
            in GhostOwnerComponent ghostOwnerComponent,
            in WeaponShotSimulationOriginOverride shotSimulationOriginOverride, 
            in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
        {
            if (ghostOwnerComponent.NetworkId != LocalNetId)
            {
                // TODO: should handle the case where a weapon goes out of client's area-of-interest and then comes back later with a high shots count diff
                uint shotsToProcess = weapon.RemoteShotsCount - weapon.LastRemoteShotsCount;
                weapon.LastRemoteShotsCount = weapon.RemoteShotsCount;

                for (int i = 0; i < shotsToProcess; i++)
                {
                    WeaponUtilities.ComputeShotDetails(
                        ref weapon,
                        in shotSimulationOriginOverride,
                        in ignoredEntities,
                        ref Hits,
                        in CollisionWorld,
                        in LocalToWorldLookup,
                        in StoredKinematicCharacterDataLookup,
                        out bool hitFound,
                        out RaycastHit closestValidHit,
                        out StandardRaycastWeaponShotVisualsData shotVisualsData);

                    shotVFXRequestsBuffer.Add(new StandardRaycastWeaponShotVFXRequest { ShotVisualsData = shotVisualsData });
                    weaponFeedback.ShotFeedbackRequests++;
                }
            }
        }
    }

    [BurstCompile]
    public partial struct StandardRaycastWeaponShotVisualsJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public ComponentLookup<CharacterWeaponVisualFeedback> CharacterWeaponVisualFeedbackLookup;

        void Execute(
            Entity entity, 
            ref StandardRaycastWeapon weapon, 
            ref WeaponVisualFeedback weaponFeedback,
            ref DynamicBuffer<StandardRaycastWeaponShotVFXRequest> shotVFXRequestsBuffer,
            in WeaponOwner owner)
        {
            // Shot VFX
            for (int i = 0; i < shotVFXRequestsBuffer.Length; i++)
            {
                StandardRaycastWeaponShotVisualsData shotVisualsData = shotVFXRequestsBuffer[i].ShotVisualsData;
                
                Entity shotVisualsEntity = ECB.Instantiate(weapon.ProjectileVisualPrefab);
                ECB.SetComponent(shotVisualsEntity, LocalTransform.FromPositionRotation(shotVisualsData.VisualOrigin, quaternion.LookRotationSafe(shotVisualsData.SimulationDirection, shotVisualsData.SimulationUp)));
                ECB.AddComponent(shotVisualsEntity, shotVisualsData);
            }
            shotVFXRequestsBuffer.Clear();

            // Shot feedback
            for (int i = 0; i < weaponFeedback.ShotFeedbackRequests; i++)
            {
                if (CharacterWeaponVisualFeedbackLookup.TryGetComponent(owner.Entity, out CharacterWeaponVisualFeedback characterFeedback))
                {
                    characterFeedback.CurrentRecoil += weaponFeedback.RecoilStrength;
                    characterFeedback.TargetRecoilFOVKick += weaponFeedback.RecoilFOVKick;

                    CharacterWeaponVisualFeedbackLookup[owner.Entity] = characterFeedback;
                }
            }
            weaponFeedback.ShotFeedbackRequests = 0;
        }
    }
}