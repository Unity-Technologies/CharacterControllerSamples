using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class HealthAuthoring : MonoBehaviour
{
    public float MaxHealth = 100f;
    
    public class Baker : Baker<HealthAuthoring>
    {
        public override void Bake(HealthAuthoring authoring)
        {
            AddComponent(new Health
            {
                MaxHealth = authoring.MaxHealth,
                CurrentHealth = authoring.MaxHealth,
            });
        }
    }
}
