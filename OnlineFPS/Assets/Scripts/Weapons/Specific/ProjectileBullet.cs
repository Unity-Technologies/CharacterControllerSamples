using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using UnityEngine;

namespace OnlineFPS
{
    public struct ProjectileBullet : IComponentData, IEnableableComponent
    {
        public float Damage;

        public float HitSparksSize;
        public float HitSparksLifetime;
        public float HitSparksSpeed;
        public float3 HitSparksColor;

        public byte HasProcessedHitSimulation;
        public byte HasProcessedHitVFX;
    }
}