using System;
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
    public RaycastProjectileVisualsSyncMode ProjectileVisualsSyncMode = RaycastProjectileVisualsSyncMode.Precise;
    public GameObject ShotOrigin;
    public GameObject ProjectileVisualPrefab;
    public float Range = 1000f;
    public float Damage = 1000f;
    public float SpreadDegrees = 0f;
    public int ProjectilesCount = 1;
    
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
                ProjectileVisualsSyncMode = authoring.ProjectileVisualsSyncMode,
                Range = authoring.Range,
                Damage = authoring.Damage,
                SpreadRadians = math.radians(authoring.SpreadDegrees),
                ProjectilesCount = authoring.ProjectilesCount,
                Random = Random.CreateFromIndex(0),
            });

            switch (authoring.ProjectileVisualsSyncMode)
            {
                case RaycastProjectileVisualsSyncMode.Precise:
                    AddComponent<StandardRaycastWeaponShotVFXRequest>(entity);
                    break;
                case RaycastProjectileVisualsSyncMode.Efficient:
                    AddComponent<StandardRaycastWeaponShotVFXData>(entity);
                    break;
            }
        }
    }
}
