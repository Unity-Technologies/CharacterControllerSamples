using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using RaycastHit = Unity.Physics.RaycastHit;

public enum RaycastProjectileVisualsSyncMode
{
    Precise,
    Efficient,
}

[Serializable]
[GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
public struct StandardRaycastWeapon : IComponentData, IEnableableComponent
{
    public Entity ShotOrigin;
    public Entity ProjectileVisualPrefab;
    
    public float Range;
    public float Damage;
    public float SpreadRadians;
    public int ProjectilesCount;
    public RaycastProjectileVisualsSyncMode ProjectileVisualsSyncMode;

    // Calculation data
    [GhostField()]
    public Random Random;

    public uint LastProcessedVisualsTick;
}

[Serializable]
[GhostComponent(OwnerSendType = SendToOwnerType.SendToNonOwner)]
public struct StandardRaycastWeaponShotVFXRequest : IBufferElementData
{
    [GhostField]
    public uint Tick;
    [GhostField]
    public byte DidHit;
    [GhostField]
    public float3 EndPoint;
    [GhostField]
    public float3 HitNormal;
}

[Serializable]
[GhostComponent(OwnerSendType = SendToOwnerType.SendToNonOwner)]
public struct StandardRaycastWeaponShotVFXData : IComponentData
{
    [GhostField]
    public uint ProjectileSpawnCount;
    public uint LastProjectileSpawnCount;
    public byte IsInitialized;
}

[Serializable]
public struct StandardRaycastWeaponShotVisualsData : IComponentData
{
    public byte DidHit;
    public float3 StartPoint;
    public float3 EndPoint;
    public float3 HitNormal;

    public float GetLength()
    {
        return math.length(EndPoint - StartPoint);
    }

    public float3 GetDirection()
    {
        return math.normalizesafe(EndPoint - StartPoint);
    }
}