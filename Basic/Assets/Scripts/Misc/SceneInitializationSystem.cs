using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.GraphicsIntegration;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[BurstCompile]
public partial struct SceneInitializationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    { }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Game init
        if (SystemAPI.HasSingleton<SceneInitialization>())
        {
            ref SceneInitialization sceneInitializer = ref SystemAPI.GetSingletonRW<SceneInitialization>().ValueRW;

            // Cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Spawn player
            Entity playerEntity = state.EntityManager.Instantiate(sceneInitializer.PlayerPrefabEntity);

            // Spawn character at spawn point
            Entity characterEntity = state.EntityManager.Instantiate(sceneInitializer.CharacterPrefabEntity);
            SystemAPI.SetComponent(characterEntity, SystemAPI.GetComponent<LocalTransform>(sceneInitializer.CharacterSpawnPointEntity));

            // Spawn camera
            Entity cameraEntity = state.EntityManager.Instantiate(sceneInitializer.CameraPrefabEntity);

            // Assign camera & character to player
            BasicPlayer player = SystemAPI.GetComponent<BasicPlayer>(playerEntity);
            player.ControlledCharacter = characterEntity;
            player.ControlledCamera = cameraEntity;
            SystemAPI.SetComponent(playerEntity, player);
            
            state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<SceneInitialization>());
        }
    }
}