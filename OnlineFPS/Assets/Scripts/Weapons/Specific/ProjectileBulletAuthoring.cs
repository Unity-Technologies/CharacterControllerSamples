using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;

namespace OnlineFPS
{
    [RequireComponent(typeof(PrefabProjectileAuthoring))]
    public class ProjectileBulletAuthoring : MonoBehaviour
    {
        public float Damage;

        public float HitSparksSize = 1f;
        public float HitSparksSpeed = 1f;
        public float HitSparksLifetime = 1f;
        [ColorUsage(false, true)] public Color HitSparksColor = Color.red;

        class Baker : Baker<ProjectileBulletAuthoring>
        {
            public override void Bake(ProjectileBulletAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ProjectileBullet
                {
                    Damage = authoring.Damage,

                    HitSparksColor = ((float4)(Vector4)authoring.HitSparksColor).xyz,
                    HitSparksSize = authoring.HitSparksSize,
                    HitSparksSpeed = authoring.HitSparksSpeed,
                    HitSparksLifetime = authoring.HitSparksLifetime,
                });
            }
        }
    }
}