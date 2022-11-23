using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct SphericalGravityZone : IComponentData
{
    public float GravityStrengthAtCenter;
}