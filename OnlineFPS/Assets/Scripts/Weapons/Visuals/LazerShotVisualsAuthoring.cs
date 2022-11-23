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
            AddComponent(new LazerShotVisuals
            {
                LifeTime = authoring.Lifetime,
                Width = authoring.Width,
                HitVisualsPrefab = GetEntity(authoring.HitVisualPrefab),
            });
            AddComponent(new PostTransformScale { Value = float3x3.Scale(1f) });
        }
    }
}
