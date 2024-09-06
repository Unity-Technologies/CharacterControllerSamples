using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Random = Unity.Mathematics.Random;

namespace OnlineFPS
{
    [RequireComponent(typeof(BaseWeaponAuthoring))]
    public class PrefabProjectileWeaponAuthoring : MonoBehaviour
    {
        public GameObject ProjectilePrefab;

        class Baker : Baker<PrefabProjectileWeaponAuthoring>
        {
            public override void Bake(PrefabProjectileWeaponAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PrefabWeapon
                {
                    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                });
            }
        }
    }
}