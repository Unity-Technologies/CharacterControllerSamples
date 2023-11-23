
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using Unity.Logging;
using Unity.NetCode.LowLevel;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[BurstCompile]
[UpdateInGroup(typeof(ProjectilePredictionUpdateGroup), OrderFirst = true)]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial struct ProjectileSimulationSystem : ISystem
{
    private NativeList<RaycastHit> _hits;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkTime>();
        state.RequireForUpdate<PhysicsWorldSingleton>();

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
        ProjectileSimulationsJob job = new ProjectileSimulationsJob
        {
            IsServer = state.WorldUnmanaged.IsServer(),
            DeltaTime = SystemAPI.Time.DeltaTime,
            NetworkTime = SystemAPI.GetSingleton<NetworkTime>(),
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            Hits = _hits,
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            PhysicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
            InterpolationDelayLookup = SystemAPI.GetComponentLookup<InterpolationDelay>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ProjectileSimulationsJob : IJobEntity
    {
        public bool IsServer;
        public float DeltaTime;
        public NetworkTime NetworkTime;
        public EntityCommandBuffer ECB;
        [ReadOnly] 
        public PhysicsWorld PhysicsWorld;
        [ReadOnly]
        public PhysicsWorldHistorySingleton PhysicsWorldHistory;
        [ReadOnly] 
        public ComponentLookup<InterpolationDelay> InterpolationDelayLookup;
        public NativeList<RaycastHit> Hits;

        void Execute(Entity entity, ref Projectile projectile, ref LocalTransform localTransform, in ProjectileSpawnId spawnId, in DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities)
        {
            if (projectile.HasHit == 0)
            {
                uint interpolationDelay = 0;
                if (InterpolationDelayLookup.TryGetComponent(spawnId.WeaponEntity, out InterpolationDelay interpolationDelayComp))
                {
                    interpolationDelay = interpolationDelayComp.Value;
                }
                PhysicsWorldHistory.GetCollisionWorldFromTick(NetworkTime.ServerTick, interpolationDelay, ref PhysicsWorld, out var collisionWorld);
                
                // Movement
                float3 rocketForward = math.mul(localTransform.Rotation, math.forward());
                float3 displacement = rocketForward * projectile.Speed * DeltaTime;

                // Hit detection
                if (NetworkTime.IsFirstTimeFullyPredictingTick)
                {
                    Hits.Clear();
                    RaycastInput raycastInput = new RaycastInput
                    {
                        Start = localTransform.Position,
                        End = localTransform.Position + displacement,
                        Filter = CollisionFilter.Default,
                    };
                    collisionWorld.CastRay(raycastInput, ref Hits);
                    if (WeaponUtilities.GetClosestValidWeaponRaycastHit(in Hits, in ignoredEntities, out RaycastHit closestValidHit))
                    {
                        displacement *= closestValidHit.Fraction;
                        projectile.HitEntity = closestValidHit.Entity;
                        projectile.HitPosition = closestValidHit.Position;
                        projectile.HitNormal = closestValidHit.SurfaceNormal;
                        projectile.HasHit = 1;
                    }
                }

                // Advance position 
                localTransform.Position += displacement;
            }

            // Lifetime
            projectile.LifetimeCounter += DeltaTime;
            if (IsServer)
            {
                if (projectile.LifetimeCounter >= projectile.MaxLifetime)
                {
                    ECB.DestroyEntity(entity);
                }
            }
        }
    }
}

/// <summary>
/// This system handles offsetting the projectile's visual render meshes so that they look like they're coming out of the weapon's
/// barrel instead of the center of the camera. It merges the visual projectile's position with the camera-centered trajectory
/// over a certain period of time.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(LocalToWorldSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct ProjectileShotVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ProjectilsShotVisualsJob job = new ProjectilsShotVisualsJob
        { };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct ProjectilsShotVisualsJob : IJobEntity
    {
        void Execute(ref LocalToWorld ltw, in LocalTransform transform, in Projectile projectile, in ProjectileShotVisuals projectileVisuals)
        {
            float3 visualOffset = math.lerp(projectileVisuals.VisualOffset, float3.zero, math.saturate(projectile.LifetimeCounter / projectileVisuals.VisualOffsetCorrectionDuration));
            float4x4 visualOffsetTransform = float4x4.Translate(visualOffset);
            ltw.Value = math.mul(visualOffsetTransform, float4x4.TRS(transform.Position, transform.Rotation, transform.Scale));
        }
    }
}


[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
[CreateAfter(typeof(GhostCollectionSystem))]
[CreateAfter(typeof(GhostReceiveSystem))]
[UpdateAfter(typeof(GhostSpawnClassificationSystem))]
[BurstCompile]
public partial struct ProjectileClassificationSystem : ISystem
{
    private SnapshotDataLookupHelper _snapshotDataLookupHelper;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _snapshotDataLookupHelper = new SnapshotDataLookupHelper(ref state, SystemAPI.GetSingletonEntity<GhostCollection>(), SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>());
        
        state.RequireForUpdate<GhostSpawnQueue>();
        state.RequireForUpdate<PredictedGhostSpawnList>();
        state.RequireForUpdate<NetworkId>();
        state.RequireForUpdate<ProjectileSpawnId>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _snapshotDataLookupHelper.Update(ref state);
        
        ProjectileClassificationJob projectileClassificationJob = new ProjectileClassificationJob
        {
            Frame = SystemAPI.GetSingleton<NetworkTime>().ServerTick.TickIndexForValidTick,
            SnapshotDataLookupHelper = _snapshotDataLookupHelper,
            SpawnListEntity = SystemAPI.GetSingletonEntity<PredictedGhostSpawnList>(),
            PredictedSpawnListLookup = SystemAPI.GetBufferLookup<PredictedGhostSpawn>(),
            ProjectileSpawnIdLookup = SystemAPI.GetComponentLookup<ProjectileSpawnId>(),
        };
        state.Dependency = projectileClassificationJob.Schedule(state.Dependency);
    }

    [WithAll(typeof(GhostSpawnQueue))]
    [BurstCompile]
    partial struct ProjectileClassificationJob : IJobEntity
    {
        public uint Frame;
        
        public SnapshotDataLookupHelper SnapshotDataLookupHelper;
        public Entity SpawnListEntity;
        public BufferLookup<PredictedGhostSpawn> PredictedSpawnListLookup;
        public ComponentLookup<ProjectileSpawnId> ProjectileSpawnIdLookup;

        public void Execute(DynamicBuffer<GhostSpawnBuffer> ghostsToSpawn, DynamicBuffer<SnapshotDataBuffer> snapshotDataBuffer)
        {
            DynamicBuffer<PredictedGhostSpawn> predictedSpawnList = PredictedSpawnListLookup[SpawnListEntity];
            SnapshotDataBufferComponentLookup snapshotDataLookup = SnapshotDataLookupHelper.CreateSnapshotBufferLookup();
            
            // For each ghost that must now be spawned...
            for (int i = 0; i < ghostsToSpawn.Length; ++i)
            {
                GhostSpawnBuffer newGhostSpawn = ghostsToSpawn[i];
                
                // If this is a predicted spawn and we haven't already classified it...
                if (newGhostSpawn.SpawnType == GhostSpawnBuffer.Type.Predicted && !newGhostSpawn.HasClassifiedPredictedSpawn && newGhostSpawn.PredictedSpawnEntity == Entity.Null)
                {
                    // If this is a projectile...
                    if (snapshotDataLookup.TryGetComponentDataFromSnapshotHistory(newGhostSpawn.GhostType, snapshotDataBuffer, out ProjectileSpawnId newGhostSpawnId, i))
                    {
                        // Mark all projectiles as classified by default
                        newGhostSpawn.HasClassifiedPredictedSpawn = true;

                        // Try to find exact match b ID among our local predicted spawns
                        for (int j = predictedSpawnList.Length - 1; j >= 0; j--)
                        {
                            PredictedGhostSpawn predictedSpawn = predictedSpawnList[j];
                            
                            // If same type of ghost prefab...
                            if (newGhostSpawn.GhostType == predictedSpawn.ghostType)
                            {
                                // If same spawn ID...
                                ProjectileSpawnId predictedGhostSpawnId = ProjectileSpawnIdLookup[predictedSpawn.entity];
                                if (predictedGhostSpawnId.IsSame(newGhostSpawnId))
                                {
                                    // Assign the correct matching entity
                                    newGhostSpawn.PredictedSpawnEntity = predictedSpawn.entity;
                                    predictedSpawnList.RemoveAtSwapBack(j);
                                    break;
                                }
                            }
                        }
                    }
                    
                    ghostsToSpawn[i] = newGhostSpawn;
                }
            }
        }
    }
}