using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
    class BaseWeaponAuthoring : MonoBehaviour
    {
        public GameObject ShotOrigin;
        public bool Automatic;
        public float FiringRate;
        public float SpreadDegrees;
        public int ProjectilesPerShot;
        public WeaponVisualFeedback.Authoring VisualFeedback = WeaponVisualFeedback.Authoring.GetDefault();

        class Baker : Baker<BaseWeaponAuthoring>
        {
            public override void Bake(BaseWeaponAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new BaseWeapon
                {
                    ShotOrigin = GetEntity(authoring.ShotOrigin, TransformUsageFlags.Dynamic),
                    Automatic = authoring.Automatic,
                    FiringRate = authoring.FiringRate,
                    SpreadRadians = math.radians(authoring.SpreadDegrees),
                    ProjectilesPerShot = authoring.ProjectilesPerShot,
                });
                AddComponent(entity, new WeaponVisualFeedback(authoring.VisualFeedback));
                AddComponent(entity, new WeaponControl());
                AddComponent(entity, new WeaponOwner());
                AddComponent(entity, new WeaponShotSimulationOriginOverride());
                AddBuffer<WeaponShotIgnoredEntity>(entity);
                AddBuffer<WeaponProjectileEvent>(entity);
            }
        }
    }
}