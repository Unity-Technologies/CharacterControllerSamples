using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;

[Serializable]
public struct LocalGameData : IComponentData
{
    public FixedString128Bytes LocalPlayerName;
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GameManagementSystem : SystemBase
{
    public World ClientWorld;
    public World ServerWorld;
    
    public const string LocalHost = "127.0.0.1";
    
    [Serializable]
    public struct JoinRequest : IComponentData
    {
        public FixedString128Bytes LocalPlayerName;
        public NetworkEndpoint EndPoint;
        public bool Spectator;
    }
    
    [Serializable]
    public struct HostRequest : IComponentData
    {
        public NetworkEndpoint EndPoint;
    }
    
    [Serializable]
    public struct DisconnectRequest : IComponentData
    { }
    
    [Serializable]
    public struct DisposeClientWorldRequest : IComponentData
    { }
    
    [Serializable]
    public struct DisposeServerWorldRequest : IComponentData
    { }
    
    [Serializable]
    public struct Singleton : IComponentData
    {
        public MenuState MenuState;
        public Entity MenuVisualsSceneInstance;
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        
        // Auto-create singleton
        EntityManager.CreateEntity(typeof(Singleton));
        
        RequireForUpdate<GameResources>();
        RequireForUpdate<Singleton>();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        
        // Start a tmp server just once so we can get a firewall prompt when running the game for the first time
        {
            NetworkDriver tmpNetDriver = NetworkDriver.Create();
            NetworkEndpoint tmpEndPoint = NetworkEndpoint.Parse(LocalHost, 7777);
            if (tmpNetDriver.Bind(tmpEndPoint) == 0)
            {
                tmpNetDriver.Listen();
            }
            tmpNetDriver.Dispose();
        }
    }

    protected override void OnUpdate()
    {
        ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(World.Unmanaged);
        GameResources gameResources = SystemAPI.GetSingleton<GameResources>();
        
        ProcessHostRequests(ref singleton, ref ecb, gameResources);
        ProcessJoinRequests(ref singleton, ref ecb, gameResources);
        ProcessDisconnectRequests(ref singleton, ref ecb);
        HandleMenuState(ref singleton);
        HandleDisposeClientServerWorldsAndReturnToMenu(ref singleton, ref ecb);
    }

    private void ProcessHostRequests(ref Singleton singleton, ref EntityCommandBuffer ecb, GameResources gameResources)
    {
        EntityCommandBuffer serverECB = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (request, entity) in SystemAPI.Query<RefRO<HostRequest>>().WithEntityAccess())
        {
            if (!WorldUtilities.IsValidAndCreated(ServerWorld))
            {
                // Create server world
                ServerWorld = NetCodeBootstrap.CreateServerWorld("ServerWorld");
                
                // Tickrate
                Entity tickRateEntity = serverECB.CreateEntity();
                serverECB.AddComponent(tickRateEntity, gameResources.GetClientServerTickRate());

                // Listen to endpoint
                EntityQuery serverNetworkDriverQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<NetworkStreamDriver>().Build(ServerWorld.EntityManager);
                serverNetworkDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(request.ValueRO.EndPoint);

                // Load game resources subscene
                SceneSystem.LoadSceneAsync(ServerWorld.Unmanaged, gameResources.GameResourcesScene);
                
                // Create a request to accept joins once the game scenes have been loaded
                {
                    EntityQuery serverGameSingletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<ServerGameSystem.Singleton>().Build(ServerWorld.EntityManager);
                    ref ServerGameSystem.Singleton serverGameSingleton = ref serverGameSingletonQuery.GetSingletonRW<ServerGameSystem.Singleton>().ValueRW;
                    serverGameSingleton.AcceptJoins = false;

                    Entity requestAcceptJoinsEntity = serverECB.CreateEntity();
                    serverECB.AddComponent(requestAcceptJoinsEntity, new ServerGameSystem.AcceptJoinsOnceScenesLoadedRequest
                    {
                        PendingSceneLoadRequest = SceneLoadRequestSystem.CreateSceneLoadRequest(serverECB, gameResources.GameScene),
                    });
                }
            }
            
            ecb.DestroyEntity(entity);
            break;
        }

        if (WorldUtilities.IsValidAndCreated(ServerWorld))
        {
            serverECB.Playback(ServerWorld.EntityManager);
        }
        serverECB.Dispose();
    }

    private void ProcessJoinRequests(ref Singleton singleton, ref EntityCommandBuffer ecb, GameResources gameResources)
    {
        EntityCommandBuffer clientECB = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (request, entity) in SystemAPI.Query<RefRO<JoinRequest>>().WithEntityAccess())
        {
            if (!WorldUtilities.IsValidAndCreated(ClientWorld))
            {
                // Create client world
                ClientWorld = NetCodeBootstrap.CreateClientWorld("ClientWorld");

                // Tickrate
                Entity tickRateEntity = clientECB.CreateEntity();
                clientECB.AddComponent(tickRateEntity, gameResources.GetClientServerTickRate());
                
                // Connect to endpoint
                EntityQuery clientNetworkDriverQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<NetworkStreamDriver>().Build(ClientWorld.EntityManager);
                clientNetworkDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(ClientWorld.EntityManager, request.ValueRO.EndPoint);

                // Create local game data singleton in client world
                Entity localGameDataEntity = ClientWorld.EntityManager.CreateEntity();
                ClientWorld.EntityManager.AddComponentData(localGameDataEntity, new LocalGameData
                {
                    LocalPlayerName = request.ValueRO.LocalPlayerName,
                });
                
                // Load game resources subscene
                SceneSystem.LoadSceneAsync(ClientWorld.Unmanaged, gameResources.GameResourcesScene);
                
                // Create a request to join once the game scenes have been loaded
                {
                    EntityQuery clientGameSingletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<ClientGameSystem.Singleton>().Build(ClientWorld.EntityManager);
                    ref ClientGameSystem.Singleton clientGameSingleton = ref clientGameSingletonQuery.GetSingletonRW<ClientGameSystem.Singleton>().ValueRW;
                    clientGameSingleton.Spectator = request.ValueRO.Spectator;
                    
                    Entity requestAcceptJoinsEntity = clientECB.CreateEntity();
                    clientECB.AddComponent(requestAcceptJoinsEntity, new ClientGameSystem.JoinOnceScenesLoadedRequest
                    {
                        PendingSceneLoadRequest = SceneLoadRequestSystem.CreateSceneLoadRequest(clientECB, gameResources.GameScene),
                    });
                }
            }

            ecb.DestroyEntity(entity);
            break;
        }

        if (WorldUtilities.IsValidAndCreated(ClientWorld))
        {
            clientECB.Playback(ClientWorld.EntityManager);
        }
        clientECB.Dispose();
    }

    private void ProcessDisconnectRequests(ref Singleton singleton, ref EntityCommandBuffer ecb)
    {
        EntityQuery disconnectRequestQuery = SystemAPI.QueryBuilder().WithAll<DisconnectRequest>().Build();
        if (disconnectRequestQuery.CalculateEntityCount() > 0)
        {
            if (WorldUtilities.IsValidAndCreated(ClientWorld))
            {
                Entity disconnectClientRequestEntity = ecb.CreateEntity();
                ecb.AddComponent(disconnectClientRequestEntity, new ClientGameSystem.DisconnectRequest());
                ecb.AddComponent(disconnectClientRequestEntity, new MoveToClientWorld());
            }

            if (WorldUtilities.IsValidAndCreated(ServerWorld))
            {
                Entity disconnectServerRequestEntity = ecb.CreateEntity();
                ecb.AddComponent(disconnectServerRequestEntity, new ServerGameSystem.DisconnectRequest());
                ecb.AddComponent(disconnectServerRequestEntity, new MoveToServerWorld());
            }
        }
        ecb.DestroyEntity(disconnectRequestQuery);
    }

    private void HandleDisposeClientServerWorldsAndReturnToMenu(ref Singleton singleton, ref EntityCommandBuffer ecb)
    {
        EntityQuery disposeClientRequestQuery = SystemAPI.QueryBuilder().WithAll<DisposeClientWorldRequest>().Build();
        if (disposeClientRequestQuery.CalculateEntityCount() > 0)
        {
            if (WorldUtilities.IsValidAndCreated(ClientWorld))
            {
                ClientWorld.Dispose();
            }
            
            EntityManager.DestroyEntity(disposeClientRequestQuery);
        }
        
        EntityQuery disposeServerRequestQuery = SystemAPI.QueryBuilder().WithAll<DisposeServerWorldRequest>().Build();
        if (disposeServerRequestQuery.CalculateEntityCount() > 0)
        {
            if (WorldUtilities.IsValidAndCreated(ServerWorld))
            {
                ServerWorld.Dispose();
            }
            
            EntityManager.DestroyEntity(disposeServerRequestQuery);
        }
    }

    private void HandleMenuState(ref Singleton singleton)
    {
        // Detect state changes
        {
            if (WorldUtilities.IsValidAndCreated(ClientWorld))
            {
                EntityQuery connectionInGameQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkId, NetworkStreamInGame>().Build(ClientWorld.EntityManager);
                if (connectionInGameQuery.CalculateEntityCount() == 0)
                {
                    singleton.MenuState = MenuState.Connecting;
                }
                else
                {
                    singleton.MenuState = MenuState.InGame;
                }
            }
            else if (WorldUtilities.IsValidAndCreated(ServerWorld))
            {
                EntityQuery serverGameSingletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<ServerGameSystem.Singleton>().Build(ServerWorld.EntityManager);
                ServerGameSystem.Singleton serverGameSingleton = serverGameSingletonQuery.GetSingleton<ServerGameSystem.Singleton>();
                if (serverGameSingleton.AcceptJoins)
                {
                    singleton.MenuState = MenuState.InGame;
                }
                else
                {
                    singleton.MenuState = MenuState.Connecting;
                }
            }
            else
            {
                singleton.MenuState = MenuState.InMenu;
            }
        }
        
        // Handle state update
        if (singleton.MenuState == MenuState.InMenu)
        {
            // load menu scene if it doesn't exist
            if (!SystemAPI.HasComponent<SceneReference>(singleton.MenuVisualsSceneInstance))
            {
                singleton.MenuVisualsSceneInstance = SceneSystem.LoadSceneAsync(World.Unmanaged, SystemAPI.GetSingleton<GameResources>().MenuVisualsScene);
            }
        }
        else
        {
            // unload menu scene if it exists
            if (SystemAPI.HasComponent<SceneReference>(singleton.MenuVisualsSceneInstance))
            {
                SceneSystem.UnloadScene(World.Unmanaged, singleton.MenuVisualsSceneInstance, SceneSystem.UnloadParameters.DestroyMetaEntities);
            }
        }
    }
}