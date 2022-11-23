using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class BulletShotVisualsAuthoring : MonoBehaviour
{
    public GameObject HitVisualsPrefab;
    public float Speed = 10f;
    public float StretchFromSpeed = 1f;
    public float MaxStretch = 1f;
    
    public class Baker : Baker<BulletShotVisualsAuthoring>
    {
        public override void Bake(BulletShotVisualsAuthoring authoring)
        {
            AddComponent(new BulletShotVisuals
            {
                HitVisualsPrefab = GetEntity(authoring.HitVisualsPrefab),
                Speed = authoring.Speed,
                StretchFromSpeed = authoring.StretchFromSpeed,
                MaxStretch = authoring.MaxStretch,
            });
        }
    }
}
