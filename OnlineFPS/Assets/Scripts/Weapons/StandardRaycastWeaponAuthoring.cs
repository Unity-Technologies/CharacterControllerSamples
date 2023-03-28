using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class StandardRaycastWeaponAuthoring : MonoBehaviour
{
    public StandardWeaponFiringMecanism.Authoring FiringMecanism = StandardWeaponFiringMecanism.Authoring.GetDefault();
    public WeaponVisualFeedback.Authoring VisualFeedback = WeaponVisualFeedback.Authoring.GetDefault();
    public GameObject ShotOrigin;
    public GameObject ProjectileVisualPrefab;
    public float Range = 1000f;
    public float Damage = 1000f;
    public float SpreadDegrees = 0f;
    public int ProjectilesCount = 1;
    public PhysicsCategoryTags HitCollisionFilter;
    
    public class Baker : Baker<StandardRaycastWeaponAuthoring>
    {
        public override void Bake(StandardRaycastWeaponAuthoring authoring)
        {
            WeaponUtilities.AddBasicWeaponBakingComponents(this);
            
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new WeaponVisualFeedback(authoring.VisualFeedback));
            AddComponent(entity, new StandardWeaponFiringMecanism(authoring.FiringMecanism));
            AddComponent(entity, new StandardRaycastWeapon
            {
                ShotOrigin = GetEntity(authoring.ShotOrigin, TransformUsageFlags.Dynamic),
                ProjectileVisualPrefab = GetEntity(authoring.ProjectileVisualPrefab, TransformUsageFlags.Dynamic),
                Range = authoring.Range,
                Damage = authoring.Damage,
                SpreadRadians = math.radians(authoring.SpreadDegrees),
                ProjectilesCount = authoring.ProjectilesCount,
                HitCollisionFilter = new CollisionFilter { BelongsTo = CollisionFilter.Default.BelongsTo, CollidesWith = authoring.HitCollisionFilter.Value },
                Random = Random.CreateFromIndex(0),
            });
            AddComponent<InterpolationDelay>(entity);
            AddBuffer<StandardRaycastWeaponShotVFXRequest>(entity);
        }
    }
}
