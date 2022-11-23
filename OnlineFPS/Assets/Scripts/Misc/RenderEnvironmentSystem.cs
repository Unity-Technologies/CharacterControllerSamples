using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using RenderSettings = UnityEngine.RenderSettings;

[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial class RenderEnvironmentSystem : SystemBase
{
    public struct Singleton : IComponentData
    {
        public int ActiveLightingScene;
    }

    protected override void OnCreate()
    {
        base.OnCreate();

        Entity singletonEntity = EntityManager.CreateEntity();
        EntityManager.AddComponentData(singletonEntity, new Singleton
        {
            ActiveLightingScene = -1,
        });
        
        RequireForUpdate<Singleton>();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    protected override void OnUpdate()
    {
        ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (renderEnvironment, entity) in SystemAPI.Query<RenderEnvironment>().WithNone<RenderEnvironmentCleanup>().WithEntityAccess())
        {
            // setup cleanup component
            ecb.AddComponent(entity, new RenderEnvironmentCleanup { LightingSceneIndex = renderEnvironment.LightingSceneIndex });
            
            if (renderEnvironment.LightingSceneIndex >= 0)
            {
                AsyncOperation loadSceneOperation = SceneManager.LoadSceneAsync(renderEnvironment.LightingSceneIndex, LoadSceneMode.Additive);
                loadSceneOperation.allowSceneActivation = true;
                
                singleton.ActiveLightingScene = renderEnvironment.LightingSceneIndex;
            }
        }
        
        // Auto-unload lighting scene when RenderEnvironment is destroyed
        foreach (var (renderEnvironmentCleanup, entity) in SystemAPI.Query<RenderEnvironmentCleanup>().WithNone<RenderEnvironment>().WithEntityAccess())
        {
            if (renderEnvironmentCleanup.LightingSceneIndex >= 0)
            {
                if (SceneManager.GetSceneByBuildIndex(renderEnvironmentCleanup.LightingSceneIndex) != null)
                {
                    SceneManager.UnloadSceneAsync(singleton.ActiveLightingScene);
                }
            }
            ecb.RemoveComponent<RenderEnvironmentCleanup>(entity);
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    protected void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
        if (scene.buildIndex == singleton.ActiveLightingScene)
        {
            SceneManager.SetActiveScene(scene);
        }
    }
}
