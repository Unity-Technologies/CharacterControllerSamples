using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;

public partial struct SceneLoadRequestSystem : ISystem
{
    public static Entity CreateSceneLoadRequest(EntityManager entityManager, EntitySceneReference sceneReference)
    {
        Entity requestEntity = entityManager.CreateEntity(typeof(SceneLoadRequest));
        DynamicBuffer<SceneIdentifier> scenesBuffer = entityManager.AddBuffer<SceneIdentifier>(requestEntity);
        scenesBuffer.Add(new SceneIdentifier(sceneReference));
        return requestEntity;
    }

    public static Entity CreateSceneLoadRequest(EntityManager entityManager, NativeList<EntitySceneReference> sceneReferences, bool autoDisposeList)
    {
        Entity requestEntity = entityManager.CreateEntity(typeof(SceneLoadRequest));
        DynamicBuffer<SceneIdentifier> scenesBuffer = entityManager.AddBuffer<SceneIdentifier>(requestEntity);
        for (int i = 0; i < sceneReferences.Length; i++)
        {
            scenesBuffer.Add(new SceneIdentifier(sceneReferences[i]));
        }

        if (autoDisposeList)
        {
            sceneReferences.Dispose();
        }

        return requestEntity;
    }

    public static Entity CreateSceneLoadRequest(EntityCommandBuffer ecb, EntitySceneReference sceneReference)
    {
        Entity requestEntity = ecb.CreateEntity();
        ecb.AddComponent(requestEntity, new SceneLoadRequest());
        DynamicBuffer<SceneIdentifier> scenesBuffer = ecb.AddBuffer<SceneIdentifier>(requestEntity);
        scenesBuffer.Add(new SceneIdentifier(sceneReference));
        return requestEntity;
    }

    public static Entity CreateSceneLoadRequest(EntityCommandBuffer ecb, NativeList<EntitySceneReference> sceneReferences, bool autoDisposeList)
    {
        Entity requestEntity = ecb.CreateEntity();
        ecb.AddComponent(requestEntity, new SceneLoadRequest());
        DynamicBuffer<SceneIdentifier> scenesBuffer = ecb.AddBuffer<SceneIdentifier>(requestEntity);
        for (int i = 0; i < sceneReferences.Length; i++)
        {
            scenesBuffer.Add(new SceneIdentifier(sceneReferences[i]));
        }

        if (autoDisposeList)
        {
            sceneReferences.Dispose();
        }

        return requestEntity;
    }

    private EntityQuery _sceneLoadRequestQuery;
    
    public void OnCreate(ref SystemState state)
    {
        _sceneLoadRequestQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<SceneLoadRequest, SceneIdentifier>()
            .Build(ref state);
        
        state.RequireForUpdate(_sceneLoadRequestQuery);
    }

    public void OnDestroy(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        NativeList<Entity> sceneRequestsToLoad = new NativeList<Entity>(Allocator.Temp);
        BufferLookup<SceneIdentifier> sceneBufferLookup = SystemAPI.GetBufferLookup<SceneIdentifier>(false);

        foreach (var (loadRequest, entity) in SystemAPI.Query<RefRW<SceneLoadRequest>>().WithEntityAccess())
        {
            if (sceneBufferLookup.HasBuffer(entity))
            {
                DynamicBuffer<SceneIdentifier> sceneBuffer = sceneBufferLookup[entity];

                bool hasAnyScenesNotStartedLoading = false;
                for (int i = 0; i < sceneBuffer.Length; i++)
                {
                    SceneIdentifier scene = sceneBuffer[i];
                    if (scene.SceneEntity == Entity.Null)
                    {
                        hasAnyScenesNotStartedLoading = true;
                    }
                }

                if (hasAnyScenesNotStartedLoading)
                {
                    sceneRequestsToLoad.Add(entity);
                }
                else
                {
                    bool allScenesLoaded = true;
                    for (int i = 0; i < sceneBuffer.Length; i++)
                    {
                        SceneIdentifier scene = sceneBuffer[i];

                        // Start loading scene if no entity
                        if (scene.SceneEntity == Entity.Null)
                        {
                        }
                        else
                        {
                            // Check if scene loaded
                            if (!SceneSystem.IsSceneLoaded(state.WorldUnmanaged, scene.SceneEntity))
                            {
                                allScenesLoaded = false;
                            }

                            sceneBuffer[i] = scene;
                        }
                    }

                    loadRequest.ValueRW.IsLoaded = allScenesLoaded;
                }
            }
        }

        for (int i = 0; i < sceneRequestsToLoad.Length; i++)
        {
            Entity entity = sceneRequestsToLoad[i];
            if (SystemAPI.GetBufferLookup<SceneIdentifier>(false).HasBuffer(entity))
            {
                NativeArray<SceneIdentifier> scenesArray = SystemAPI.GetBufferLookup<SceneIdentifier>(false)[entity].ToNativeArray(Allocator.Temp);
                for (int j = 0; j < scenesArray.Length; j++)
                {
                    SceneIdentifier sceneId = scenesArray[j];
                    if (sceneId.SceneEntity == Entity.Null)
                    {
                        sceneId.SceneEntity = SceneSystem.LoadSceneAsync(state.WorldUnmanaged, sceneId.SceneReference);

                        // Required due to structural changes
                        DynamicBuffer<SceneIdentifier> buffer = SystemAPI.GetBufferLookup<SceneIdentifier>(false)[entity];
                        buffer[j] = sceneId;
                    }
                }
                scenesArray.Dispose();
            }
        }

        sceneRequestsToLoad.Dispose();
    }
}
