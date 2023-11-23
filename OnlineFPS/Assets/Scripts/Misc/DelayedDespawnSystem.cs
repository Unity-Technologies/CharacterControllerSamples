using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct DelayedDespawnSystem : ISystem
{
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {
        DelayedDespawnJob job = new DelayedDespawnJob
        {
            IsServer = state.WorldUnmanaged.IsServer(),
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            ChildBufferLookup = SystemAPI.GetBufferLookup<Child>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct DelayedDespawnJob : IJobEntity
    {
        public bool IsServer;
        public float DeltaTime;
        public EntityCommandBuffer ECB;
        [ReadOnly] public BufferLookup<Child> ChildBufferLookup;
        
        void Execute(Entity entity, ref DelayedDespawn delayedDespawn)
        {
            if (IsServer)
            {
                delayedDespawn.Timer -= DeltaTime;
                if (delayedDespawn.Timer <= 0f)
                {
                    ECB.DestroyEntity(entity);
                }
            }
            // If client...
            else if (delayedDespawn.HasDisabledRendering == 0)
            {
                MiscUtilities.DisableRenderingInHierarchy(ECB, entity, ref ChildBufferLookup);
                delayedDespawn.HasDisabledRendering = 1;
            }
        }
    }
}
