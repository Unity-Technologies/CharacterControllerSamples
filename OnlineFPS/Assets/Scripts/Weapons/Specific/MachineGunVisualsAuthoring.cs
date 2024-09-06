using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
    public class MachineGunVisualsAuthoring : MonoBehaviour
    {
        public GameObject BarrelEntity;
        public float SpinVelocity = math.PI * 2f;
        public float SpinVelocityDecay = 3f;

        public class Baker : Baker<MachineGunVisualsAuthoring>
        {
            public override void Bake(MachineGunVisualsAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MachineGunVisuals
                {
                    BarrelEntity = GetEntity(authoring.BarrelEntity, TransformUsageFlags.Dynamic),
                    SpinVelocity = authoring.SpinVelocity,
                    SpinVelocityDecay = authoring.SpinVelocityDecay,
                });
            }
        }
    }
}