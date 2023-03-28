using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct CharacterDeathServerSystem : ISystem
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
        MiscUtilities.GetConnectionsArrays(ref state, Allocator.TempJob, out NativeArray<Entity> connectionEntities, out NativeArray<NetworkId> connections);
        
        CharacterDeathServerJob serverJob = new CharacterDeathServerJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            GameResources = SystemAPI.GetSingleton<GameResources>(),
            ConnectionEntities = connectionEntities,
            Connections = connections,
        };
        state.Dependency = serverJob.Schedule(state.Dependency);

        connectionEntities.Dispose(state.Dependency);
        connections.Dispose(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct CharacterDeathServerJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public GameResources GameResources;
        public NativeArray<Entity> ConnectionEntities;
        public NativeArray<NetworkId> Connections;
        
        void Execute(Entity entity, in FirstPersonCharacterComponent character, in Health health, in GhostOwner ghostOwner)
        {
            if (health.IsDead())
            {
                // TODO: we could do something more efficient for this
                // Find the entity of the owning connection
                Entity owningConnectionEntity = Entity.Null;
                for (int i = 0; i < Connections.Length; i++)
                {
                    if (Connections[i].Value == ghostOwner.NetworkId)
                    {
                        owningConnectionEntity = ConnectionEntities[i];
                        break;
                    }
                }
                
                if (owningConnectionEntity != Entity.Null)
                {
                    // Send character's owning client a message to start respawn countdown
                    Entity respawnScreenRequestEntity = ECB.CreateEntity();
                    ECB.AddComponent(respawnScreenRequestEntity, new RespawnMessageRequest { Start = true, CountdownTime = GameResources.RespawnTime });
                    ECB.AddComponent(respawnScreenRequestEntity, new SendRpcCommandRequest { TargetConnection = owningConnectionEntity });

                    // Request to spawn character for the owning client
                    Entity spawnCharacterRequestEntity = ECB.CreateEntity();
                    ECB.AddComponent(spawnCharacterRequestEntity, new ServerGameSystem.CharacterSpawnRequest { ForConnection = owningConnectionEntity, Delay = GameResources.RespawnTime });
                }
                
                ECB.DestroyEntity(entity);
            }
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct CharacterDeathClientSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, FirstPersonCharacterComponent, CharacterClientCleanup>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CharacterDeathVFXSpawnPointJob job = new CharacterDeathVFXSpawnPointJob
        {
            LtWLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct CharacterDeathVFXSpawnPointJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LtWLookup;

        void Execute(ref CharacterClientCleanup characterCleanup, in FirstPersonCharacterComponent character)
        {
            if (LtWLookup.TryGetComponent(character.DeathVFXSpawnPoint, out LocalToWorld spawnPointLtW))
            {
                characterCleanup.DeathVFXSpawnWorldPosition = spawnPointLtW.Position;
            }
        }
    }
}
