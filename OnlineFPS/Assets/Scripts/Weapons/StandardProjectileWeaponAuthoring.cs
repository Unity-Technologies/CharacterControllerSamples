using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Random = Unity.Mathematics.Random;

public class StandardProjectileWeaponAuthoring : MonoBehaviour
{
    public StandardWeaponFiringMecanism.Authoring FiringMecanism = StandardWeaponFiringMecanism.Authoring.GetDefault();
    public WeaponVisualFeedback.Authoring VisualFeedback = WeaponVisualFeedback.Authoring.GetDefault();
    public GameObject ShotOrigin;
    public GameObject ProjectilePrefab;
    public float SpreadDegrees = 0f;
    public int ProjectilesCount = 1;
    
    class Baker : Baker<StandardProjectileWeaponAuthoring>
    {
        public override void Bake(StandardProjectileWeaponAuthoring authoring)
        {
            WeaponUtilities.AddBasicWeaponBakingComponents(this);
            
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new WeaponVisualFeedback(authoring.VisualFeedback));
            AddComponent(entity, new StandardWeaponFiringMecanism(authoring.FiringMecanism));
            AddComponent(entity, new StandardProjectileWeapon
            {
                ShotOrigin = GetEntity(authoring.ShotOrigin, TransformUsageFlags.Dynamic),
                ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                SpreadRadians = math.radians(authoring.SpreadDegrees),
                ProjectilesCount = authoring.ProjectilesCount,
                Random = Random.CreateFromIndex(0),
                
            });
        }
    }
}
