using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GlobalGravityZoneAuthoring : MonoBehaviour
{
    public float3 Gravity;

    class Baker : Baker<GlobalGravityZoneAuthoring>
    {
        public override void Bake(GlobalGravityZoneAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new GlobalGravityZone { Gravity = authoring.Gravity });
        }
    }
}
