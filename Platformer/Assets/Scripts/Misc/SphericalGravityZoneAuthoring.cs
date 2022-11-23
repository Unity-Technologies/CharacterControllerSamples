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
            AddComponent(new SphericalGravityZone { GravityStrengthAtCenter = authoring.GravityStrengthAtCenter });
        }
    }
}
