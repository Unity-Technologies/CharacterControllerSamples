using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace OnlineFPS
{
    [DisallowMultipleComponent]
    public class MainEntityCameraAuthoring : MonoBehaviour
    {
        public float FOV = 75f;

        public class Baker : Baker<MainEntityCameraAuthoring>
        {
            public override void Bake(MainEntityCameraAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MainEntityCamera(authoring.FOV));
            }
        }
    }
}