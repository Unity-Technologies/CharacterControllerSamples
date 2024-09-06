using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Scenes;
using Unity.Transforms;


namespace OnlineFPS
{
    public struct CharacterSpawnRequest : IComponentData
    {
        public Entity ForConnection;
        public float Delay;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateBefore(typeof(GameWorldSystem))]
    [BurstCompile]
    public partial struct ServerGameSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NetworkEndpoint ListenEndPoint;

            public Unity.Mathematics.Random Random;
            public bool HasHandledListen;
            public bool AcceptJoins;

            public NativeHashMap<int, Entity> ConnectionEntityMap;
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

        private EntityQuery _joinRequestQuery;
        private EntityQuery _connectionsQuery;
        private NativeHashMap<int, Entity> _connectionEntityMap;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameResources>();
            state.RequireForUpdate<GameResourcesWeapon>();

            _joinRequestQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<JoinRequest, ReceiveRpcCommandRequest>().Build(ref state);
            _connectionsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkId>().Build(state.EntityManager);

            _connectionEntityMap = new NativeHashMap<int, Entity>(300, Allocator.Persistent);

            // Auto-create singleton
            uint randomSeed = (uint)DateTime.Now.Millisecond;
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton
            {
                Random = Unity.Mathematics.Random.CreateFromIndex(randomSeed),
                ConnectionEntityMap = this._connectionEntityMap,
            });
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_connectionEntityMap.IsCreated)
            {
                _connectionEntityMap.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            GameResources gameResources = SystemAPI.GetSingleton<GameResources>();

            if (SystemAPI.HasSingleton<DisableCharacterDynamicContacts>())
            {
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<DisableCharacterDynamicContacts>());
            }

            if (!SystemAPI.HasSingleton<ClientServerTickRate>())
            {
                state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(),
                    gameResources.ClientServerTickRate);
            }

            HandleListen(ref state, ref singleton);
            BuildConnectionEntityMap(ref state, ref singleton);
            HandleAcceptJoinsOncePendingScenesAreLoaded(ref state, ref singleton);
            HandleJoinRequests(ref state, ref singleton, gameResources);
            HandlePendingJoinClientTimeout(ref state, ref singleton, gameResources);
            HandleCharacters(ref state, ref singleton, gameResources);
        }

        private void HandleListen(ref SystemState state, ref Singleton singleton)
        {
            if (!singleton.HasHandledListen &&
                SystemAPI.TryGetSingletonRW(out RefRW<NetworkStreamDriver> netStreamDriver))
            {
                netStreamDriver.ValueRW.Listen(singleton.ListenEndPoint);
                singleton.HasHandledListen = true;
            }
        }

        private void BuildConnectionEntityMap(ref SystemState state, ref Singleton singleton)
        {
            NativeArray<Entity> connectionEntities = _connectionsQuery.ToEntityArray(state.WorldUpdateAllocator);
            NativeArray<NetworkId> connections =
                _connectionsQuery.ToComponentDataArray<NetworkId>(state.WorldUpdateAllocator);

            state.Dependency = new BuildConnectionEntityMapJob
            {
                ConnectionEntityMap = singleton.ConnectionEntityMap,
                Connections = connections,
                ConnectionEntities = connectionEntities,
            }.Schedule(state.Dependency);

            connectionEntities.Dispose(state.Dependency);
            connections.Dispose(state.Dependency);
        }

        private void HandleAcceptJoinsOncePendingScenesAreLoaded(ref SystemState state, ref Singleton singleton)
        {
            if (!singleton.AcceptJoins)
            {
                EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged);

                bool hasHadSceneLoadRequest = false;
                bool allSceneLoadRequestsComplete = true;
                foreach (var (request, entity) in SystemAPI.Query<SceneLoadRequest>().WithEntityAccess())
                {
                    hasHadSceneLoadRequest = true;
                    if (!request.IsLoaded)
                    {
                        allSceneLoadRequestsComplete = false;
                    }
                }

                if (hasHadSceneLoadRequest && allSceneLoadRequestsComplete)
                {
                    singleton.AcceptJoins = true;
                    foreach (var (request, entity) in SystemAPI.Query<SceneLoadRequest>().WithEntityAccess())
                    {
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }

        private void HandlePendingJoinClientTimeout(ref SystemState state, ref Singleton singleton,
            GameResources gameResources)
        {
            // Add ConnectionState component
            {
                EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                foreach (var (netId, entity) in SystemAPI.Query<NetworkId>().WithNone<ConnectionState>()
                             .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new ConnectionState());
                }
            }

            // Mark unjoined clients as pending
            {
                EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                foreach (var (netId, entity) in SystemAPI.Query<NetworkId>().WithNone<PendingClient>()
                             .WithNone<JoinedClient>().WithEntityAccess())
                {
                    ecb.AddComponent(entity, new PendingClient());
                }
            }

            // Handle join timeout for pending clients
            {
                EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                foreach (var (netId, pendingCLient, entity) in SystemAPI.Query<NetworkId, RefRW<PendingClient>>()
                             .WithEntityAccess())
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
                EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                // Process join requests
                foreach (var (request, rpcReceive, entity) in SystemAPI
                             .Query<JoinRequest, ReceiveRpcCommandRequest>().WithEntityAccess())
                {
                    if (SystemAPI.HasComponent<NetworkId>(rpcReceive.SourceConnection) &&
                        !SystemAPI.HasComponent<JoinedClient>(rpcReceive.SourceConnection))
                    {
                        int ownerConnectionId = SystemAPI.GetComponent<NetworkId>(rpcReceive.SourceConnection).Value;

                        // Mark connection as joined
                        ecb.RemoveComponent<PendingClient>(rpcReceive.SourceConnection);

                        Entity playerEntity = Entity.Null;
                        // Spawn player
                        playerEntity = ecb.Instantiate(gameResources.PlayerGhost);
                        ecb.SetComponent(playerEntity, new GhostOwner { NetworkId = ownerConnectionId });
                        ecb.AppendToBuffer(rpcReceive.SourceConnection,
                            new LinkedEntityGroup() { Value = playerEntity });

                        // Set player data
                        FirstPersonPlayer player = SystemAPI.GetComponent<FirstPersonPlayer>(gameResources.PlayerGhost);
                        player.Name = request.PlayerName;
                        ecb.SetComponent(playerEntity, player);

                        if (!request.IsSpectator)
                        {
                            // Request to spawn character
                            Entity spawnCharacterRequestEntity = ecb.CreateEntity();
                            ecb.AddComponent(spawnCharacterRequestEntity,
                                new CharacterSpawnRequest { ForConnection = rpcReceive.SourceConnection, Delay = -1f });
                        }

                        // Remember player for connection
                        ecb.AddComponent(rpcReceive.SourceConnection, new JoinedClient { PlayerEntity = playerEntity });

                        // Accept join request by sending it back
                        Entity joinRequestAcceptedEntity = state.EntityManager.CreateEntity();
                        ecb.AddComponent(joinRequestAcceptedEntity, request);
                        ecb.AddComponent(joinRequestAcceptedEntity,
                            new SendRpcCommandRequest { TargetConnection = rpcReceive.SourceConnection });

                        // Stream in game
                        ecb.AddComponent(rpcReceive.SourceConnection, new NetworkStreamInGame());
                    }

                    ecb.DestroyEntity(entity);
                }
            }
        }

        private void HandleCharacters(ref SystemState state, ref Singleton singleton, GameResources gameResources)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Initialize characters common
            foreach (var (character, physicsCollider, characterInitialized, entity) in SystemAPI
                         .Query<FirstPersonCharacterComponent, RefRW<PhysicsCollider>,
                             EnabledRefRW<CharacterInitialized>>()
                         .WithDisabled<CharacterInitialized>()
                         .WithEntityAccess())
            {
                physicsCollider.ValueRW.MakeUnique(entity, ecb);

                // Mark initialized
                characterInitialized.ValueRW = true;
            }

            // Spawn character requests
            if (SystemAPI.QueryBuilder().WithAll<CharacterSpawnRequest>().Build().CalculateEntityCount() > 0)
            {
                EntityQuery spawnPointsQuery = SystemAPI.QueryBuilder().WithAll<SpawnPoint, LocalToWorld>().Build();
                NativeArray<LocalToWorld> spawnPointLtWs =
                    spawnPointsQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
                DynamicBuffer<GameResourcesWeapon> weaponPrefabs = SystemAPI.GetSingletonBuffer<GameResourcesWeapon>();

                foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRW<CharacterSpawnRequest>>()
                             .WithEntityAccess())
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
                            int connectionId = SystemAPI.GetComponent<NetworkId>(spawnRequest.ValueRW.ForConnection)
                                .Value;
                            Entity playerEntity = SystemAPI
                                .GetComponent<JoinedClient>(spawnRequest.ValueRW.ForConnection)
                                .PlayerEntity;
                            float3 randomSpawnPosition = float3.zero;
                            if (spawnPointLtWs.Length > 0)
                            {
                                randomSpawnPosition =
                                    spawnPointLtWs[singleton.Random.NextInt(0, spawnPointLtWs.Length - 1)].Position;

                                randomSpawnPosition += singleton.Random.NextFloat3(
                                    new float3(-0.1f, 0f, -0.1f),
                                    new float3(0.1f, 0f, 0.1f));
                            }

                            // Spawn character
                            Entity characterEntity = ecb.Instantiate(gameResources.CharacterGhost);
                            ecb.SetComponent(characterEntity, new GhostOwner { NetworkId = connectionId });
                            ecb.SetComponent(characterEntity, LocalTransform.FromPosition(randomSpawnPosition));
                            ecb.SetComponent(characterEntity, new OwningPlayer { Entity = playerEntity });
                            ecb.AppendToBuffer(spawnRequest.ValueRW.ForConnection,
                                new LinkedEntityGroup() { Value = characterEntity });

                            // Assign character to player
                            FirstPersonPlayer player = SystemAPI.GetComponent<FirstPersonPlayer>(playerEntity);
                            player.ControlledCharacter = characterEntity;
                            ecb.SetComponent(playerEntity, player);

                            // Spawn & assign starting weapon
                            Entity randomWeaponPrefab;
                            if (gameResources.ForceOnlyFirstWeapon)
                            {
                                randomWeaponPrefab = weaponPrefabs[0].WeaponPrefab;
                            }
                            else
                            {
                                randomWeaponPrefab = weaponPrefabs[singleton.Random.NextInt(0, weaponPrefabs.Length)]
                                    .WeaponPrefab;
                            }

                            ;

                            // Weapon
                            Entity weaponEntity = ecb.Instantiate(randomWeaponPrefab);
                            ecb.SetComponent(weaponEntity, new GhostOwner { NetworkId = connectionId });
                            ecb.SetComponent(characterEntity, new ActiveWeapon { Entity = weaponEntity });

                            // End respawn screen if any
                            Entity respawnScreenRequestEntity = ecb.CreateEntity();
                            ecb.AddComponent(respawnScreenRequestEntity,
                                new RespawnMessageRequest { Start = false, CountdownTime = 0f });
                            ecb.AddComponent(respawnScreenRequestEntity,
                                new SendRpcCommandRequest { TargetConnection = spawnRequest.ValueRW.ForConnection });
                        }

                        ecb.DestroyEntity(entity);
                    }
                }

                spawnPointLtWs.Dispose();
            }
        }

        [BurstCompile]
        public struct BuildConnectionEntityMapJob : IJob
        {
            public NativeHashMap<int, Entity> ConnectionEntityMap;
            public NativeArray<Entity> ConnectionEntities;
            public NativeArray<NetworkId> Connections;

            public void Execute()
            {
                ConnectionEntityMap.Clear();
                for (int i = 0; i < Connections.Length; i++)
                {
                    ConnectionEntityMap.TryAdd(Connections[i].Value, ConnectionEntities[i]);
                }
            }
        }
    }
}
