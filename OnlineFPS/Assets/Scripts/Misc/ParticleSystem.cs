using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PostPredictionPreTransformsECBSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct ParticleSystem : ISystem
{
    public struct Singleton : IComponentData
    {
        public Random Random;
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        Entity singleton = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singleton, new Singleton
        {
            Random = Random.CreateFromIndex(0),
        });
        
        state.RequireForUpdate<Singleton>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ParticleSpawnersJob spawnerJob = new ParticleSpawnersJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            SingletonEntity = SystemAPI.GetSingletonEntity<Singleton>(),
            SingletonLookup = SystemAPI.GetComponentLookup<Singleton>(false),
        };
        spawnerJob.Schedule();

        ParticlesJob particleJob = new ParticlesJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
        };
        particleJob.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct ParticleSpawnersJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public Entity SingletonEntity;
        public ComponentLookup<Singleton> SingletonLookup;
        
        void Execute(Entity entity, in LocalTransform transform, in ParticleSpawner spawner)
        {
            float3 pos = transform.Position;
            quaternion rot = transform.Rotation;
            Singleton singleton = SingletonLookup[SingletonEntity];
            
            for (int i = 0; i < spawner.ParticleCount; i++)
            {
                Entity particle = ECB.Instantiate(spawner.ParticlePrefab);
                ECB.SetComponent(particle, new Particle
                {
                    Lifetime = singleton.Random.NextFloat(spawner.ParticleLifetimeMinMax.x, spawner.ParticleLifetimeMinMax.y),
                    Velocity = singleton.Random.NextFloat3Direction() * singleton.Random.NextFloat(spawner.ParticleSpeedMinMax.x, spawner.ParticleSpeedMinMax.y),
                    StartingScale = singleton.Random.NextFloat(spawner.ParticleScaleMinMax.x, spawner.ParticleScaleMinMax.y),
                    CurrentLifetime = 0f,
                });
                ECB.SetComponent(particle, LocalTransform.FromPositionRotation(pos, rot));
            }

            ECB.DestroyEntity(entity);
            SingletonLookup[SingletonEntity] = singleton;
        }
    }

    [BurstCompile]
    public partial struct ParticlesJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;
        
        void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, ref Particle particle, ref LocalTransform transform)
        {
            if (particle.Lifetime <= 0f || particle.CurrentLifetime >= particle.Lifetime)
            {
                ECB.DestroyEntity(chunkIndexInQuery, entity);
                return;
            }
            
            float lifetimeRatio = math.clamp(particle.CurrentLifetime / particle.Lifetime, 0f, 1f);
            float invLifetimeRatio = 1f - lifetimeRatio ;
            transform.Position += particle.Velocity * DeltaTime;
            transform.Scale = invLifetimeRatio * particle.StartingScale;

            particle.CurrentLifetime += DeltaTime;
        }
    }
}
