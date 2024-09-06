using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace OnlineFPS
{
    [Serializable]
    [GhostComponent()]
    public struct Health : IComponentData
    {
        public float MaxHealth;
        [GhostField(Quantization = 100)] public float CurrentHealth;

        public bool IsDead()
        {
            return CurrentHealth <= 0f;
        }
    }
}
