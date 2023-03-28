using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class StressTestManagerAuthoring : MonoBehaviour
{
    public GameObject CharacterPrefab;
    public List<GameObject> EnvironmentPrefabs = new List<GameObject>();
    public float CharacterSpacing = 5f;

    public class Baker : Baker<StressTestManagerAuthoring>
    {
        public override void Bake(StressTestManagerAuthoring authoring)
        {
            Entity selfEntity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(selfEntity, new StressTestManagerSystem.Singleton
            {
                CharacterPrefab = GetEntity(authoring.CharacterPrefab, TransformUsageFlags.None),
                CharacterSpacing = authoring.CharacterSpacing,
            });
        
            DynamicBuffer<StressTestManagerSystem.EnvironmentPrefabs> environmentsBuffer = AddBuffer<StressTestManagerSystem.EnvironmentPrefabs>(selfEntity);
            for (int i = 0; i < authoring.EnvironmentPrefabs.Count; i++)
            {
                GameObject go = authoring.EnvironmentPrefabs[i];
                if (go != null)
                {
                    environmentsBuffer.Reinterpret<Entity>().Add(GetEntity(go, TransformUsageFlags.None));
                }
            }
        
            AddBuffer<StressTestManagerSystem.SpawnedCharacter>(selfEntity);
            AddBuffer<StressTestManagerSystem.SpawnedEnvironment>(selfEntity);
            AddBuffer<StressTestManagerSystem.Event>(selfEntity);
        }
    }
} 