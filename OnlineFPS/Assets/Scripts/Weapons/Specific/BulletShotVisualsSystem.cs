using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(ProjectileVisualsUpdateGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct BulletShotVisualsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VFXSparksSingleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BulletShotVisuals, RaycastProjectile>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            VFXSparksSingleton vfxSparksSingleton = SystemAPI.GetSingletonRW<VFXSparksSingleton>().ValueRW;

            BulletShotVisualsJob job = new BulletShotVisualsJob
            {
                VFXSparksManager = vfxSparksSingleton.Manager,
                DeltaTime = SystemAPI.Time.DeltaTime,
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct BulletShotVisualsJob : IJobEntity
        {
            public VFXManager<VFXSparksRequest> VFXSparksManager;
            public float DeltaTime;
            public EntityCommandBuffer ECB;

            void Execute(Entity entity, ref BulletShotVisuals shotVisuals, ref LocalTransform localTransform,
                ref PostTransformMatrix postTransformMatrix, in RaycastVisualProjectile visualProjectile)
            {
                if (!shotVisuals.IsInitialized)
                {
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

                    shotVisuals.IsInitialized = true;
                }

                // Speed
                float3 movedDistance =
                    math.mul(localTransform.Rotation, math.forward()) * shotVisuals.Speed * DeltaTime;
                localTransform.Position += movedDistance;
                shotVisuals.DistanceTraveled += math.length(movedDistance);

                // Stretch
                float zScale = math.clamp(shotVisuals.Speed * shotVisuals.StretchFromSpeed, 0f,
                    math.min(shotVisuals.DistanceTraveled, shotVisuals.MaxStretch));

                // On reached hit
                if (shotVisuals.DistanceTraveled >= visualProjectile.GetLengthOfTrajectory())
                {
                    // clamp position to max dist
                    float preClampDistFromOrigin = math.length(localTransform.Position - visualProjectile.StartPoint);
                    localTransform.Position = visualProjectile.EndPoint;

                    // adjust scale stretch for clamped pos
                    zScale *= math.length(localTransform.Position - visualProjectile.StartPoint) /
                              preClampDistFromOrigin;

                    ECB.DestroyEntity(entity);
                }

                postTransformMatrix.Value = float4x4.Scale(1f, 1f, zScale);
            }
        }
    }
}