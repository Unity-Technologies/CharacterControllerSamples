using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MachineGunVisualsAuthoring : MonoBehaviour
{
    public GameObject BarrelEntity;
    public float SpinVelocity = math.PI * 2f;
    public float SpinVelocityDecay = 3f;
    
    public class Baker : Baker<MachineGunVisualsAuthoring>
    {
        public override void Bake(MachineGunVisualsAuthoring authoring)
        {
            AddComponent(new MachineGunVisuals
            {
                BarrelEntity = GetEntity(authoring.BarrelEntity),
                SpinVelocity = authoring.SpinVelocity,
                SpinVelocityDecay = authoring.SpinVelocityDecay,
            });
        }
    }
}
