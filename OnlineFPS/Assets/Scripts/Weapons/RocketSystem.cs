using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[BurstCompile]
[UpdateInGroup(typeof(ProjectilePredictionUpdateGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial struct RocketSimulationSystem : ISystem
{
    private NativeList<DistanceHit> _hits;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameResources>();
        state.RequireForUpdate<PhysicsWorldSingleton>();

        _hits = new NativeList<DistanceHit>(Allocator.Persistent);
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
        RocketJob job = new RocketJob
        {
            IsServer = state.WorldUnmanaged.IsServer(), 
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            DespawnTime = SystemAPI.GetSingleton<GameResources>().PolledEventsTimeout,
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            Hits = _hits,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    [WithNone(typeof(DelayedDespawn))]
    public partial struct RocketJob : IJobEntity
    {
        public bool IsServer;
        public EntityCommandBuffer ECB;
        public float DespawnTime;
        public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public CollisionWorld CollisionWorld;
        public NativeList<DistanceHit> Hits;

        void Execute(Entity entity, ref Rocket rocket, ref LocalTransform localTransform, in Projectile projectile)
        {
            if (IsServer)
            {
                // Hit processing
                if (rocket.HasProcessedHitSimulation == 0 && projectile.HasHit == 1)
                {
                    // Direct hit damage
                    if (HealthLookup.TryGetComponent(projectile.HitEntity, out Health health))
                    {
                        health.CurrentHealth -= rocket.DirectHitDamage;
                        HealthLookup[projectile.HitEntity] = health;
                    }

                    // Area damage
                    Hits.Clear();
                    if (CollisionWorld.OverlapSphere(localTransform.Position, rocket.DamageRadius, ref Hits, CollisionFilter.Default))
                    {
                        for (int i = 0; i < Hits.Length; i++)
                        {
                            var hit = Hits[i];
                            if (HealthLookup.TryGetComponent(hit.Entity, out Health health2))
                            {
                                float damageWithFalloff = rocket.MaxRadiusDamage * (1f - math.saturate(hit.Distance / rocket.DamageRadius));
                                health2.CurrentHealth -= damageWithFalloff;
                                HealthLookup[hit.Entity] = health2;
                            }
                        }
                    }

                    // Activate delayed despawn
                    ECB.AddComponent(entity, new DelayedDespawn
                    {
                        Timer = DespawnTime,
                    });
                    
                    rocket.HasProcessedHitSimulation = 1;
                }
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct RocketVFXSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        RocketVFXJob job = new RocketVFXJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct RocketVFXJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        
        void Execute(Entity entity, ref Rocket rocket, in Projectile projectile)
        {
            if (rocket.HasProcessedHitVFX == 0 && projectile.HasHit == 1)
            {
                ECB.AddComponent(entity, new DelayedDespawn());
                
                Entity hitVFXEntity = ECB.Instantiate(rocket.HitVFXPrefab);
                ECB.SetComponent(hitVFXEntity, LocalTransform.FromPositionRotation(projectile.HitPosition, quaternion.LookRotationSafe(projectile.HitNormal, math.up())));
                
                rocket.HasProcessedHitVFX = 1;
            }
        }
    }
}
