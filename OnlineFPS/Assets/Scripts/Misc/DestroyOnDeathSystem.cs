using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct DestroyOnDeathSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, FirstPersonCharacterComponent>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DestroyOnDeathJob job = new DestroyOnDeathJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct DestroyOnDeathJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        
        void Execute(Entity entity, in Health health)
        {
            if (health.IsDead())
            {
                ECB.DestroyEntity(entity);
            }
        }
    }
}
