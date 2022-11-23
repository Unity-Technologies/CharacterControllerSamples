using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct BulletShotVisuals : IComponentData
{
    public Entity HitVisualsPrefab;
    public float Speed;
    public float StretchFromSpeed;
    public float MaxStretch;

    public bool IsInitialized;
    public float DistanceTraveled;
}
