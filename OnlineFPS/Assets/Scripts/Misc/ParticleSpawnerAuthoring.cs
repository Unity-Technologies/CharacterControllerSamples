using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ParticleSpawnerAuthoring : MonoBehaviour
{
    public GameObject ParticlePrefab;
    public ParticleSpawner ParticleSpawner;
    
    public class Baker : Baker<ParticleSpawnerAuthoring>
    {
        public override void Bake(ParticleSpawnerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            authoring.ParticleSpawner.ParticlePrefab = GetEntity(authoring.ParticlePrefab, TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.ParticleSpawner);
        }
    }
}
