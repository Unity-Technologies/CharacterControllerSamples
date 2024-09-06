using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(ProjectileVisualsUpdateGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct LazerShotVisualsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VFXSparksSingleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder()
                .WithAll<LazerShotVisuals, LocalTransform, PostTransformMatrix, RaycastVisualProjectile>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            VFXSparksSingleton vfxSparksSingleton = SystemAPI.GetSingletonRW<VFXSparksSingleton>().ValueRW;

            LazerShotVisualsJob job = new LazerShotVisualsJob
            {
                VFXSparksManager = vfxSparksSingleton.Manager,
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct LazerShotVisualsJob : IJobEntity
        {
            public VFXManager<VFXSparksRequest> VFXSparksManager;
            public float ElapsedTime;
            public EntityCommandBuffer ECB;

            void Execute(Entity entity, ref LazerShotVisuals shotVisuals, ref LocalTransform localTransform,
                ref PostTransformMatrix postTransformMatrix, in RaycastVisualProjectile visualProjectile)
            {
                if (!shotVisuals.HasInitialized)
                {
                    shotVisuals.StartTime = ElapsedTime;

                    // Scale
                    shotVisuals.StartingScale = new float3(shotVisuals.Width, shotVisuals.Width,
                        visualProjectile.GetLengthOfTrajectory());

                    // Hit VFX
                    if (visualProjectile.DidHit == 1)
                    {
                        VFXSparksManager.AddRequest(new VFXSparksRequest
                        {
                            Position = visualProjectile.EndPoint,
                            Color = shotVisuals.HitSparksColor,
                            Size = shotVisuals.HitSparksSize,
                            Speed = shotVisuals.HitSparksSpeed,
                            Lifetime = shotVisuals.HitSparksLifetime,
                        });
                    }

                    shotVisuals.HasInitialized = true;
                }

                if (shotVisuals.LifeTime > 0f)
                {
                    float timeRatio = (ElapsedTime - shotVisuals.StartTime) / shotVisuals.LifeTime;
                    float clampedTimeRatio = math.clamp(timeRatio, 0f, 1f);
                    float invTimeRatio = 1f - clampedTimeRatio;

                    if (timeRatio >= 1f)
                    {
                        ECB.DestroyEntity(entity);
                    }

                    postTransformMatrix.Value = float4x4.Scale(new float3(shotVisuals.StartingScale.x * invTimeRatio,
                        shotVisuals.StartingScale.y * invTimeRatio, shotVisuals.StartingScale.z));
                }
                else
                {
                    ECB.DestroyEntity(entity);
                }
            }
        }
    }
}
