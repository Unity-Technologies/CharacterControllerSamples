using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct SelfDestructAfterTime : IComponentData
{
    public float LifeTime;
    public float TimeSinceAlive;
}