using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct WindZone : IComponentData
{
    public float3 WindForce;
}