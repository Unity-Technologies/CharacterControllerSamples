using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Logging;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;

namespace OnlineFPS
{
    public class GameSession
    {
        public Action OnAllDisconnected;
        public Action OnAllDestroyed;

        private World LocalWorld = null;
        private World ServerWorld = null;
        private World ClientWorld = null;
        private List<World> ThinClientWorlds = new List<World>();
        private World MainWorld = null;
        private List<World> GameWorlds = new List<World>();

        private bool _awaitingDisconnectAll;
        private int _worldsWithConnections;

        public static GameSession CreateLocalSession(string playerName, bool isGame)
        {
            GameSession newSession = new GameSession();
            newSession.CreateLocalWorld(playerName, out World localWorld);
            newSession.MainWorld = localWorld;
            GameManager.Instance.SetMenuState(isGame ? MenuState.InGame : MenuState.InMenu);
            return newSession;
        }

        public static GameSession CreateServerSession(string ip, ushort port, int thinClients)
        {
            GameSession newSession = new GameSession();
            newSession.CreateServerWorld(port, out World serverWorld);
            for (int i = 0; i < thinClients; i++)
            {
                newSession.CreateThinClientWorld(ip, port, i, out _);
            }

            newSession.MainWorld = serverWorld;
            GameManager.Instance.SetMenuState(MenuState.InGame);
            return newSession;
        }

        public static GameSession CreateClientServerSession(string ip, ushort port, int thinClients, string playerName,
            bool isSpectator)
        {
            GameSession newSession = new GameSession();
            newSession.CreateServerWorld(port, out World serverWorld);
            newSession.CreateClientWorld(ip, port, playerName, isSpectator, out World clientWorld);
            for (int i = 0; i < thinClients; i++)
            {
                newSession.CreateThinClientWorld(ip, port, i, out _);
            }

            newSession.MainWorld = clientWorld;
            GameManager.Instance.SetMenuState(MenuState.Connecting);
            return newSession;
        }

        public static GameSession CreateClientSession(string ip, ushort port, string playerName, bool isSpectator)
        {
#if UNITY_EDITOR
            MultiplayerPlayModePreferences.RequestedNumThinClients = 0;
#endif
            GameSession newSession = new GameSession();
            newSession.CreateClientWorld(ip, port, playerName, isSpectator, out World clientWorld);
            newSession.MainWorld = clientWorld;
            GameManager.Instance.SetMenuState(MenuState.Connecting);
            return newSession;
        }

        public bool IsMainWorld(World world)
        {
            return world == MainWorld;
        }

        public void DisconnectAll()
        {
            _awaitingDisconnectAll = true;

            int worldsCount = GameWorlds.Count;
            for (int i = worldsCount - 1; i >= 0; i--)
            {
                World tmpWorld = GameWorlds[i];
                if (tmpWorld != null && tmpWorld.IsCreated)
                {
                    Entity requestEntity = tmpWorld.EntityManager.CreateEntity();
                    tmpWorld.EntityManager.AddComponentData(requestEntity, new GameWorldSystem.RequestDisconnect());
                }
            }
        }

        public void DestroyAll()
        {
            int worldsCount = GameWorlds.Count;
            for (int i = worldsCount - 1; i >= 0; i--)
            {
                World tmpWorld = GameWorlds[i];
                if (tmpWorld != null && tmpWorld.IsCreated)
                {
                    tmpWorld.Dispose();
                }
            }

            OnAllDestroyed?.Invoke();
        }

        public void LoadSubsceneInAllGameWorlds(WeakObjectSceneReference subscene)
        {
            for (int i = 0; i < GameWorlds.Count; i++)
            {
                World tmpWorld = GameWorlds[i];
                if (tmpWorld != null && tmpWorld.IsCreated)
                {
                    SceneSystem.LoadSceneAsync(tmpWorld.Unmanaged, subscene.Id.GlobalId.AssetGUID);
                }
            }
        }

        public void CreateSubscenesLoadRequest()
        {
            // Create a scene load request in all game worlds
            int worldsCount = GameWorlds.Count;
            for (int i = worldsCount - 1; i >= 0; i--)
            {
                World tmpWorld = GameWorlds[i];
                if (tmpWorld != null && tmpWorld.IsCreated)
                {
                    // Find already-created subscene entities and add them to scene load requests
                    EntityQuery subscenesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<SceneReference>()
                        .Build(tmpWorld.EntityManager);
                    NativeList<SceneIdentifier> sceneIdentifiers = new NativeList<SceneIdentifier>(Allocator.Temp);
                    NativeArray<Entity> subsceneEntities = subscenesQuery.ToEntityArray(Allocator.Temp);

                    for (int j = 0; j < subsceneEntities.Length; j++)
                    {
                        sceneIdentifiers.Add(new SceneIdentifier(subsceneEntities[j]));
                    }

                    SceneLoadRequestSystem.CreateSceneLoadRequest(tmpWorld.EntityManager, sceneIdentifiers);

                    sceneIdentifiers.Dispose();
                    subsceneEntities.Dispose();
                }
            }
        }

        public void Update()
        {
            if (_awaitingDisconnectAll && _worldsWithConnections <= 0)
            {
                OnAllDisconnected?.Invoke();
                _awaitingDisconnectAll = false;
            }
        }

        public void OnConnectionSuccess(World world)
        {
            if (world != ServerWorld && IsMainWorld(world))
            {
                GameManager.Instance.SetMenuState(MenuState.InGame);
            }

            CalculateWorldsWithConnections();
        }

        public void OnWorldDisconnect(World world)
        {
            CalculateWorldsWithConnections();

            if (IsMainWorld(world))
            {
                OnAllDisconnected -= DestroyAll;
                OnAllDisconnected += DestroyAll;
                OnAllDestroyed -= GameManager.Instance.EndSessionAndReturnToMenu;
                OnAllDestroyed += GameManager.Instance.EndSessionAndReturnToMenu;
                DisconnectAll();
            }
        }

        private void CalculateWorldsWithConnections()
        {
            _worldsWithConnections = 0;
            int worldsCount = GameWorlds.Count;
            for (int i = worldsCount - 1; i >= 0; i--)
            {
                World tmpWorld = GameWorlds[i];
                if (tmpWorld != null && tmpWorld.IsCreated)
                {
                    EntityQuery netIDQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkId>()
                        .Build(tmpWorld.EntityManager);
                    if (netIDQuery.CalculateEntityCount() > 0)
                    {
                        _worldsWithConnections++;
                    }
                }
            }
        }

        private void InitGameWorldCommon(World world, string playerName)
        {
            // Game world singleton
            Entity gameWorldSystemSingletonEntity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(gameWorldSystemSingletonEntity, new GameWorldSystem.Singleton
            {
                PlayerName = playerName,
            });

            // Game session link
            world.EntityManager.AddComponentObject(gameWorldSystemSingletonEntity, new GameSessionLink
            {
                GameSession = this,
            });

            GameWorlds.Add(world);
        }

        private void CreateLocalWorld(string playerName, out World world)
        {
            world = ClientServerBootstrap.CreateLocalWorld("LocalWorld");
            LocalWorld = world;
            InitGameWorldCommon(world, playerName);
        }

        private void CreateServerWorld(ushort port, out World world)
        {
            GetServerNetworkEndpoint(port, out NetworkEndpoint endpoint);

            world = ClientServerBootstrap.CreateServerWorld("ServerWorld");

            EntityQuery clientSingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ServerGameSystem.Singleton>().Build(world.EntityManager);
            ref ServerGameSystem.Singleton serverSingleton =
                ref clientSingletonQuery.GetSingletonRW<ServerGameSystem.Singleton>().ValueRW;
            serverSingleton.ListenEndPoint = endpoint;

            ServerWorld = world;
            InitGameWorldCommon(world, "Server");
        }

        private bool CreateClientWorld(string ip, ushort port, string playerName, bool isSpectator, out World world)
        {
            if (GetJoinNetworkEndpoint(ip, port, out NetworkEndpoint endpoint))
            {
                world = ClientServerBootstrap.CreateClientWorld("ClientWorld");

                EntityQuery clientSingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ClientGameSystem.Singleton>().Build(world.EntityManager);
                ref ClientGameSystem.Singleton clientSingleton =
                    ref clientSingletonQuery.GetSingletonRW<ClientGameSystem.Singleton>().ValueRW;
                clientSingleton.ConnectEndPoint = endpoint;
                clientSingleton.IsSpectator = isSpectator;

                ClientWorld = world;
                InitGameWorldCommon(world, playerName);

                return true;
            }
            else
            {
                world = null;
                return false;
            }
        }

        private bool CreateThinClientWorld(string ip, ushort port, int thinClientId, out World world)
        {
            if (GetJoinNetworkEndpoint(ip, port, out NetworkEndpoint endpoint))
            {
                world = ClientServerBootstrap.CreateThinClientWorld();
                string playerName = $"ThinClient_{thinClientId}";

                EntityQuery clientSingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ClientGameSystem.Singleton>().Build(world.EntityManager);
                ref ClientGameSystem.Singleton clientSingleton =
                    ref clientSingletonQuery.GetSingletonRW<ClientGameSystem.Singleton>().ValueRW;
                clientSingleton.ConnectEndPoint = endpoint;
                clientSingleton.IsSpectator = false;

                ThinClientWorlds.Add(world);
                InitGameWorldCommon(world, playerName);

                return true;
            }
            else
            {
                world = null;
                return false;
            }
        }

        private bool GetJoinNetworkEndpoint(string ip, ushort port, out NetworkEndpoint endpoint)
        {
            endpoint = default;
            if (NetworkEndpoint.TryParse(ip, port, out endpoint))
            {
                return true;
            }
            else
            {
                Log.Error("Error: couldn't create valid network endpoint");
            }

            return false;
        }

        private void GetServerNetworkEndpoint(ushort port, out NetworkEndpoint endpoint)
        {
            endpoint = NetworkEndpoint.AnyIpv4;
            endpoint.Port = port;
        }
    }
}