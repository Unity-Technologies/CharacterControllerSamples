using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[Serializable]
[GhostComponent()]
public struct Health : IComponentData
{
    public float MaxHealth;
    [GhostField()]
    public float CurrentHealth;

    public bool IsDead()
    {
        return CurrentHealth <= 0f;
    }
}
