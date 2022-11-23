using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class PlatformerCharacterHybridData : IComponentData
{
    public GameObject MeshPrefab;
}

[Serializable]
public class PlatformerCharacterHybridLink : ICleanupComponentData
{
    public GameObject Object;
    public Animator Animator;
}