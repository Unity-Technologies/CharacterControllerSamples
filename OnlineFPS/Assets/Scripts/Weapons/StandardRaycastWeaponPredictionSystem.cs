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
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            Hits = _hits,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
        };
        predictionJob.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct StandardRaycastWeaponPredictionJob : IJobEntity
    {
        public bool IsServer;
        public NetworkTime NetworkTime;
        [ReadOnly]
        public PhysicsWorld PhysicsWorld;
        [ReadOnly]
        public PhysicsWorldHistorySingleton PhysicsWorldHistory;
        public ComponentLookup<Health> HealthLookup;
        [ReadOnly]
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly]
        public ComponentLookup<Parent> ParentLookup;
        [ReadOnly]
        public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
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

            bool computeShotVisuals = !IsServer && NetworkTime.IsFirstTimeFullyPredictingTick;
            
            for (int i = 0; i < mecanism.ShotsToFire; i++)
            {
                WeaponUtilities.ComputeShotDetails(
                    ref weapon, 
                    in shotSimulationOriginOverride,
                    in ignoredEntities,
                    ref Hits,
                    ref LocalTransformLookup,
                    ref ParentLookup,
                    ref PostTransformMatrixLookup,
                    in collisionWorld,
                    computeShotVisuals,
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
                if (computeShotVisuals)
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
        StandardRaycastWeaponRemoteShotsJob remoteShotsJob = new StandardRaycastWeaponRemoteShotsJob
        { 
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            StoredKinematicCharacterDataLookup = SystemAPI.GetComponentLookup<StoredKinematicCharacterData>(true),
            Hits = _hits,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
        };
        remoteShotsJob.Schedule();
    }

    [BurstCompile]
    [WithNone(typeof(GhostOwnerIsLocal))]
    public partial struct StandardRaycastWeaponRemoteShotsJob : IJobEntity
    {
        [ReadOnly]
        public CollisionWorld CollisionWorld;
        [ReadOnly]
        public ComponentLookup<StoredKinematicCharacterData> StoredKinematicCharacterDataLookup;
        [ReadOnly]
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly]
        public ComponentLookup<Parent> ParentLookup;
        [ReadOnly]
        public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        public NativeList<RaycastHit> Hits;

        void Execute(
            Entity entity,
            ref StandardRaycastWeapon weapon,
            ref WeaponVisualFeedback weaponFeedback,
            ref DynamicBuffer<StandardRaycastWeaponShotVFXRequest> shotVFXRequestsBuffer,
            in WeaponShotSimulationOriginOverride shotSimulationOriginOverride,
            in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
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
                    ref LocalTransformLookup,
                    ref ParentLookup,
                    ref PostTransformMatrixLookup,
                    in CollisionWorld,
                    true,
                    out bool hitFound,
                    out RaycastHit closestValidHit,
                    out StandardRaycastWeaponShotVisualsData shotVisualsData);

                shotVFXRequestsBuffer.Add(new StandardRaycastWeaponShotVFXRequest { ShotVisualsData = shotVisualsData });
                weaponFeedback.ShotFeedbackRequests++;
            }
        }
    }
}