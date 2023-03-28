using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ConstantRotationAuthoring : MonoBehaviour
{
    public ConstantRotation ConstantRotation;

    public class Baker : Baker<ConstantRotationAuthoring>
    {
        public override void Bake(ConstantRotationAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.ConstantRotation);
        }
    }
}
