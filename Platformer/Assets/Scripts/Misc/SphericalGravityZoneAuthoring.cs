using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class SphericalGravityZoneAuthoring : MonoBehaviour
{
    public float GravityStrengthAtCenter;
    
    class Baker : Baker<SphericalGravityZoneAuthoring>
    {
        public override void Bake(SphericalGravityZoneAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SphericalGravityZone { GravityStrengthAtCenter = authoring.GravityStrengthAtCenter });
        }
    }
}
