using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using System;
using Unity.Assertions;
using Unity.Core;
using UnityEngine;

namespace Rival
{
    /// <summary>
    /// Handles remembering character interpolation data during the fixed physics update
    /// </summary>
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup), OrderFirst = true)] 
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct CharacterInterpolationRememberTransformSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public float InterpolationDeltaTime;
            public double LastTimeRememberedInterpolationTransforms;
        }
        
        private ComponentTypeHandle<LocalTransform> _transformType;
        private ComponentTypeHandle<CharacterInterpolation> _characterInterpolationType;
        private EntityQuery _interpolatedEntitiesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _interpolatedEntitiesQuery = KinematicCharacterUtilities.GetInterpolatedCharacterQueryBuilder().Build(ref state);

            _transformType = state.GetComponentTypeHandle<LocalTransform>(true);
            _characterInterpolationType = state.GetComponentTypeHandle<CharacterInterpolation>(false);

            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton());

            state.RequireForUpdate(_interpolatedEntitiesQuery);
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformType.Update(ref state);
            _characterInterpolationType.Update(ref state);

            TimeData time = SystemAPI.Time;
            ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            singleton.InterpolationDeltaTime = time.DeltaTime;
            singleton.LastTimeRememberedInterpolationTransforms = time.ElapsedTime;

            CharacterInterpolationRememberTransformJob job = new CharacterInterpolationRememberTransformJob
            {
                TransformType = _transformType,
                CharacterInterpolationType = _characterInterpolationType,
            };
            state.Dependency = job.ScheduleParallel(_interpolatedEntitiesQuery, state.Dependency);
        }

        [BurstCompile]
        public unsafe struct CharacterInterpolationRememberTransformJob : IJobChunk
        {
            [ReadOnly] 
            public ComponentTypeHandle<LocalTransform> TransformType;
            public ComponentTypeHandle<CharacterInterpolation> CharacterInterpolationType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // No enabled comps support for interpolation
                Assert.IsFalse(useEnabledMask);

                NativeArray<LocalTransform> chunkTransforms = chunk.GetNativeArray(ref TransformType);
                NativeArray<CharacterInterpolation> chunkCharacterInterpolations = chunk.GetNativeArray(ref CharacterInterpolationType);

                void* chunkInterpolationsPtr = chunkCharacterInterpolations.GetUnsafePtr();
                int chunkCount = chunk.Count;
                int sizeCharacterInterpolation = UnsafeUtility.SizeOf<CharacterInterpolation>();
                var sizeTransform = UnsafeUtility.SizeOf<LocalTransform>();
                int sizePosition = UnsafeUtility.SizeOf<float3>();
                int sizeScale = UnsafeUtility.SizeOf<float>();
                int sizeRotation = UnsafeUtility.SizeOf<quaternion>();
                int sizeByte = UnsafeUtility.SizeOf<byte>();

                // Efficiently copy all position & rotation to the "from" rigidtransform in the character interpolation component
                {
                    // Copy positions
                    UnsafeUtility.MemCpyStride(
                        (void*)((long)chunkInterpolationsPtr + sizeRotation),
                        sizeCharacterInterpolation,
                        chunkTransforms.GetUnsafeReadOnlyPtr(),
                        sizeTransform,
                        sizePosition,
                        chunkCount
                    );
                    
                    // Copy rotations
                    UnsafeUtility.MemCpyStride(
                        chunkInterpolationsPtr,
                        sizeCharacterInterpolation,
                        (void*)((long)chunkTransforms.GetUnsafeReadOnlyPtr() + sizePosition + sizeScale),
                        sizeTransform,
                        sizeRotation,
                        chunkCount
                    );
                    
                    // Reset interpolation skippings
                    UnsafeUtility.MemCpyStride(
                        (void*)((long)chunkInterpolationsPtr + sizeRotation + sizePosition), // the "InterpolationSkipping" field
                        sizeCharacterInterpolation,
                        (void*)((long)chunkInterpolationsPtr + sizePosition + sizeRotation + sizeByte), // the "DefaultByte" field
                        sizeCharacterInterpolation,
                        sizeByte,
                        chunkCount
                    );
                }
            }
        }
    }

    /// <summary>
    /// Handles interpolating the character during variable update
    /// </summary>
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LocalToWorldSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct CharacterInterpolationSystem : ISystem
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
            CharacterInterpolationRememberTransformSystem.Singleton singleton = SystemAPI.GetSingletonRW<CharacterInterpolationRememberTransformSystem.Singleton>().ValueRO;
    
            if (singleton.LastTimeRememberedInterpolationTransforms <= 0f)
            {
                return;
            }
    
            float fixedTimeStep = singleton.InterpolationDeltaTime;
            if (fixedTimeStep == 0f)
            {
                return;
            }
    
            float timeAheadOfLastFixedUpdate = (float)(SystemAPI.Time.ElapsedTime - singleton.LastTimeRememberedInterpolationTransforms);
            float normalizedTimeAhead = math.clamp(timeAheadOfLastFixedUpdate / fixedTimeStep, 0f, 1f);
            
            CharacterInterpolationJob job = new CharacterInterpolationJob
            {
                NormalizedTimeAhead = normalizedTimeAhead,
            };
            job.ScheduleParallel();
        }
        
        [BurstCompile]
        public partial struct CharacterInterpolationJob : IJobEntity
        {
            public float NormalizedTimeAhead;
    
            void Execute(
                ref CharacterInterpolation characterInterpolation, 
                ref LocalToWorld localToWorld,
                in LocalTransform transform)
            {
                RigidTransform targetTransform = new RigidTransform(transform.Rotation, transform.Position);

                quaternion interpolatedRot = targetTransform.rot;
                if (characterInterpolation.InterpolateRotation == 1)
                {
                    if (!characterInterpolation.ShouldSkipNextRotationInterpolation())
                    {
                        interpolatedRot = math.slerp(characterInterpolation.InterpolationFromTransform.rot, targetTransform.rot, NormalizedTimeAhead);
                    }
                }
            
                float3 interpolatedPos = targetTransform.pos;
                if (characterInterpolation.InterpolatePosition == 1)
                {
                    if (!characterInterpolation.ShouldSkipNextPositionInterpolation())
                    {
                        interpolatedPos = math.lerp(characterInterpolation.InterpolationFromTransform.pos, targetTransform.pos, NormalizedTimeAhead);
                    }
                }
                
                localToWorld.Value = new float4x4(interpolatedRot, interpolatedPos);
            }
        }
    }
}