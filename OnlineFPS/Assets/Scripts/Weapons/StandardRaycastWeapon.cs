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

[Serializable]
[GhostComponent()]
public struct StandardRaycastWeapon : IComponentData, IEnableableComponent
{
    public Entity ShotOrigin;
    public Entity ProjectileVisualPrefab;
    
    public float Range;
    public float Damage;
    public float SpreadRadians;
    public int ProjectilesCount;
    public CollisionFilter HitCollisionFilter;

    // Calculation data
    [GhostField()]
    public Random Random;
    [GhostField()]
    public uint RemoteShotsCount;
    public uint LastRemoteShotsCount;
}

[Serializable]
public struct StandardRaycastWeaponShotVFXRequest : IBufferElementData
{
    public StandardRaycastWeaponShotVisualsData ShotVisualsData;
}

[Serializable]
public struct StandardRaycastWeaponShotVisualsData : IComponentData
{
    public float3 VisualOrigin;
    public float3 SimulationOrigin;
    public float3 SimulationDirection;
    public float3 SimulationUp;
    public float SimulationHitDistance;
    public RaycastHit Hit;
    public float3 VisualOriginToHit;
}