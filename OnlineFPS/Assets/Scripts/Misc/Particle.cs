using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct Particle : IComponentData
{
    public float Lifetime;
    public float StartingScale;
    
    public float3 Velocity;
    public float CurrentLifetime;
}
