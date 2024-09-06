using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;

namespace OnlineFPS
{
    public partial struct SceneLoadRequestSystem : ISystem
    {
        public static Entity CreateSceneLoadRequest(EntityManager entityManager, SceneIdentifier sceneIdentifier)
        {
            Entity requestEntity = entityManager.CreateEntity(typeof(SceneLoadRequest));
            entityManager.AddBuffer<SceneIdentifier>(requestEntity).Add(sceneIdentifier);
            return requestEntity;
        }

        public static Entity CreateSceneLoadRequest(EntityManager entityManager,
            NativeList<SceneIdentifier> sceneIdentifiers)
        {
            Entity requestEntity = entityManager.CreateEntity(typeof(SceneLoadRequest));
            entityManager.AddBuffer<SceneIdentifier>(requestEntity);
            for (int i = 0; i < sceneIdentifiers.Length; i++)
            {
                entityManager.GetBuffer<SceneIdentifier>(requestEntity).Add(sceneIdentifiers[i]);
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
            // Detect completion of scenes load
            foreach (var (loadRequest, sceneIdentifiers, entity) in SystemAPI
                         .Query<RefRW<SceneLoadRequest>, DynamicBuffer<SceneIdentifier>>().WithEntityAccess())
            {
                DynamicBuffer<SceneIdentifier> sceneIdentifiersBuffer = sceneIdentifiers;

                bool allScenesLoaded = true;
                for (int i = 0; i < sceneIdentifiersBuffer.Length; i++)
                {
                    SceneIdentifier sceneIdentifier = sceneIdentifiersBuffer[i];

                    // Check if scene loaded
                    if (!SceneSystem.IsSceneLoaded(state.WorldUnmanaged, sceneIdentifier.SceneEntity))
                    {
                        allScenesLoaded = false;
                    }

                    sceneIdentifiersBuffer[i] = sceneIdentifier;
                }

                loadRequest.ValueRW.IsLoaded = allScenesLoaded;
            }
        }
    }
}