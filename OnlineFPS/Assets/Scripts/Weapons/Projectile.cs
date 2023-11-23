using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[WriteGroup(typeof(LocalToWorld))]
[GhostComponent]
public struct ProjectileShotVisuals : IComponentData
{
    public float VisualOffsetCorrectionDuration;
    [GhostField]
    public float3 VisualOffset;
}

[GhostComponent]
public struct Projectile : IComponentData
{
    public float Speed;
    public float MaxLifetime;

    [GhostField]
    public float LifetimeCounter;
    [GhostField]
    public byte HasHit;
    public Entity HitEntity;
    [GhostField]
    public float3 HitPosition;
    [GhostField]
    public float3 HitNormal;
}

[GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
public struct ProjectileSpawnId : IComponentData
{
    [GhostField]
    public Entity WeaponEntity;
    [GhostField]
    public int SpawnId;
    
    public bool IsSame(ProjectileSpawnId other)
    {
        return WeaponEntity == other.WeaponEntity && SpawnId == other.SpawnId;
    }
}

