using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.NetCode;
using UnityEngine;

[Serializable]
public struct GameResources : IComponentData
{
    public int TickRate;
    public int SendRate;
    public int MaxSimulationStepsPerFrame;
    public float JoinTimeout;
    
    public float RespawnTime;
    
    public EntitySceneReference MenuVisualsScene;
    public EntitySceneReference GameResourcesScene;
    public EntitySceneReference GameScene;
    
    public Entity PlayerGhost;
    public Entity CharacterGhost;
    public Entity RailgunGhost;
    public Entity MachineGunGhost;

    public Entity SpectatorPrefab;

    public ClientServerTickRate GetClientServerTickRate()
    {
        ClientServerTickRate tickRate = new ClientServerTickRate();
        tickRate.ResolveDefaults();
        tickRate.SimulationTickRate = TickRate;
        tickRate.NetworkTickRate = SendRate;
        tickRate.MaxSimulationStepsPerFrame = MaxSimulationStepsPerFrame;
        return tickRate;
    }
}
