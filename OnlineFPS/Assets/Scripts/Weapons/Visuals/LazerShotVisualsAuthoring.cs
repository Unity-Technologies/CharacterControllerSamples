using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class LazerShotVisualsAuthoring : MonoBehaviour
{
    public float Lifetime = 1f;
    public float Width = 1f;
    public GameObject HitVisualPrefab;
    
    class Baker : Baker<LazerShotVisualsAuthoring>
    {
        public override void Bake(LazerShotVisualsAuthoring authoring)
        {
            Entity selfEntity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);
            
            AddComponent(selfEntity, new LazerShotVisuals
            {
                LifeTime = authoring.Lifetime,
                Width = authoring.Width,
                HitVisualsPrefab = GetEntity(authoring.HitVisualPrefab, TransformUsageFlags.Dynamic),
            });
            AddComponent(selfEntity, new PostTransformMatrix { Value = float4x4.Scale(1f) });
        }
    }
}
