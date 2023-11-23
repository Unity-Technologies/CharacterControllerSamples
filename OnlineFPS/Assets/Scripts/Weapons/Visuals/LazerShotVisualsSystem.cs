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
public partial struct LazerShotVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<LazerShotVisuals, LocalTransform, PostTransformMatrix, StandardRaycastWeaponShotVisualsData>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        LazerShotVisualsJob job = new LazerShotVisualsJob
        {
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct LazerShotVisualsJob : IJobEntity
    {
        public float ElapsedTime;
        public EntityCommandBuffer ECB;
        
        void Execute(Entity entity, ref LazerShotVisuals shotVisuals, ref LocalTransform localTransform, ref LocalToWorld ltw, in StandardRaycastWeaponShotVisualsData shotData)
        {
            if (!shotVisuals.HasInitialized)
            {
                shotVisuals.StartTime = ElapsedTime;
                
                // Scale
                shotVisuals.StartingScale = new float3(shotVisuals.Width, shotVisuals.Width, shotData.GetLength());
                    
                // Hit VFX
                if (shotData.DidHit == 1)
                {
                    Entity hitVisualsEntity = ECB.Instantiate(shotVisuals.HitVisualsPrefab);
                    ECB.SetComponent(hitVisualsEntity, LocalTransform.FromPositionRotation(shotData.EndPoint, quaternion.LookRotationSafe(shotData.HitNormal, math.up())));
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

                // Calculating the LocalToWorld manually since this is happening after the transforms group update
                ltw.Value = float4x4.TRS(localTransform.Position, localTransform.Rotation, new float3(shotVisuals.StartingScale.x * invTimeRatio, shotVisuals.StartingScale.y * invTimeRatio, shotVisuals.StartingScale.z));
            }
            else
            {
                ECB.DestroyEntity(entity);
            }
        }
    }
}
