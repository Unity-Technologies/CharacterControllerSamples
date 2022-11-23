using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class BouncySurfaceAuthoring : MonoBehaviour
{
    public float BounceEnergyMultiplier = 1f;

    public class Baker : Baker<BouncySurfaceAuthoring>
    {
        public override void Bake(BouncySurfaceAuthoring authoring)
        {
            AddComponent(new BouncySurface
            {
                BounceEnergyMultiplier = authoring.BounceEnergyMultiplier,
            });
        }
    }
}