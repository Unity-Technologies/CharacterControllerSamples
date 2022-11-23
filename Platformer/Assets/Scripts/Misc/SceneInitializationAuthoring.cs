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
            AddComponent(new SceneInitialization
            {
                CharacterSpawnPointEntity = GetEntity(authoring.CharacterSpawnPointEntity),
                CharacterPrefabEntity = GetEntity(authoring.CharacterPrefabEntity),
                CameraPrefabEntity = GetEntity(authoring.CameraPrefabEntity),
                PlayerPrefabEntity = GetEntity(authoring.PlayerPrefabEntity),
            });
        }
    }
}