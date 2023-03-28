using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[BurstCompile]
public partial struct ServerGameSystem : ISystem
{
    public struct Singleton : IComponentData
    {
        public bool RequestingDisconnect;

        public Unity.Mathematics.Random Random;
        public bool AcceptJoins;

        public int DisconnectionFramesCounter;
    }

    public struct AcceptJoinsOnceScenesLoadedRequest : IComponentData
    {
        public Entity PendingSceneLoadRequest;
    }

    public struct PendingClient : IComponentData
    {
        public float TimeConnected;
        public bool IsJoining;
    }

    public struct JoinedClient : IComponentData
    {
        public Entity PlayerEntity;
    }

    public struct JoinRequestAccepted : IRpcCommand
    {
    }

    public struct ClientOwnedEntities : ICleanupBufferElementData
    {
        public Entity Entity;
    }

    public struct DisconnectRequest : IComponentData
    {
    }

    public struct CharacterSpawnRequest : IComponentData
    {
        public Entity ForConnection;
        public float Delay;
    }

    private EntityQuery _singletonQuery;
    private EntityQuery _joinRequestQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameResources>();

        _singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<Singleton>().Build(state.EntityManager);
        _joinRequestQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<ClientGameSystem.JoinRequest, ReceiveRpcCommandRequest>().Build(ref state);
        
        // Auto-create singleton
        uint randomSeed = (uint)DateTime.Now.Millisecond;
        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new Singleton
        {
            Random = Unity.Mathematics.Random.CreateFromIndex(randomSeed),
        });
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ref Singleton singleton = ref _singletonQuery.GetSingletonRW<Singleton>().ValueRW;
        GameResources gameResources = SystemAPI.GetSingleton<GameResources>();

        HandleAcceptJoinsOncePendingScenesAreLoaded(ref state, ref singleton);
        HandleJoinRequests(ref state, ref singleton, gameResources);
        HandlePendingJoinClientTimeout(ref state, ref singleton, gameResources);
        HandleDisconnect(ref state, ref singleton);
        HandleSpawnCharacter(ref state, ref singleton, gameResources);
    }

    private void HandleAcceptJoinsOncePendingScenesAreLoaded(ref SystemState state, ref Singleton singleton)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, entity) in SystemAPI.Query<AcceptJoinsOnceScenesLoadedRequest>().WithEntityAccess())
        {
            if (SystemAPI.HasComponent<SceneLoadRequest>(request.PendingSceneLoadRequest) && SystemAPI.GetComponent<SceneLoadRequest>(request.PendingSceneLoadRequest).IsLoaded)
            {
                singleton.AcceptJoins = true;
                ecb.DestroyEntity(request.PendingSceneLoadRequest);
                ecb.DestroyEntity(entity);
            }
        }
    }

    private void HandlePendingJoinClientTimeout(ref SystemState state, ref Singleton singleton, GameResources gameResources)
    {
        // Add ConnectionState component
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (netId, entity) in SystemAPI.Query<NetworkId>().WithNone<ConnectionState>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new ConnectionState());
            }
        }

        // Mark unjoined clients as pending
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (netId, entity) in SystemAPI.Query<NetworkId>().WithNone<PendingClient>().WithNone<JoinedClient>().WithEntityAccess())
            {
                ecb.AddComponent(entity, new PendingClient());
            }
        }

        // Handle join timeout for pending clients
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (netId, pendingCLient, entity) in SystemAPI.Query<NetworkId, RefRW<PendingClient>>().WithEntityAccess())
            {
                pendingCLient.ValueRW.TimeConnected += SystemAPI.Time.DeltaTime;
                if (pendingCLient.ValueRW.TimeConnected > gameResources.JoinTimeout)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }

    private void HandleJoinRequests(ref SystemState state, ref Singleton singleton, GameResources gameResources)
    {
        if (singleton.AcceptJoins && _joinRequestQuery.CalculateEntityCount() > 0)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process join requests
            foreach (var (request, rpcReceive, entity) in SystemAPI.Query<ClientGameSystem.JoinRequest, ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<NetworkId>(rpcReceive.SourceConnection) &&
                    !SystemAPI.HasComponent<JoinedClient>(rpcReceive.SourceConnection))
                {
                    int ownerConnectionId = SystemAPI.GetComponent<NetworkId>(rpcReceive.SourceConnection).Value;

                    // Mark connection as joined
                    ecb.RemoveComponent<PendingClient>(rpcReceive.SourceConnection);
                    ecb.AddBuffer<ClientOwnedEntities>(rpcReceive.SourceConnection);

                    Entity playerEntity = Entity.Null;
                    // Spawn player
                    playerEntity = ecb.Instantiate(gameResources.PlayerGhost);
                    ecb.SetComponent(playerEntity, new GhostOwner { NetworkId = ownerConnectionId });
                    ecb.AppendToBuffer(rpcReceive.SourceConnection, new ClientOwnedEntities { Entity = playerEntity });
                    
                    // Set player data
                    FirstPersonPlayer player = SystemAPI.GetComponent<FirstPersonPlayer>(gameResources.PlayerGhost);
                    player.Name = request.PlayerName;
                    ecb.SetComponent(playerEntity, player);

                    if (!request.Spectator)
                    { 
                        // Request to spawn character
                        Entity spawnCharacterRequestEntity = ecb.CreateEntity();
                        ecb.AddComponent(spawnCharacterRequestEntity, new CharacterSpawnRequest { ForConnection = rpcReceive.SourceConnection, Delay = -1f });
                    }

                    // Remember player for connection
                    ecb.AddComponent(rpcReceive.SourceConnection, new JoinedClient { PlayerEntity = playerEntity });

                    // Accept join request
                    Entity joinRequestAcceptedEntity = state.EntityManager.CreateEntity();
                    ecb.AddComponent(joinRequestAcceptedEntity, new JoinRequestAccepted());
                    ecb.AddComponent(joinRequestAcceptedEntity, new SendRpcCommandRequest{ TargetConnection = rpcReceive.SourceConnection });

                    // Stream in game
                    ecb.AddComponent(rpcReceive.SourceConnection, new NetworkStreamInGame());
                }

                ecb.DestroyEntity(entity);
            }
        }
    }

    private void HandleDisconnect(ref SystemState state, ref Singleton singleton)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

        // Client disconnect
        foreach (var (connectionState, ownedEntities, entity) in SystemAPI.Query<ConnectionState, DynamicBuffer<ClientOwnedEntities>>().WithEntityAccess())
        {
            if (connectionState.CurrentState == ConnectionState.State.Disconnected)
            {
                // Destroy all entities owned by client
                for (int i = 0; i < ownedEntities.Length; i++)
                {
                    ecb.DestroyEntity(ownedEntities[i].Entity);
                }

                ecb.RemoveComponent<ClientOwnedEntities>(entity);
                ecb.RemoveComponent<ConnectionState>(entity);
            }
        }

        // Disconnect requests
        EntityQuery disconnectRequestQuery = SystemAPI.QueryBuilder().WithAll<DisconnectRequest>().Build();
        if (disconnectRequestQuery.CalculateEntityCount() > 0)
        {
            // Make sure all renderEnvironments are disposed before initiating disconnect
            EntityQuery renderEnvironmentsQuery = SystemAPI.QueryBuilder().WithAll<RenderEnvironment>().Build();
            if (renderEnvironmentsQuery.CalculateEntityCount() > 0)
            {
                state.EntityManager.DestroyEntity(renderEnvironmentsQuery);
                singleton.DisconnectionFramesCounter = 0;
            }

            // Allow systems to have updated since disconnection, for cleanup
            if (singleton.DisconnectionFramesCounter > 3)
            {
                Entity disposeRequestEntity = ecb.CreateEntity();
                ecb.AddComponent(disposeRequestEntity, new GameManagementSystem.DisposeServerWorldRequest());
                ecb.AddComponent(disposeRequestEntity, new MoveToLocalWorld());
                ecb.DestroyEntity(disconnectRequestQuery);
            }

            singleton.DisconnectionFramesCounter++;
        }
    }

    private void HandleSpawnCharacter(ref SystemState state, ref Singleton singleton, GameResources gameResources)
    {
        if (SystemAPI.QueryBuilder().WithAll<CharacterSpawnRequest>().Build().CalculateEntityCount() > 0)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
            EntityQuery spawnPointsQuery = SystemAPI.QueryBuilder().WithAll<SpawnPoint, LocalToWorld>().Build();
            NativeArray<LocalToWorld> spawnPointLtWs = spawnPointsQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRW<CharacterSpawnRequest>>().WithEntityAccess())
            {
                if (spawnRequest.ValueRW.Delay > 0f)
                {
                    spawnRequest.ValueRW.Delay -= SystemAPI.Time.DeltaTime;
                }
                else
                {
                    if (SystemAPI.HasComponent<NetworkId>(spawnRequest.ValueRW.ForConnection) &&
                        SystemAPI.HasComponent<JoinedClient>(spawnRequest.ValueRW.ForConnection))
                    {
                        int connectionId = SystemAPI.GetComponent<NetworkId>(spawnRequest.ValueRW.ForConnection).Value;
                        Entity playerEntity = SystemAPI.GetComponent<JoinedClient>(spawnRequest.ValueRW.ForConnection).PlayerEntity;
                        float3 randomSpawnPosition = spawnPointLtWs[singleton.Random.NextInt(0, spawnPointLtWs.Length - 1)].Position;

                        // Spawn character
                        Entity characterEntity = ecb.Instantiate(gameResources.CharacterGhost);
                        ecb.SetComponent(characterEntity, new GhostOwner { NetworkId = connectionId });
                        ecb.SetComponent(characterEntity, LocalTransform.FromPosition(randomSpawnPosition));
                        ecb.SetComponent(characterEntity, new OwningPlayer { Entity = playerEntity });
                        ecb.AppendToBuffer(spawnRequest.ValueRW.ForConnection, new ClientOwnedEntities { Entity = characterEntity });

                        // Assign character to player
                        FirstPersonPlayer player = SystemAPI.GetComponent<FirstPersonPlayer>(playerEntity);
                        player.ControlledCharacter = characterEntity;
                        ecb.SetComponent(playerEntity, player);

                        // Spawn & assign starting weapon
                        Entity randomWeaponPrefab = default;
                        switch (singleton.Random.NextInt(0, 2))
                        {
                            case 0:
                                randomWeaponPrefab = gameResources.MachineGunGhost;
                                break;
                            case 1:
                                randomWeaponPrefab = gameResources.RailgunGhost;
                                break;
                        }
                        Entity weaponEntity = ecb.Instantiate(randomWeaponPrefab);
                        ecb.SetComponent(weaponEntity, new GhostOwner { NetworkId = connectionId });
                        ecb.SetComponent(characterEntity, new ActiveWeapon { Entity = weaponEntity });
                    }

                    ecb.DestroyEntity(entity);
                }
            }

            spawnPointLtWs.Dispose();
        }
    }
}
