using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

[BurstCompile]
public partial struct PrefabThrowerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    { }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }
    
    public void OnUpdate(ref SystemState state)
    {
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            PrefabThrowerJob job = new PrefabThrowerJob
            {
                ECB = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            };
            job.Schedule();
        }
    }

    [BurstCompile]
    public partial struct PrefabThrowerJob : IJobEntity
    {
        public EntityCommandBuffer ECB;

        void Execute(ref PrefabThrower prefabThrower, in LocalToWorld localToWorld)
        {
            Entity spawnedEntity = ECB.Instantiate(prefabThrower.PrefabEntity);
            ECB.SetComponent(spawnedEntity, new LocalTransform { Position = localToWorld.Position, Rotation = quaternion.Euler(prefabThrower.InitialEulerAngles), Scale = 1f });
            ECB.SetComponent(spawnedEntity, new PhysicsVelocity { Linear = localToWorld.Forward * prefabThrower.ThrowForce });
        }
    }
}