using Unity.Entities;
using UnityEngine;

namespace OnlineFPS
{
    class PrefabProjectileAuthoring : MonoBehaviour
    {
        public float Speed = 10f;
        public float Gravity = -10f;
        public float MaxLifetime = 5f;
        public float VisualOffsetCorrectionDuration = 0.3f;

        class Baker : Baker<PrefabProjectileAuthoring>
        {
            public override void Bake(PrefabProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PrefabProjectile
                {
                    Speed = authoring.Speed,
                    Gravity = authoring.Gravity,
                    MaxLifetime = authoring.MaxLifetime,
                    VisualOffsetCorrectionDuration = authoring.VisualOffsetCorrectionDuration,
                    LifetimeCounter = 0f,
                });
                AddComponent(entity, new ProjectileSpawnId());
                AddComponent<WeaponShotIgnoredEntity>(entity);
                AddComponent(entity, new DelayedDespawn());
                SetComponentEnabled<DelayedDespawn>(entity, false);
            }
        }
    }
}