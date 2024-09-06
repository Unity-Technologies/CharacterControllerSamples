using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;
using Random = Unity.Mathematics.Random;

namespace OnlineFPS
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXSparksRequest
    {
        public Vector3 Position;
        public Vector3 Color;
        public float Size;
        public float Speed;
        public float Lifetime;
    }

    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct VFXExplosionRequest
    {
        public Vector3 Position;
        public float Size;
    }

    public static class VFXReferences
    {
        public static VisualEffect SparksGraph;
        public static GraphicsBuffer SparksRequestsBuffer;

        public static VisualEffect ExplosionsGraph;
        public static GraphicsBuffer ExplosionsRequestsBuffer;
    }

    public struct VFXManager<T> where T : unmanaged
    {
        public NativeReference<int> RequestsCount;
        public NativeArray<T> Requests;

        public bool GraphIsInitialized { get; private set; }

        public VFXManager(int maxRequests)
        {
            RequestsCount = new NativeReference<int>(0, Allocator.Persistent);
            Requests = new NativeArray<T>(maxRequests, Allocator.Persistent);

            GraphIsInitialized = false;
        }

        public void Dispose()
        {
            if (RequestsCount.IsCreated)
            {
                RequestsCount.Dispose();
            }

            if (Requests.IsCreated)
            {
                Requests.Dispose();
            }
        }

        public void Update(
            VisualEffect vfxGraph,
            ref GraphicsBuffer graphicsBuffer,
            bool uploadDataToGraphs,
            float deltaTimeMultiplier,
            int spawnBatchId,
            int requestsCountId,
            int requestsBufferId)
        {
            if (vfxGraph != null && graphicsBuffer != null)
            {
                vfxGraph.playRate = deltaTimeMultiplier;

                if (!GraphIsInitialized)
                {
                    vfxGraph.SetGraphicsBuffer(requestsBufferId, graphicsBuffer);
                    GraphIsInitialized = true;
                }

                if (graphicsBuffer.IsValid())
                {
                    if (uploadDataToGraphs)
                    {
                        graphicsBuffer.SetData(Requests, 0, 0, RequestsCount.Value);
                        vfxGraph.SetInt(requestsCountId, math.min(RequestsCount.Value, Requests.Length));
                        vfxGraph.SendEvent(spawnBatchId);
                    }

                    RequestsCount.Value = 0;
                }
            }
        }

        public void AddRequest(T request)
        {
            if (RequestsCount.Value < Requests.Length)
            {
                Requests[RequestsCount.Value] = request;
                RequestsCount.Value++;
            }
        }
    }

    public struct VFXSparksSingleton : IComponentData
    {
        public VFXManager<VFXSparksRequest> Manager;
    }

    public struct VFXExplosionsSingleton : IComponentData
    {
        public VFXManager<VFXExplosionRequest> Manager;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct VFXSystem : ISystem
    {
        private int _spawnBatchId;
        private int _requestsCountId;
        private int _requestsBufferId;

        private VFXManager<VFXSparksRequest> _sparksManager;
        private VFXManager<VFXExplosionRequest> _explosionsManager;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldSystem.Singleton>();

            // Names to Ids
            _spawnBatchId = Shader.PropertyToID("SpawnBatch");
            _requestsCountId = Shader.PropertyToID("SpawnRequestsCount");
            _requestsBufferId = Shader.PropertyToID("SpawnRequestsBuffer");

            // VFX managers
            _sparksManager = new VFXManager<VFXSparksRequest>(GameManager.SparksCapacity);
            _explosionsManager = new VFXManager<VFXExplosionRequest>(GameManager.ExplosionsCapacity);

            // Singletons
            state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new VFXSparksSingleton
            {
                Manager = _sparksManager,
            });
            state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new VFXExplosionsSingleton
            {
                Manager = _explosionsManager,
            });
        }

        public void OnDestroy(ref SystemState state)
        {
            _sparksManager.Dispose();
            _explosionsManager.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            GameSessionLink gameSessionLink =
                state.EntityManager.GetComponentObject<GameSessionLink>(SystemAPI
                    .GetSingletonEntity<GameWorldSystem.Singleton>());

            bool shouldUploadVFXData = gameSessionLink.GameSession.IsMainWorld(state.World);

            // This is required because we must use data in native collections on the main thread, to send it to VFXGraphs
            SystemAPI.QueryBuilder().WithAll<VFXSparksSingleton>().Build().CompleteDependency();
            SystemAPI.QueryBuilder().WithAll<VFXExplosionsSingleton>().Build().CompleteDependency();

            // Update managers
            float rateRatio = SystemAPI.Time.DeltaTime / Time.deltaTime;

            _sparksManager.Update(
                VFXReferences.SparksGraph,
                ref VFXReferences.SparksRequestsBuffer,
                shouldUploadVFXData,
                rateRatio,
                _spawnBatchId,
                _requestsCountId,
                _requestsBufferId);

            _explosionsManager.Update(
                VFXReferences.ExplosionsGraph,
                ref VFXReferences.ExplosionsRequestsBuffer,
                shouldUploadVFXData,
                rateRatio,
                _spawnBatchId,
                _requestsCountId,
                _requestsBufferId);
        }
    }
}