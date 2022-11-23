using System;
using Unity.Entities;

[Serializable]
public struct CharacterFrictionSurface : IComponentData
{
    public float VelocityFactor;
}