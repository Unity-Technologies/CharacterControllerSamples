using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
    [RequireComponent(typeof(RaycastProjectileAuthoring))]
    public class BulletShotVisualsAuthoring : MonoBehaviour
    {
        public float Speed = 10f;
        public float StretchFromSpeed = 1f;
        public float MaxStretch = 1f;
        public float HitSparksSize = 1f;
        public float HitSparksSpeed = 1f;
        public float HitSparksLifetime = 1f;
        [ColorUsage(false, true)] public Color HitSparksColor = Color.red;

        public class Baker : Baker<BulletShotVisualsAuthoring>
        {
            public override void Bake(BulletShotVisualsAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);
                AddComponent(entity, new BulletShotVisuals
                {
                    Speed = authoring.Speed,
                    StretchFromSpeed = authoring.StretchFromSpeed,
                    MaxStretch = authoring.MaxStretch,
                    HitSparksColor = ((float4)(Vector4)authoring.HitSparksColor).xyz,
                    HitSparksSize = authoring.HitSparksSize,
                    HitSparksSpeed = authoring.HitSparksSpeed,
                    HitSparksLifetime = authoring.HitSparksLifetime,
                });
            }
        }
    }
}