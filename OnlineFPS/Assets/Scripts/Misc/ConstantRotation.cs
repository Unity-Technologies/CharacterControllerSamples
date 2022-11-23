using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct  ConstantRotation : IComponentData
{
    public float3 RotationSpeed;
}
