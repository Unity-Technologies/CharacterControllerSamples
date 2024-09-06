using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
    public struct BulletShotVisuals : IComponentData
    {
        public float Speed;
        public float StretchFromSpeed;
        public float MaxStretch;

        public float HitSparksSize;
        public float HitSparksLifetime;
        public float HitSparksSpeed;
        public float3 HitSparksColor;

        public bool IsInitialized;
        public float DistanceTraveled;
    }
}