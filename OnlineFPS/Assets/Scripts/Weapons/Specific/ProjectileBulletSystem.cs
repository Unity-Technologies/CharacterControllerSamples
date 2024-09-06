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
    public partial struct ProjectileBulletSimulationSystem : ISystem
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
            ProjectileBulletSimulationJob simulationJob = new ProjectileBulletSimulationJob
            {
                IsServer = state.WorldUnmanaged.IsServer(),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
                DelayedDespawnLookup = SystemAPI.GetComponentLookup<DelayedDespawn>(false),
            };
            state.Dependency = simulationJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        [WithDisabled(typeof(DelayedDespawn))]
        public partial struct ProjectileBulletSimulationJob : IJobEntity
        {
            public bool IsServer;
            public ComponentLookup<Health> HealthLookup;
            public ComponentLookup<DelayedDespawn> DelayedDespawnLookup;

            void Execute(Entity entity, ref ProjectileBullet bullet, in PrefabProjectile projectile)
            {
                if (IsServer)
                {
                    // Hit processing
                    if (bullet.HasProcessedHitSimulation == 0 && projectile.HasHit == 1)
                    {
                        // Direct hit damage
                        if (HealthLookup.TryGetComponent(projectile.HitEntity, out Health health))
                        {
                            health.CurrentHealth -= bullet.Damage;
                            HealthLookup[projectile.HitEntity] = health;
                        }

                        // Activate delayed despawn
                        if (projectile.HitEntity != Entity.Null)
                        {
                            DelayedDespawnLookup.SetComponentEnabled(entity, true);
                        }

                        bullet.HasProcessedHitSimulation = 1;
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(ProjectileVisualsUpdateGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ProjectileBulletVFXSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VFXExplosionsSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            VFXSparksSingleton vfxSparksSingleton = SystemAPI.GetSingletonRW<VFXSparksSingleton>().ValueRW;

            ProjectileBulletVFXJob job = new ProjectileBulletVFXJob
            {
                VFXSparksManager = vfxSparksSingleton.Manager,
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct ProjectileBulletVFXJob : IJobEntity
        {
            public VFXManager<VFXSparksRequest> VFXSparksManager;

            void Execute(Entity entity, in LocalTransform transform, ref ProjectileBullet bullet,
                in PrefabProjectile projectile)
            {
                if (bullet.HasProcessedHitVFX == 0 && projectile.HasHit == 1)
                {
                    VFXSparksManager.AddRequest(new VFXSparksRequest
                    {
                        Position = transform.Position,
                        Color = bullet.HitSparksColor,
                        Size = bullet.HitSparksSize,
                        Speed = bullet.HitSparksSpeed,
                        Lifetime = bullet.HitSparksLifetime,
                    });

                    bullet.HasProcessedHitVFX = 1;
                }
            }
        }
    }
}