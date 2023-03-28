using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WindZoneAuthoring : MonoBehaviour
{
    public float3 WindForce;

    class Baker : Baker<WindZoneAuthoring>
    {
        public override void Bake(WindZoneAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new WindZone { WindForce = authoring.WindForce });
        }
    }
}
