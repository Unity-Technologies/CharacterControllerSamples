using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct ParticleSpawner : IComponentData
{
    public Entity ParticlePrefab;
    public int ParticleCount;
    public float2 ParticleLifetimeMinMax;
    public float2 ParticleScaleMinMax;
    public float2 ParticleSpeedMinMax;
}
