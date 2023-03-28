using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class SceneInitializationAuthoring : MonoBehaviour
{
    public GameObject CharacterSpawnPointEntity;
    public GameObject CharacterPrefabEntity;
    public GameObject CameraPrefabEntity;
    public GameObject PlayerPrefabEntity;

    public class Baker : Baker<SceneInitializationAuthoring>
    {
        public override void Bake(SceneInitializationAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SceneInitialization
            {
                CharacterSpawnPointEntity = GetEntity(authoring.CharacterSpawnPointEntity, TransformUsageFlags.Dynamic),
                CharacterPrefabEntity = GetEntity(authoring.CharacterPrefabEntity, TransformUsageFlags.Dynamic),
                CameraPrefabEntity = GetEntity(authoring.CameraPrefabEntity, TransformUsageFlags.Dynamic),
                PlayerPrefabEntity = GetEntity(authoring.PlayerPrefabEntity, TransformUsageFlags.Dynamic),
            });
        }
    }
}