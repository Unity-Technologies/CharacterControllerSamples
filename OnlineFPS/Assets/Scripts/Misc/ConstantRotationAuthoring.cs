using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
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
}