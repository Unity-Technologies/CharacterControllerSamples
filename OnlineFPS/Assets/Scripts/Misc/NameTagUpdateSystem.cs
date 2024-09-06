using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    public struct NameTagProxy : IComponentData
    {
        public Entity PlayerEntity;
    }

    public class NameTagProxyCleanup : ICleanupComponentData
    {
        public GameObject NameTagGameObject;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class NameTagUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (GameManager.Instance == null)
                return;

            // Init (spawn gameObject)
            EntityQuery initNameTagQuery =
                SystemAPI.QueryBuilder().WithAll<NameTagProxy>().WithNone<NameTagProxyCleanup>().Build();
            if (initNameTagQuery.CalculateChunkCount() > 0)
            {
                NativeArray<Entity> entities = initNameTagQuery.ToEntityArray(Allocator.Temp);
                NativeArray<NameTagProxy>
                    nameTags = initNameTagQuery.ToComponentDataArray<NameTagProxy>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity playerEntity = nameTags[i].PlayerEntity;

                    string playerName = "Player";
                    if (EntityManager.HasComponent<FirstPersonPlayer>(playerEntity))
                    {
                        playerName = EntityManager.GetComponentData<FirstPersonPlayer>(playerEntity).Name.ToString();
                    }

                    GameObject nameTagInstance = GameObject.Instantiate(GameManager.Instance.NameTagPrefab);
                    nameTagInstance.GetComponentInChildren<TextMeshProUGUI>().text = playerName;

                    EntityManager.AddComponentObject(entities[i],
                        new NameTagProxyCleanup { NameTagGameObject = nameTagInstance });
                }

                entities.Dispose();
                nameTags.Dispose();
            }

            // Update (follow transform & look at camera)
            if (SystemAPI.HasSingleton<MainEntityCamera>())
            {
                Entity mainCameraEntity = SystemAPI.GetSingletonEntity<MainEntityCamera>();
                float3 mainCameraPosition = SystemAPI.GetComponent<LocalToWorld>(mainCameraEntity).Position;

                foreach (var (ltw, nameTag, cleanup, entity) in SystemAPI
                             .Query<LocalToWorld, NameTagProxy, NameTagProxyCleanup>().WithEntityAccess())
                {
                    if (cleanup.NameTagGameObject != null)
                    {
                        cleanup.NameTagGameObject.transform.position = ltw.Position;

                        float3 selfToCamera = mainCameraPosition - ltw.Position;
                        cleanup.NameTagGameObject.transform.LookAt(ltw.Position - selfToCamera, math.up());
                    }
                }
            }

            // Destroy (destroy gameObject
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>()
                .ValueRW.CreateCommandBuffer(World.Unmanaged);
            foreach (var (cleanup, entity) in SystemAPI.Query<NameTagProxyCleanup>().WithNone<NameTagProxy>()
                         .WithEntityAccess())
            {
                if (cleanup.NameTagGameObject != null)
                {
                    GameObject.Destroy(cleanup.NameTagGameObject);
                }

                ecb.RemoveComponent<NameTagProxyCleanup>(entity);
            }
        }
    }
}