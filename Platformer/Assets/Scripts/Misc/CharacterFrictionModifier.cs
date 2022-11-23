using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct CharacterFrictionModifier : IComponentData
{
    public float Friction;
}