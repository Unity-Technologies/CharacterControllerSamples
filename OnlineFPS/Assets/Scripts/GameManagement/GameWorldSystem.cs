using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.NetCode;
using UnityEngine;

namespace OnlineFPS
{
    public class GameSessionLink : IComponentData
    {
        public GameSession GameSession;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.LocalSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct GameWorldSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public FixedString128Bytes PlayerName;

            public bool IsAwaitingDisconnect;

            public SwitchBool HasConnected;
            public SwitchBool CrosshairActive;
            public SwitchBool RespawnScreenActive;
            public float RespawnScreenTimer;
        }

        public struct RequestDisconnect : IComponentData
        {
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>()
                .ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
            ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            GameSessionLink gameSessionLink =
                state.EntityManager.GetComponentObject<GameSessionLink>(SystemAPI.GetSingletonEntity<Singleton>());

            // Handle request to disconnect world
            {
                foreach (var (e, entity) in SystemAPI.Query<RequestDisconnect>().WithEntityAccess())
                {
                    singleton.IsAwaitingDisconnect = true;
                    ecb.DestroyEntity(entity);
                }

                if (singleton.IsAwaitingDisconnect)
                {
                    foreach (var (netId, entity) in SystemAPI.Query<NetworkId>()
                                 .WithNone<NetworkStreamRequestDisconnect>().WithEntityAccess())
                    {
                        ecb.AddComponent<NetworkStreamRequestDisconnect>(entity);
                    }
                }
            }

            // Detect connected
            if (singleton.HasConnected.HasChanged())
            {
                singleton.HasConnected.ConsumeChange();
                if (singleton.HasConnected.Get())
                {
                    gameSessionLink.GameSession.OnConnectionSuccess(state.World);
                }
            }

            // Detect disconnected
            if (singleton.IsAwaitingDisconnect)
            {
                int activeConnections = SystemAPI.QueryBuilder().WithAll<NetworkId>().Build().CalculateEntityCount();
                if (activeConnections <= 0)
                {
                    gameSessionLink.GameSession.OnWorldDisconnect(state.World);
                }
            }

            if (gameSessionLink.GameSession.IsMainWorld(state.World))
            {
                // Detect crosshair changes
                if (singleton.CrosshairActive.HasChanged())
                {
                    singleton.CrosshairActive.ConsumeChange();
                    GameManager.Instance.SetCrosshairActive(singleton.CrosshairActive.Get());
                }

                // Handle respawn screen
                {
                    // Toggle on/off
                    if (singleton.RespawnScreenActive.HasChanged())
                    {
                        singleton.RespawnScreenActive.ConsumeChange();
                        GameManager.Instance.SetRespawnScreenActive(singleton.RespawnScreenActive.Get());
                    }

                    // Timer
                    if (singleton.RespawnScreenActive.Get())
                    {
                        GameManager.Instance.SetRespawnScreenTimer(singleton.RespawnScreenTimer);
                    }
                }
            }
        }
    }
}