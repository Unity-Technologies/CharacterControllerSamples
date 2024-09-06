using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace OnlineFPS
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct DelayedDespawnSystem : ISystem
    {
        [BurstCompile]
        private void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameResources>();
        }

        [BurstCompile]
        private void OnUpdate(ref SystemState state)
        {
            GameResources gameResources = SystemAPI.GetSingleton<GameResources>();

            DelayedDespawnJob job = new DelayedDespawnJob
            {
                IsServer = state.WorldUnmanaged.IsServer(),
                DespawnTicks = gameResources.DespawnTicks,
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                ChildBufferLookup = SystemAPI.GetBufferLookup<Child>(true),
                PhysicsColliderLookup = SystemAPI.GetComponentLookup<PhysicsCollider>(false),
            };
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        public unsafe partial struct DelayedDespawnJob : IJobEntity
        {
            public bool IsServer;
            public uint DespawnTicks;
            public EntityCommandBuffer ECB;
            [ReadOnly] public BufferLookup<Child> ChildBufferLookup;
            public ComponentLookup<PhysicsCollider> PhysicsColliderLookup;

            void Execute(Entity entity, ref DelayedDespawn delayedDespawn)
            {
                if (IsServer)
                {
                    delayedDespawn.Ticks++;
                    if (delayedDespawn.Ticks > DespawnTicks)
                    {
                        ECB.DestroyEntity(entity);
                    }
                }

                if (delayedDespawn.HasHandledPreDespawn == 0)
                {
                    if (!IsServer)
                    {
                        MiscUtilities.DisableRenderingInHierarchy(ECB, entity, ref ChildBufferLookup);
                    }

                    // Disable collisions
                    if (PhysicsColliderLookup.TryGetComponent(entity, out PhysicsCollider physicsCollider))
                    {
                        ref Collider collider = ref *physicsCollider.ColliderPtr;
                        collider.SetCollisionResponse(CollisionResponsePolicy.None);
                    }

                    delayedDespawn.HasHandledPreDespawn = 1;
                }
            }
        }
    }
}