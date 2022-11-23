using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PrefabThrowerAuthoring : MonoBehaviour
{
    public GameObject PrefabEntity;
    public float3 InitialEulerAngles;
    public float ThrowForce;

    public class Baker : Baker<PrefabThrowerAuthoring>
    {
        public override void Bake(PrefabThrowerAuthoring authoring)
        {
            AddComponent(new PrefabThrower
            {
                PrefabEntity = GetEntity(authoring.PrefabEntity),
                ThrowForce = authoring.ThrowForce,
                InitialEulerAngles = authoring.InitialEulerAngles,
            });
        }
    }
}