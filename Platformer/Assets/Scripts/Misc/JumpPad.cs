using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct JumpPad : IComponentData
{
    public float JumpPower;
    public float UngroundingDotThreshold;
}