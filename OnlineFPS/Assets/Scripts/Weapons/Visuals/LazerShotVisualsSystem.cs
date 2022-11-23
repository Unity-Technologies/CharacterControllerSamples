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
public partial struct LazerShotVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<LazerShotVisuals, LocalTransform, PostTransformScale, StandardRaycastWeaponShotVisualsData>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        LazerShotVisualsJob job = new LazerShotVisualsJob
        {
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct LazerShotVisualsJob : IJobEntity
    {
        public float ElapsedTime;
        public EntityCommandBuffer ECB;
        
        void Execute(Entity entity, ref LazerShotVisuals shotVisuals, ref LocalTransform localTransform, ref PostTransformScale postTransformScale, in StandardRaycastWeaponShotVisualsData shotData)
        {
            if (!shotVisuals.HasInitialized)
            {
                shotVisuals.StartTime = ElapsedTime;
                
                // Scale
                shotVisuals.StartingScale = new float3(shotVisuals.Width, shotVisuals.Width, math.length(shotData.VisualOriginToHit));
                
                // Orientation
                localTransform.Rotation = quaternion.LookRotationSafe(math.normalizesafe(shotData.VisualOriginToHit), shotData.SimulationUp);
                    
                // Hit VFX
                if (shotData.Hit.Entity != Entity.Null)
                {
                    Entity hitVisualsEntity = ECB.Instantiate(shotVisuals.HitVisualsPrefab);
                    ECB.SetComponent(hitVisualsEntity, LocalTransform.FromPositionRotation(shotData.Hit.Position, quaternion.LookRotationSafe(shotData.Hit.SurfaceNormal, math.up())));
                }

                shotVisuals.HasInitialized = true;
            }
            
            if (shotVisuals.LifeTime > 0f)
            {
                float timeRatio = (ElapsedTime - shotVisuals.StartTime) / shotVisuals.LifeTime;
                float clampedTimeRatio = math.clamp(timeRatio, 0f, 1f);
                float invTimeRatio = 1f - clampedTimeRatio;

                postTransformScale.Value = float3x3.Scale(shotVisuals.StartingScale.x * invTimeRatio, shotVisuals.StartingScale.y * invTimeRatio, shotVisuals.StartingScale.z);

                if (timeRatio >= 1f)
                {
                    ECB.DestroyEntity(entity);
                }
            }
            else
            {
                ECB.DestroyEntity(entity);
            }
        }
    }
}
