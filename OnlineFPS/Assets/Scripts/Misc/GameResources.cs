using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

[Serializable]
public struct GameResources : IComponentData
{
    public int TickRate;
    public int SendRate;
    public int MaxSimulationStepsPerFrame;
    public float JoinTimeout;
    public float PolledEventsTimeout;
    
    public float RespawnTime;
    
    public EntitySceneReference MenuVisualsScene;
    public EntitySceneReference GameResourcesScene;
    public EntitySceneReference GameScene;
    
    public Entity PlayerGhost;
    public Entity CharacterGhost;
    public Entity RailgunGhost;
    public Entity MachineGunGhost;
    public Entity RocketLauncherGhost;
    public Entity ShotgunGhost;

    public Entity SpectatorPrefab;

    public ClientServerTickRate GetClientServerTickRate()
    {
        ClientServerTickRate tickRate = new ClientServerTickRate();
        tickRate.ResolveDefaults();
        tickRate.SimulationTickRate = TickRate;
        tickRate.NetworkTickRate = SendRate;
        tickRate.MaxSimulationStepsPerFrame = MaxSimulationStepsPerFrame;
        tickRate.PredictedFixedStepSimulationTickRatio = 1;
        return tickRate;
    }

    public uint GetOldestAllowedTickForPolledEventsTimeout(NetworkTick currentTick)
    {
        uint oldestAllowedTick = currentTick.TickIndexForValidTick;
        uint maxAgeInTicks = (uint)math.ceil(PolledEventsTimeout * TickRate);
        oldestAllowedTick = math.clamp(oldestAllowedTick - maxAgeInTicks, 0, uint.MaxValue);
        return oldestAllowedTick;
    }
}
