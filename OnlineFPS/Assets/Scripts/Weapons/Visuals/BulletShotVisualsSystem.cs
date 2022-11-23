using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateAfter(typeof(PostPredictionPreTransformsECBSystem))]
[BurstCompile]
public partial struct BulletShotVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BulletShotVisuals, StandardRaycastWeaponShotVisualsData>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        BulletShotVisualsJob job = new BulletShotVisualsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct BulletShotVisualsJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref BulletShotVisuals shotVisuals, ref LocalTransform localTransform, ref PostTransformScale postTransformScale, in StandardRaycastWeaponShotVisualsData shotData)
        {
            if (!shotVisuals.IsInitialized)
            {
                // Hit VFX
                if (shotData.Hit.Entity != Entity.Null)
                {
                    Entity hitVisualsEntity = ECB.Instantiate(chunkIndexInQuery, shotVisuals.HitVisualsPrefab);
                    ECB.SetComponent(chunkIndexInQuery, hitVisualsEntity, LocalTransform.FromPositionRotation(shotData.Hit.Position, quaternion.LookRotationSafe(shotData.Hit.SurfaceNormal, math.up())));
                }

                // Orient bullet
                localTransform.Rotation = quaternion.LookRotationSafe(shotData.SimulationDirection, math.up());

                shotVisuals.IsInitialized = true;
            }

            // Speed
            float3 movedDistance = math.mul(localTransform.Rotation, math.forward()) * shotVisuals.Speed * DeltaTime;
            localTransform.Position += movedDistance;
            shotVisuals.DistanceTraveled += math.length(movedDistance);

            // Stretch
            float zScale = math.clamp(shotVisuals.Speed * shotVisuals.StretchFromSpeed, 0f, math.min(shotVisuals.DistanceTraveled, shotVisuals.MaxStretch));

            // On reached hit
            if (shotVisuals.DistanceTraveled >= shotData.SimulationHitDistance)
            {
                // clamp position to max dist
                float preClampDistFromOrigin = math.length(localTransform.Position - shotData.VisualOrigin);
                localTransform.Position = shotData.VisualOrigin + shotData.VisualOriginToHit;

                // adjust scale stretch for clamped pos
                zScale *= math.length(localTransform.Position - shotData.VisualOrigin) / preClampDistFromOrigin;

                ECB.DestroyEntity(chunkIndexInQuery, entity);
            }

            postTransformScale.Value = float3x3.Scale(localTransform.Scale, localTransform.Scale, zScale);
        }
    }
}