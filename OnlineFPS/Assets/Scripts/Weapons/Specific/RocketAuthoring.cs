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
    public class RocketAuthoring : MonoBehaviour
    {
        public float DirectHitDamage;
        public float MaxRadiusDamage;
        public float DamageRadius;
        public float ExplosionSize = 1f;

        class Baker : Baker<RocketAuthoring>
        {
            public override void Bake(RocketAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Rocket
                {
                    DirectHitDamage = authoring.DirectHitDamage,
                    MaxRadiusDamage = authoring.MaxRadiusDamage,
                    DamageRadius = authoring.DamageRadius,
                    ExplosionSize = authoring.ExplosionSize,
                });
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, DamageRadius);
        }
    }
}