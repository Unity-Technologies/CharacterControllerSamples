using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CustomGravityAuthoring : MonoBehaviour
{
    public float GravityMultiplier = 1f;

    class Baker : Baker<CustomGravityAuthoring>
    {
        public override void Bake(CustomGravityAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new CustomGravity { GravityMultiplier = authoring.GravityMultiplier });
        }
    }
}
