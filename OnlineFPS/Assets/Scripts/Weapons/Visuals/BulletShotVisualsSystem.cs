using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(WeaponShotVisualsGroup))]
[UpdateAfter(typeof(WeaponShotVisualsSpawnECBSystem))]
[BurstCompile]
public partial struct BulletShotVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BulletShotVisuals, StandardRaycastWeaponShotVisualsData>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        BulletShotVisualsJob job = new BulletShotVisualsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct BulletShotVisualsJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref BulletShotVisuals shotVisuals, ref LocalTransform localTransform, ref LocalToWorld ltw, in StandardRaycastWeaponShotVisualsData shotData)
        {
            if (!shotVisuals.IsInitialized)
            {
                // Hit VFX
                if (shotData.DidHit == 1)
                {
                    Entity hitVisualsEntity = ECB.Instantiate(chunkIndexInQuery, shotVisuals.HitVisualsPrefab);
                    ECB.SetComponent(chunkIndexInQuery, hitVisualsEntity, LocalTransform.FromPositionRotation(shotData.EndPoint, quaternion.LookRotationSafe(shotData.HitNormal, math.up())));
                }

                shotVisuals.IsInitialized = true;
            }

            // Speed
            float3 movedDistance = math.mul(localTransform.Rotation, math.forward()) * shotVisuals.Speed * DeltaTime;
            localTransform.Position += movedDistance;
            shotVisuals.DistanceTraveled += math.length(movedDistance);

            // Stretch
            float zScale = math.clamp(shotVisuals.Speed * shotVisuals.StretchFromSpeed, 0f, math.min(shotVisuals.DistanceTraveled, shotVisuals.MaxStretch));

            // On reached hit
            if (shotVisuals.DistanceTraveled >= shotData.GetLength())
            {
                // clamp position to max dist
                float preClampDistFromOrigin = math.length(localTransform.Position - shotData.StartPoint);
                localTransform.Position = shotData.EndPoint;

                // adjust scale stretch for clamped pos
                zScale *= math.length(localTransform.Position - shotData.StartPoint) / preClampDistFromOrigin;

                ECB.DestroyEntity(chunkIndexInQuery, entity);
            }

            // Calculating the LocalToWorld manually since this is happening after the transforms group update
            ltw.Value = float4x4.TRS(localTransform.Position, localTransform.Rotation, new float3(localTransform.Scale, localTransform.Scale, zScale));
        }
    }
}