using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    public class LazerShotVisualsAuthoring : MonoBehaviour
    {
        public float Lifetime = 1f;
        public float Width = 1f;
        public float HitSparksSize = 1f;
        public float HitSparksSpeed = 1f;
        public float HitSparksLifetime = 1f;
        [ColorUsage(false, true)] public Color HitSparksColor = Color.red;

        class Baker : Baker<LazerShotVisualsAuthoring>
        {
            public override void Bake(LazerShotVisualsAuthoring authoring)
            {
                Entity selfEntity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);

                AddComponent(selfEntity, new LazerShotVisuals
                {
                    LifeTime = authoring.Lifetime,
                    Width = authoring.Width,
                    HitSparksColor = ((float4)(Vector4)authoring.HitSparksColor).xyz,
                    HitSparksSize = authoring.HitSparksSize,
                    HitSparksSpeed = authoring.HitSparksSpeed,
                    HitSparksLifetime = authoring.HitSparksLifetime,
                });
                AddComponent(selfEntity, new PostTransformMatrix { Value = float4x4.Scale(1f) });
            }
        }
    }
}