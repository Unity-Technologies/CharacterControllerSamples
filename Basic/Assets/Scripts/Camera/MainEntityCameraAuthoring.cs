using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[DisallowMultipleComponent]
public class MainEntityCameraAuthoring : MonoBehaviour
{
    public class Baker : Baker<MainEntityCameraAuthoring>
    {
        public override void Bake(MainEntityCameraAuthoring authoring)
        {
            AddComponent<MainEntityCamera>();
        }
    }
}
