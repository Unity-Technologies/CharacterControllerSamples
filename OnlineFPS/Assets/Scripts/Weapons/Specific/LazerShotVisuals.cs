using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
    public struct LazerShotVisuals : IComponentData
    {
        public float LifeTime;
        public float Width;

        public float HitSparksSize;
        public float HitSparksLifetime;
        public float HitSparksSpeed;
        public float3 HitSparksColor;

        public float StartTime;
        public float3 StartingScale;
        public bool HasInitialized;
    }
}
