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
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectilePredictionUpdateGroup))]
[BurstCompile]
public partial struct CharacterDeathServerSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, FirstPersonCharacterComponent>().Build());
        state.RequireForUpdate<ServerGameSystem.Singleton>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CharacterDeathServerJob serverJob = new CharacterDeathServerJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            RespawnTime = SystemAPI.GetSingleton<GameResources>().RespawnTime,
            DespawnTime = SystemAPI.GetSingleton<GameResources>().PolledEventsTimeout,
            ConnectionEntityMap = SystemAPI.GetSingletonRW<ServerGameSystem.Singleton>().ValueRO.ConnectionEntityMap,
        };
        state.Dependency = serverJob.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    [WithNone(typeof(DelayedDespawn))]
    public partial struct CharacterDeathServerJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public float RespawnTime;
        public float DespawnTime;
        [ReadOnly]
        public NativeHashMap<int, Entity> ConnectionEntityMap;

        void Execute(Entity entity, in FirstPersonCharacterComponent character, in Health health, in GhostOwner ghostOwner)
        {
            if (health.IsDead())
            {
                if(ConnectionEntityMap.TryGetValue(ghostOwner.NetworkId, out Entity owningConnectionEntity) && owningConnectionEntity != Entity.Null)
                {
                    // Send character's owning client a message to start respawn countdown
                    Entity respawnScreenRequestEntity = ECB.CreateEntity();
                    ECB.AddComponent(respawnScreenRequestEntity, new RespawnMessageRequest { Start = true, CountdownTime = RespawnTime });
                    ECB.AddComponent(respawnScreenRequestEntity, new SendRpcCommandRequest { TargetConnection = owningConnectionEntity });

                    // Request to spawn character for the owning client
                    Entity spawnCharacterRequestEntity = ECB.CreateEntity();
                    ECB.AddComponent(spawnCharacterRequestEntity, new ServerGameSystem.CharacterSpawnRequest { ForConnection = owningConnectionEntity, Delay = RespawnTime });
                }
                
                // Activate delayed despawn
                ECB.AddComponent(entity, new DelayedDespawn
                {
                    Timer = DespawnTime,
                });
            }
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct CharacterDeathClientSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Health, FirstPersonCharacterComponent>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CharacterDeathClientJob job = new CharacterDeathClientJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct CharacterDeathClientJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;

        void Execute(Entity entity, ref FirstPersonCharacterComponent character, in Health health)
        {
            if (health.IsDead() && character.HasProcessedDeath == 0)
            {
                ECB.AddComponent(entity, new DelayedDespawn());

                if (LocalToWorldLookup.TryGetComponent(character.DeathVFXSpawnPoint, out LocalToWorld deathVFXLtW))
                {
                    Entity deathVFXEntity = ECB.Instantiate(character.DeathVFX);
                    ECB.SetComponent(deathVFXEntity, LocalTransform.FromPositionRotation(deathVFXLtW.Position, deathVFXLtW.Rotation));
                }

                character.HasProcessedDeath = 1;
            }
        }
    }
}
