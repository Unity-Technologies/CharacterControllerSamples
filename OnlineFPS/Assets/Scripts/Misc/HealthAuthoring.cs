using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OnlineFPS
{
    public class HealthAuthoring : MonoBehaviour
    {
        public float MaxHealth = 100f;

        public class Baker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Health
                {
                    MaxHealth = authoring.MaxHealth,
                    CurrentHealth = authoring.MaxHealth,
                });
            }
        }
    }
}