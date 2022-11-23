using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct MachineGunVisuals : IComponentData
{
    public Entity BarrelEntity;
    public float SpinVelocity;
    public float SpinVelocityDecay;

    public float CurrentSpinVelocity;
}
