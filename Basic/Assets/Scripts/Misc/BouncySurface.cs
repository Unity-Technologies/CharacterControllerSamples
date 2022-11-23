using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct BouncySurface : IComponentData
{
    public float BounceEnergyMultiplier;
}