using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    public struct JoinRequest : IRpcCommand
    {
        public FixedString128Bytes PlayerName;
        public bool IsSpectator;
    }

    public struct RespawnMessageRequest : IRpcCommand
    {
        public bool Start;
        public float CountdownTime;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateBefore(typeof(GameWorldSystem))]
    [BurstCompile]
    public partial struct ClientGameSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public NetworkEndpoint ConnectEndPoint;

            public bool IsSpectator;
            public Unity.Mathematics.Random Random;

            public float TimeWithoutAConnection;
            public bool HasHandledConnect;
            public bool HasSentJoinRequest;
        }

        private EntityQuery _singletonQuery;
        private EntityQuery _spectatorSpawnPointsQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameResources>();
            state.RequireForUpdate<GameWorldSystem.Singleton>();

            _singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<Singleton>().Build(state.EntityManager);
            _spectatorSpawnPointsQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SpectatorSpawnPoint, LocalToWorld>().Build(state.EntityManager);

            // Auto-create singleton
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton
            {
                Random = Unity.Mathematics.Random.CreateFromIndex(0),
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<DisableCharacterDynamicContacts>())
            {
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<DisableCharacterDynamicContacts>());
            }

            ref Singleton singleton = ref _singletonQuery.GetSingletonRW<Singleton>().ValueRW;
            GameResources gameResources = SystemAPI.GetSingleton<GameResources>();

            HandleConnect(ref state, ref singleton);

            ref GameWorldSystem.Singleton gameWorldSystemSingleton =
                ref SystemAPI.GetSingletonRW<GameWorldSystem.Singleton>().ValueRW;

            HandleSendJoinRequest(ref state, ref singleton, ref gameWorldSystemSingleton);
            HandleWaitForJoinConfirmation(ref state, ref singleton, ref gameWorldSystemSingleton, gameResources);
            HandleCharacterSetup(ref state, ref gameWorldSystemSingleton);
            HandleRespawnScreen(ref state, ref singleton, ref gameWorldSystemSingleton);
            HandleDisconnect(ref state, ref singleton, ref gameWorldSystemSingleton, gameResources);
        }

        private void HandleConnect(ref SystemState state, ref Singleton singleton)
        {
            if (!singleton.HasHandledConnect &&
                SystemAPI.TryGetSingletonRW(out RefRW<NetworkStreamDriver> netStreamDriver))
            {
                netStreamDriver.ValueRW.Connect(state.EntityManager, singleton.ConnectEndPoint);
                singleton.HasHandledConnect = true;
            }
        }

        private void HandleSendJoinRequest(ref SystemState state, ref Singleton singleton,
            ref GameWorldSystem.Singleton gameWorldSystemSingleto)
        {
            if (!singleton.HasSentJoinRequest && SystemAPI.HasSingleton<NetworkId>())
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

                if (state.WorldUnmanaged.IsThinClient() || (hasHadSceneLoadRequest && allSceneLoadRequestsComplete))
                {
                    // Send join request
                    Entity joinRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(joinRequestEntity, new JoinRequest
                    {
                        PlayerName = gameWorldSystemSingleto.PlayerName,
                        IsSpectator = singleton.IsSpectator,
                    });
                    ecb.AddComponent(joinRequestEntity, new SendRpcCommandRequest());

                    singleton.HasSentJoinRequest = true;

                    foreach (var (request, entity) in SystemAPI.Query<SceneLoadRequest>().WithEntityAccess())
                    {
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }

        private void HandleWaitForJoinConfirmation(ref SystemState state, ref Singleton singleton,
            ref GameWorldSystem.Singleton gameWorldSystemSingleton, GameResources gameResources)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>()
                .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            if (SystemAPI.HasSingleton<NetworkId>() && !SystemAPI.HasSingleton<NetworkStreamInGame>())
            {
                // Check for request accept
                foreach (var (requestAccepted, rpcReceive, entity) in SystemAPI
                             .Query<JoinRequest, ReceiveRpcCommandRequest>().WithEntityAccess())
                {
                    singleton.TimeWithoutAConnection = 0f;

                    // Stream in game
                    ecb.AddComponent(SystemAPI.GetSingletonEntity<NetworkId>(), new NetworkStreamInGame());

                    // Overwrite client data with data received from server
                    gameWorldSystemSingleton.PlayerName = requestAccepted.PlayerName;
                    singleton.IsSpectator = requestAccepted.IsSpectator;

                    // Spectator mode
                    if (singleton.IsSpectator)
                    {
                        LocalToWorld spawnPoint = default;
                        NativeArray<LocalToWorld> spectatorSpawnPoints =
                            _spectatorSpawnPointsQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
                        if (spectatorSpawnPoints.Length > 0)
                        {
                            spawnPoint =
                                spectatorSpawnPoints[singleton.Random.NextInt(0, spectatorSpawnPoints.Length - 1)];
                        }

                        Entity spectatorEntity = ecb.Instantiate(gameResources.SpectatorPrefab);
                        ecb.SetComponent(spectatorEntity,
                            new LocalTransform()
                                { Position = spawnPoint.Position, Rotation = spawnPoint.Rotation, Scale = 1f });

                        spectatorSpawnPoints.Dispose();
                    }

                    gameWorldSystemSingleton.HasConnected.Set(true);

                    ecb.DestroyEntity(entity);
                }
            }
        }

        private void HandleCharacterSetup(ref SystemState state, ref GameWorldSystem.Singleton gameWorldSystemSingleton)
        {
            if (SystemAPI.HasSingleton<NetworkId>())
            {
                EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                // Initialize local-owned characters
                foreach (var (character, owningPlayer, ghostOwner, entity) in SystemAPI
                             .Query<FirstPersonCharacterComponent, OwningPlayer, GhostOwner>()
                             .WithAll<GhostOwnerIsLocal>()
                             .WithDisabled<CharacterInitialized>()
                             .WithEntityAccess())
                {
                    // Make camera follow character's view
                    ecb.AddComponent(character.ViewEntity, new MainEntityCamera { BaseFoV = character.BaseFoV });

                    // Make local character meshes rendering be shadow-only
                    BufferLookup<Child> childBufferLookup = SystemAPI.GetBufferLookup<Child>();
                    MiscUtilities.SetShadowModeInHierarchy(state.EntityManager, ecb, entity, ref childBufferLookup,
                        UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
                }

                // Initialize remote characters
                foreach (var (character, owningPlayer, ghostOwner, entity) in SystemAPI
                             .Query<FirstPersonCharacterComponent, OwningPlayer, GhostOwner>()
                             .WithNone<GhostOwnerIsLocal>()
                             .WithDisabled<CharacterInitialized>()
                             .WithEntityAccess())
                {
                    // Spawn nameTag
                    ecb.AddComponent(character.NameTagSocketEntity,
                        new NameTagProxy { PlayerEntity = owningPlayer.Entity });
                }

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
            }
        }

        private void HandleRespawnScreen(ref SystemState state, ref Singleton clientSingleton,
            ref GameWorldSystem.Singleton gameWorldSystemSingleton)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Handle respawn messages
            foreach (var (respawnRequest, receiveRPC, entity) in SystemAPI
                         .Query<RespawnMessageRequest, ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                if (respawnRequest.Start)
                {
                    gameWorldSystemSingleton.RespawnScreenTimer = respawnRequest.CountdownTime;
                    gameWorldSystemSingleton.RespawnScreenActive.Set(true);
                    gameWorldSystemSingleton.CrosshairActive.Set(false);
                }
                else
                {
                    gameWorldSystemSingleton.RespawnScreenActive.Set(false);
                    gameWorldSystemSingleton.CrosshairActive.Set(true);
                }

                ecb.DestroyEntity(entity);
            }

            // Update timer locally
            if (gameWorldSystemSingleton.RespawnScreenTimer >= 0f)
            {
                gameWorldSystemSingleton.RespawnScreenTimer -= SystemAPI.Time.DeltaTime;
            }
        }

        private void HandleDisconnect(ref SystemState state, ref Singleton singleton,
            ref GameWorldSystem.Singleton gameWorldSystemSingleton, GameResources gameResources)
        {
            // Check for connection timeout
            if (!SystemAPI.HasSingleton<NetworkId>())
            {
                singleton.TimeWithoutAConnection += SystemAPI.Time.DeltaTime;
                if (singleton.TimeWithoutAConnection > gameResources.JoinTimeout)
                {
                    gameWorldSystemSingleton.IsAwaitingDisconnect = true;
                }
            }
        }
    }
}