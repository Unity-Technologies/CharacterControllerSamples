using System.Collections;
using System.Collections.Generic;
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
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace OnlineFPS
{
    [BurstCompile]
    [UpdateInGroup(typeof(ProjectilePredictionUpdateGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct RocketSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameResources>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RocketSimulationJob simulationJob = new RocketSimulationJob
            {
                IsServer = state.WorldUnmanaged.IsServer(),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
                CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
                DelayedDespawnLookup = SystemAPI.GetComponentLookup<DelayedDespawn>(false),
            };
            state.Dependency = simulationJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        [WithDisabled(typeof(DelayedDespawn))]
        public partial struct RocketSimulationJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public bool IsServer;
            public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public CollisionWorld CollisionWorld;
            public ComponentLookup<DelayedDespawn> DelayedDespawnLookup;

            [NativeDisableContainerSafetyRestriction]
            private NativeList<DistanceHit> Hits;

            void Execute(Entity entity, ref Rocket rocket, ref LocalTransform localTransform,
                in PrefabProjectile projectile)
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
                        if (CollisionWorld.OverlapSphere(localTransform.Position, rocket.DamageRadius, ref Hits,
                                CollisionFilter.Default))
                        {
                            for (int i = 0; i < Hits.Length; i++)
                            {
                                var hit = Hits[i];
                                if (HealthLookup.TryGetComponent(hit.Entity, out Health health2))
                                {
                                    float damageWithFalloff = rocket.MaxRadiusDamage *
                                                              (1f - math.saturate(hit.Distance / rocket.DamageRadius));
                                    health2.CurrentHealth -= damageWithFalloff;
                                    HealthLookup[hit.Entity] = health2;
                                }
                            }
                        }

                        // Activate delayed despawn
                        if (projectile.HitEntity != Entity.Null)
                        {
                            DelayedDespawnLookup.SetComponentEnabled(entity, true);
                        }

                        rocket.HasProcessedHitSimulation = 1;
                    }
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Hits.IsCreated)
                {
                    Hits = new NativeList<DistanceHit>(64, Allocator.Temp);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            {
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(ProjectileVisualsUpdateGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct RocketVFXSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VFXExplosionsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            VFXExplosionsSingleton vfxExplosionsSingleton = SystemAPI.GetSingletonRW<VFXExplosionsSingleton>().ValueRW;

            RocketVFXJob job = new RocketVFXJob
            {
                VFXExplosionsManager = vfxExplosionsSingleton.Manager,
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct RocketVFXJob : IJobEntity
        {
            public VFXManager<VFXExplosionRequest> VFXExplosionsManager;

            void Execute(Entity entity, in LocalTransform transform, ref Rocket rocket, in PrefabProjectile projectile)
            {
                if (rocket.HasProcessedHitVFX == 0 && projectile.HasHit == 1)
                {
                    VFXExplosionsManager.AddRequest(new VFXExplosionRequest
                    {
                        Position = transform.Position,
                        Size = rocket.ExplosionSize,
                    });

                    rocket.HasProcessedHitVFX = 1;
                }
            }
        }
    }
}