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

[GhostComponent()]
public struct StandardProjectileWeapon : IComponentData
{
    public Entity ShotOrigin;
    public Entity ProjectilePrefab;
    
    public float SpreadRadians;
    public int ProjectilesCount;
    
    [GhostField()]
    public Random Random;
    [GhostField()]
    public int SpawnIdCounter;
    public uint LastProcessedVisualsTick;
}