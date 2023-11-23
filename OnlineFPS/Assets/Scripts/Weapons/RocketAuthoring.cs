using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Authoring;

public class RocketAuthoring : MonoBehaviour
{
    public float Speed;
    public float DirectHitDamage;
    public float MaxRadiusDamage;
    public float DamageRadius;
    public float MaxLifetime;
    
    public GameObject HitVFXPrefab;
    
    public float VisualOffsetCorrectionDuration;
    
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
                HitVFXPrefab = GetEntity(authoring.HitVFXPrefab, TransformUsageFlags.Dynamic),
            });
            AddComponent(entity, new ProjectileShotVisuals
            {
                VisualOffsetCorrectionDuration = authoring.VisualOffsetCorrectionDuration,
            });
            AddComponent(entity, new Projectile
            {
                Speed = authoring.Speed,
                MaxLifetime = authoring.MaxLifetime,
                
                LifetimeCounter = 0f,
            });
            AddComponent(entity, new ProjectileSpawnId());
            AddComponent<WeaponShotIgnoredEntity>(entity);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, DamageRadius);
    }
}
