using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[DisallowMultipleComponent]
public class CameraTargetAuthoring : MonoBehaviour
{
    public GameObject Target;

    public class Baker : Baker<CameraTargetAuthoring>
    {
        public override void Bake(CameraTargetAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new CameraTarget
            {
                TargetEntity = GetEntity(authoring.Target, TransformUsageFlags.Dynamic),
            });
        }
    }
} 