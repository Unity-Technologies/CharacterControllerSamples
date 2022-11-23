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
            AddComponent(new StressTestManagerSystem.Singleton
            {
                CharacterPrefab = GetEntity(authoring.CharacterPrefab),
                CharacterSpacing = authoring.CharacterSpacing,
            });
        
            DynamicBuffer<StressTestManagerSystem.EnvironmentPrefabs> environmentsBuffer = AddBuffer<StressTestManagerSystem.EnvironmentPrefabs>();
            for (int i = 0; i < authoring.EnvironmentPrefabs.Count; i++)
            {
                GameObject go = authoring.EnvironmentPrefabs[i];
                if (go != null)
                {
                    environmentsBuffer.Reinterpret<Entity>().Add(GetEntity(go));
                }
            }
        
            AddBuffer<StressTestManagerSystem.SpawnedCharacter>();
            AddBuffer<StressTestManagerSystem.SpawnedEnvironment>();
            AddBuffer<StressTestManagerSystem.Event>();
        }
    }
}