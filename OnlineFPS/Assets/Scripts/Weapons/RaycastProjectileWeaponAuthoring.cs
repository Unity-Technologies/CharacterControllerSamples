using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace OnlineFPS
{
    [RequireComponent(typeof(BaseWeaponAuthoring))]
    public class RaycastProjectileWeaponAuthoring : MonoBehaviour
    {
        public GameObject ProjectilePrefab;
        public RaycastWeaponVisualsSyncMode VisualsSyncMode;

        public class Baker : Baker<RaycastProjectileWeaponAuthoring>
        {
            public override void Bake(RaycastProjectileWeaponAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RaycastWeapon
                {
                    ProjectilePrefab = GetEntity(authoring.ProjectilePrefab, TransformUsageFlags.Dynamic),
                    VisualsSyncMode = authoring.VisualsSyncMode,
                });
                AddBuffer<RaycastWeaponVisualProjectileEvent>(entity);
            }
        }
    }
}