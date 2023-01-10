using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Material = Unity.Physics.Material;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Rival
{
    /// <summary>
    /// Interface implemented by structs meant to be passed as parameter to <see cref="KinematicCharacterAspect"/> in order to customize internal character update logic.
    /// </summary>
    /// <typeparam name="C"> The type of the character "context" struct created by the user </typeparam>
    public interface IKinematicCharacterProcessor<C> where C : unmanaged
    {
        /// <summary>
        /// Requests that the grounding up direction should be updated.
        /// </summary>
        void UpdateGroundingUp(
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext);
        
        /// <summary>
        /// Determines if a hit can be collided with or not.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The evaluated hit </param> 
        /// <returns> Return true if the hit can be collided with, return false if not. </returns>
        bool CanCollideWithHit(
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit);

        /// <summary>
        /// Determines if the character can be grounded the hit or not.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The evaluated hit </param> 
        /// <param name="groundingEvaluationType"> An identifier meant to indicate what type of grounding evaluation is being done at the moment of calling this. </param>
        /// <returns></returns>
        bool IsGroundedOnHit(
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit, 
            int groundingEvaluationType);

        /// <summary>
        /// Determines what happens when the character detects a hit during its movement phase.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The evaluated hit </param> 
        /// <param name="remainingMovementDirection"> The direction of the movement vector that remains to be processed </param>
        /// <param name="remainingMovementLength"> The magnitude of the movement vector that remains to be processed </param>
        /// <param name="originalVelocityDirection"> The original direction of the movement vector before any movement projection happened </param>
        /// <param name="hitDistance"> The distance of the detected hit </param>
        void OnMovementHit(
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float hitDistance);

        /// <summary>
        /// Requests that the character velocity be projected on the hits detected so far in the character update.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="velocity"> The character velocity that needs to be projected </param>
        /// <param name="characterIsGrounded"> Whether the character is grounded or not </param>
        /// <param name="characterGroundHit"> The current effective ground hit of the character </param>
        /// <param name="hits"> The hits that have been detected so far during the character update </param>
        /// <param name="originalVelocityDirection"> The original velocity direction of the character at the beginning of the character update, before any projection has happened </param>
        void ProjectVelocityOnHits(
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext,
            ref float3 velocity,
            ref bool characterIsGrounded,
            ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> hits,
            float3 originalVelocityDirection);

        /// <summary>
        /// Provides an opportunity to modify the physics masses used to solve impulses between characters and detected hit bodies.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterMass"> The mass of the character </param>
        /// <param name="otherMass"> The mass of the other body that we've detected a hit with </param>
        /// <param name="hit"> The evaluated hit with the dynamic body </param>
        void OverrideDynamicHitMasses(
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext,
            ref PhysicsMass characterMass,
            ref PhysicsMass otherMass,
            BasicHit hit);
    }

    /// <summary>
    /// A struct used to store the entire state of the core character components
    /// </summary>
    [Serializable]
    public unsafe struct KinematicCharacterStateSave
    {
        public LocalTransform SavedTransform;
        public KinematicCharacterProperties SavedCharacterProperties;
        public KinematicCharacterBody SavedCharacterBody;
        
        public int SavedPhysicsColliderMemorySize;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<byte> SavedPhysicsColliderMemory;

        public int SavedCharacterHitsBufferCount;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<KinematicCharacterHit> SavedCharacterHitsBuffer;
        public int SavedStatefulHitsBufferCount;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<StatefulKinematicCharacterHit> SavedStatefulHitsBuffer;
        public int SavedDeferredImpulsesBufferCount;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<KinematicCharacterDeferredImpulse> SavedDeferredImpulsesBuffer;
        public int SavedVelocityProjectionHitsCount;
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<KinematicVelocityProjectionHit> SavedVelocityProjectionHits;
        
        /// <summary>
        /// Saves the character state. Only reallocates data arrays if the current arrays are not allocated or don't have the required capacity
        /// </summary>
        /// <param name="characterAspect"> The character aspect that provides access to the components to save </param>
        /// <param name="allocator"> The type of allocation that will be used to store arrays of data </param>
        public void Save(KinematicCharacterAspect characterAspect, Allocator allocator = Allocator.Temp)
        {
            SavedTransform = characterAspect.LocalTransform.ValueRO;
            SavedCharacterProperties = characterAspect.CharacterProperties.ValueRO;
            SavedCharacterBody = characterAspect.CharacterBody.ValueRO;
            
            PhysicsCollider characterAspectPhysicsCollider = characterAspect.PhysicsCollider.ValueRO;
            SavedPhysicsColliderMemorySize = characterAspectPhysicsCollider.ColliderPtr->MemorySize;
            CheckReallocateArray(ref SavedPhysicsColliderMemory, SavedPhysicsColliderMemorySize, allocator);
            UnsafeUtility.MemCpy(SavedPhysicsColliderMemory.GetUnsafePtr(), characterAspectPhysicsCollider.ColliderPtr, SavedPhysicsColliderMemorySize);

            SavedCharacterHitsBufferCount = characterAspect.CharacterHitsBuffer.Length;
            CheckReallocateArray(ref SavedCharacterHitsBuffer, SavedCharacterHitsBufferCount, allocator);
            UnsafeUtility.MemCpy(SavedCharacterHitsBuffer.GetUnsafePtr(), characterAspect.CharacterHitsBuffer.GetUnsafePtr(), SavedCharacterHitsBufferCount);
            
            SavedStatefulHitsBufferCount = characterAspect.StatefulHitsBuffer.Length;
            CheckReallocateArray(ref SavedStatefulHitsBuffer, SavedStatefulHitsBufferCount, allocator);
            UnsafeUtility.MemCpy(SavedStatefulHitsBuffer.GetUnsafePtr(), characterAspect.StatefulHitsBuffer.GetUnsafePtr(), SavedStatefulHitsBufferCount);
            
            SavedDeferredImpulsesBufferCount = characterAspect.DeferredImpulsesBuffer.Length;
            CheckReallocateArray(ref SavedDeferredImpulsesBuffer, SavedDeferredImpulsesBufferCount, allocator);
            UnsafeUtility.MemCpy(SavedDeferredImpulsesBuffer.GetUnsafePtr(), characterAspect.DeferredImpulsesBuffer.GetUnsafePtr(), SavedDeferredImpulsesBufferCount);
            
            SavedVelocityProjectionHitsCount = characterAspect.VelocityProjectionHits.Length;
            CheckReallocateArray(ref SavedVelocityProjectionHits, SavedVelocityProjectionHitsCount, allocator);
            UnsafeUtility.MemCpy(SavedVelocityProjectionHits.GetUnsafePtr(), characterAspect.VelocityProjectionHits.GetUnsafePtr(), SavedVelocityProjectionHitsCount);
        }

        /// <summary>
        /// Restores the character state.
        /// </summary>
        /// <param name="characterAspect"> The character aspect that provides access to the components to restore the state to </param>
        public void Restore(KinematicCharacterAspect characterAspect)
        {
            characterAspect.LocalTransform.ValueRW = SavedTransform;
            characterAspect.CharacterProperties.ValueRW = SavedCharacterProperties;
            characterAspect.CharacterBody.ValueRW = SavedCharacterBody;

            PhysicsCollider characterAspectPhysicsCollider = characterAspect.PhysicsCollider.ValueRW;
            if (characterAspectPhysicsCollider.ColliderPtr->MemorySize == SavedPhysicsColliderMemorySize)
            {
                UnsafeUtility.MemCpy(characterAspectPhysicsCollider.ColliderPtr, SavedPhysicsColliderMemory.GetUnsafePtr(), SavedPhysicsColliderMemorySize);
            }
            else
            {
                UnityEngine.Debug.LogError("Error: trying to restore collider state, but memory size of the PhysicsCollider component data on the character entity is different from the saved state. This may have happened because the collider type has been changed since saving the state. In this case, you have the responsibility of manually restoring the original collider type/MemorySize before you restore state.");
            }

            characterAspect.CharacterHitsBuffer.ResizeUninitialized(SavedCharacterHitsBufferCount);
            UnsafeUtility.MemCpy(characterAspect.CharacterHitsBuffer.GetUnsafePtr(), SavedCharacterHitsBuffer.GetUnsafePtr(), SavedCharacterHitsBufferCount);

            characterAspect.StatefulHitsBuffer.ResizeUninitialized(SavedStatefulHitsBufferCount);
            UnsafeUtility.MemCpy(characterAspect.StatefulHitsBuffer.GetUnsafePtr(), SavedStatefulHitsBuffer.GetUnsafePtr(), SavedStatefulHitsBufferCount);

            characterAspect.DeferredImpulsesBuffer.ResizeUninitialized(SavedDeferredImpulsesBufferCount);
            UnsafeUtility.MemCpy(characterAspect.DeferredImpulsesBuffer.GetUnsafePtr(), SavedDeferredImpulsesBuffer.GetUnsafePtr(), SavedDeferredImpulsesBufferCount);

            characterAspect.VelocityProjectionHits.ResizeUninitialized(SavedVelocityProjectionHitsCount);
            UnsafeUtility.MemCpy(characterAspect.VelocityProjectionHits.GetUnsafePtr(), SavedVelocityProjectionHits.GetUnsafePtr(), SavedVelocityProjectionHitsCount);
        }
        
        /// <summary>
        /// Disposes all data arrays stored in the character state save
        /// </summary>
        public void Dispose()
        {
            if (SavedPhysicsColliderMemory.IsCreated)
            {
                SavedPhysicsColliderMemory.Dispose();
            }

            if (SavedCharacterHitsBuffer.IsCreated)
            {
                SavedCharacterHitsBuffer.Dispose();
            }
            if (SavedStatefulHitsBuffer.IsCreated)
            {
                SavedStatefulHitsBuffer.Dispose();
            }
            if (SavedDeferredImpulsesBuffer.IsCreated)
            {
                SavedDeferredImpulsesBuffer.Dispose();
            }
            if (SavedVelocityProjectionHits.IsCreated)
            {
                SavedVelocityProjectionHits.Dispose();
            }
        }

        /// <summary>
        /// Reallocates a native array only if it is not created or if it does not have the required specified capacity
        /// </summary>
        /// <param name="arr"> The array to reallocate </param>
        /// <param name="requiredCapacity"> The minimum required capacity that the array should have </param>
        /// <param name="allocator"> The type of allocator to use </param>
        /// <typeparam name="T"> The type of elements stored in the array </typeparam>
        public static void CheckReallocateArray<T>(ref NativeArray<T> arr, int requiredCapacity, Allocator allocator) where T : unmanaged
        {
            if (!arr.IsCreated || arr.Length < requiredCapacity)
            {
                if (arr.IsCreated)
                {
                    arr.Dispose();
                }
                
                arr = new NativeArray<T>(requiredCapacity, allocator);
            }
        }
    }

    /// <summary>
    /// The context struct holding global data that needs to be accessed during the character update.
    /// </summary>
    public struct KinematicCharacterUpdateContext
    {
        public TimeData Time;

        [ReadOnly] public PhysicsWorld PhysicsWorld;

        [ReadOnly] public ComponentLookup<StoredKinematicCharacterData> StoredCharacterBodyPropertiesLookup;
        [ReadOnly] public ComponentLookup<TrackedTransform> TrackedTransformLookup;

        [NativeDisableContainerSafetyRestriction]
        public NativeList<RaycastHit> TmpRaycastHits;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<ColliderCastHit> TmpColliderCastHits;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<DistanceHit> TmpDistanceHits;
        [NativeDisableContainerSafetyRestriction]
        public NativeList<int> TmpRigidbodyIndexesProcessed;

        /// <summary>
        /// Provides an opportunity to get and store global data at the moment of a system's creation 
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        public void OnSystemCreate(ref SystemState state)
        {
            StoredCharacterBodyPropertiesLookup = state.GetComponentLookup<StoredKinematicCharacterData>(true);
            TrackedTransformLookup = state.GetComponentLookup<TrackedTransform>(true);
        }

        /// <summary>
        /// Provides an opportunity to update stored data during a system's update
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        /// <param name="time"> The time data passed on by the system calling this method </param>
        /// <param name="physicsWorldSingleton"> The physics world singleton passed on by the system calling this method </param>
        public void OnSystemUpdate(ref SystemState state, TimeData time, PhysicsWorldSingleton physicsWorldSingleton)
        {
            Time = time;
            PhysicsWorld = physicsWorldSingleton.PhysicsWorld;

            StoredCharacterBodyPropertiesLookup.Update(ref state);
            TrackedTransformLookup.Update(ref state);

            TmpRaycastHits = default;
            TmpColliderCastHits = default;
            TmpDistanceHits = default;
            TmpRigidbodyIndexesProcessed = default;
        }

        /// <summary>
        /// Ensures that the temporary collections held in this struct are created. This should normally be called within a job, before the character update
        /// </summary>
        public void EnsureCreationOfTmpCollections()
        {
            if (!TmpRaycastHits.IsCreated)
            {
                TmpRaycastHits = new NativeList<Unity.Physics.RaycastHit>(24, Allocator.Temp);
            }
            if (!TmpColliderCastHits.IsCreated)
            {
                TmpColliderCastHits = new NativeList<ColliderCastHit>(24, Allocator.Temp);
            }
            if (!TmpDistanceHits.IsCreated)
            {
                TmpDistanceHits = new NativeList<DistanceHit>(24, Allocator.Temp);
            }
            if (!TmpRigidbodyIndexesProcessed.IsCreated)
            {
                TmpRigidbodyIndexesProcessed = new NativeList<int>(24, Allocator.Temp);
            }
        }
    }

    /// <summary>
    /// Contains a set of parameters related to step and slope handling behaviour for a character
    /// </summary>
    [Serializable]
    public struct BasicStepAndSlopeHandlingParameters
    {
        /// <summary>
        /// Whether or not step handling logic is enabled
        /// </summary>
        [UnityEngine.Header("Step Handling")]
        [UnityEngine.Tooltip("Whether or not step handling logic is enabled")]
        public bool StepHandling;
        /// <summary>
        /// Max height that the character can step on
        /// </summary>
        [UnityEngine.Tooltip("Max height that the character can step on")]
        public float MaxStepHeight;
        /// <summary>
        /// Horizontal offset distance of extra downwards raycasts used to detect grounding around a step
        /// </summary>
        [UnityEngine.Tooltip("Horizontal offset distance of extra downwards raycasts used to detect grounding around a step")]
        public float ExtraStepChecksDistance;
        /// <summary>
        /// Character width used to determine grounding for steps. For a capsule this should be 2x capsule radius, and for a box it should be maximum box width. This is for cases where character with a spherical base tries to step onto an angled surface that is near the character's max step height. In thoses cases, the character might be grounded on steps on one frame, but wouldn't be grounded on the next frame as the spherical nature of its shape would push it a bit further up beyond its max step height.
        /// </summary>
        [UnityEngine.Tooltip("Character width used to determine grounding for steps. For a capsule this should be 2x capsule radius, and for a box it should be maximum box width. This is for cases where character with a spherical base tries to step onto an angled surface that is near the character's max step height. In thoses cases, the character might be grounded on steps on one frame, but wouldn't be grounded on the next frame as the spherical nature of its shape would push it a bit further up beyond its max step height.")]
        public float CharacterWidthForStepGroundingCheck;

        /// <summary>
        /// Whether or not to cancel grounding when the character is moving off a ledge. This prevents the character from "snapping" onto the ledge as it moves off of it
        /// </summary>
        [UnityEngine.Header("Slope Changes")]
        [UnityEngine.Tooltip("Whether or not to cancel grounding when the character is moving off a ledge. This prevents the character from \"snapping\" onto the ledge as it moves off of it")]
        public bool PreventGroundingWhenMovingTowardsNoGrounding;
        /// <summary>
        /// Whether or not the character has a max slope change that it can stay grounded on
        /// </summary>
        [UnityEngine.Tooltip("Whether or not the character has a max slope change that it can stay grounded on")]
        public bool HasMaxDownwardSlopeChangeAngle;
        /// <summary>
        /// Max slope change that the character can stay grounded on
        /// </summary>
        [UnityEngine.Tooltip("Max slope change that the character can stay grounded on")]
        [UnityEngine.Range(0f, 180f)]
        public float MaxDownwardSlopeChangeAngle;
        
        /// <summary>
        /// Whether or not to constrain the character velocity to ground plane when it hits a non-grounded slope
        /// </summary>
        [UnityEngine.Header("Misc")]
        [UnityEngine.Tooltip("Whether or not to constrain the character velocity to ground plane when it hits a non-grounded slope")]
        public bool ConstrainVelocityToGroundPlane;

        public static BasicStepAndSlopeHandlingParameters GetDefault()
        {
            return new BasicStepAndSlopeHandlingParameters
            {
                StepHandling = false,
                MaxStepHeight = 0.5f,
                ExtraStepChecksDistance = 0.1f,
                CharacterWidthForStepGroundingCheck = 1f,

                PreventGroundingWhenMovingTowardsNoGrounding = true,
                HasMaxDownwardSlopeChangeAngle = false,
                MaxDownwardSlopeChangeAngle = 90f,

                ConstrainVelocityToGroundPlane = true,
            };
        }
    }

    /// <summary>
    /// Aspect regrouping the core components and logic of a character controller
    /// </summary>
    public readonly partial struct KinematicCharacterAspect : IAspect
    {
        /// <summary>
        /// Defines various constants used throughout the character update
        /// </summary>
        public struct Constants
        {
            public const float CollisionOffset = 0.01f;
            public const float MinVelocityLengthSqForGroundingIgnoreCheck = 0.01f * 0.01f;
            public const float DotProductSimilarityEpsilon = 0.001f;
            public const float DefaultReverseProjectionMaxLengthRatio = 10f;
            public const float GroundedHitDistanceTolerance = CollisionOffset * 6f;
            public const float GroundedHitDistanceToleranceSq = GroundedHitDistanceTolerance * GroundedHitDistanceTolerance;
            public const float StepGroundingDetectionHorizontalOffset = 0.01f;
            public const float MinVelocityDotRatioWithGroundingUpForSteppingUpHits = -0.85f;
            public const float MinDotRatioForVerticalDecollision = 0.1f;
        }
        
        /// <summary>
        /// The entity of the character
        /// </summary>
        public readonly Entity Entity;
        /// <summary>
        /// The local transform component of the character entity
        /// </summary>
        public readonly RefRW<LocalTransform> LocalTransform;
        /// <summary>
        /// The <see cref="KinematicCharacterProperties"/> component of the character entity
        /// </summary>
        public readonly RefRW<KinematicCharacterProperties> CharacterProperties;
        /// <summary>
        /// The <see cref="KinematicCharacterBody"/> component of the character entity
        /// </summary>
        public readonly RefRW<KinematicCharacterBody> CharacterBody;
        /// <summary>
        /// The <see cref="PhysicsCollider"/> component of the character entity
        /// </summary>
        public readonly RefRW<PhysicsCollider> PhysicsCollider;
        /// <summary>
        /// The <see cref="KinematicCharacterHit"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<KinematicCharacterHit> CharacterHitsBuffer;
        /// <summary>
        /// The <see cref="StatefulKinematicCharacterHit"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<StatefulKinematicCharacterHit> StatefulHitsBuffer;
        /// <summary>
        /// The <see cref="KinematicCharacterDeferredImpulse"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<KinematicCharacterDeferredImpulse> DeferredImpulsesBuffer;
        /// <summary>
        /// The <see cref="KinematicVelocityProjectionHit"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<KinematicVelocityProjectionHit> VelocityProjectionHits;
        
        /// <summary>
        /// Returns the forward direction of the character transform
        /// </summary>
        public float3 Forward
        {
            get => math.mul(LocalTransform.ValueRO.Rotation, math.forward());
        }
        /// <summary>
        /// Returns the back direction of the character transform
        /// </summary>
        public float3 Back
        {
            get => math.mul(LocalTransform.ValueRO.Rotation, -math.forward());
        }
        /// <summary>
        /// Returns the up direction of the character transform
        /// </summary>
        public float3 Up
        {
            get => math.mul(LocalTransform.ValueRO.Rotation, math.up());
        }
        /// <summary>
        /// Returns the down direction of the character transform
        /// </summary>
        public float3 Down
        {
            get => math.mul(LocalTransform.ValueRO.Rotation, -math.up());
        }
        /// <summary>
        /// Returns the right direction of the character transform
        /// </summary>
        public float3 Right
        {
            get => math.mul(LocalTransform.ValueRO.Rotation, math.right());
        }
        /// <summary>
        /// Returns the left direction of the character transform
        /// </summary>
        public float3 Left
        {
            get => math.mul(LocalTransform.ValueRO.Rotation, -math.right());
        }

        /// <summary>
        /// The initialization step of the character update (should be called on every character update). This resets key component values and buffers
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="characterBody"> The character body component </param>
        /// <param name="deltaTime"> The time delta of the character update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Update_Initialize<T, C>(
            in T processor, 
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody, 
            float deltaTime)  where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            CharacterHitsBuffer.Clear();
            DeferredImpulsesBuffer.Clear();
            VelocityProjectionHits.Clear();

            characterBody.WasGroundedBeforeCharacterUpdate = characterBody.IsGrounded;
            characterBody.ParentVelocity = default;
            characterBody.RotationFromParent = quaternion.identity;
            characterBody.PreviousParentEntity = characterBody.ParentEntity;
            characterBody.IsGrounded = false;
            characterBody.GroundHit = default;
            characterBody.LastPhysicsUpdateDeltaTime = deltaTime;
            
            processor.UpdateGroundingUp(ref context, ref baseContext);
        }

        /// <summary>
        /// Handles moving the character based on its currently-assigned ParentEntity, if any.
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="constrainRotationToGroundingUp"> Whether or not to limit rotation around the grounding up direction </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Update_ParentMovement<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            bool constrainRotationToGroundingUp) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;

            // Reset parent if parent entity doesn't exist anymore
            if (characterBody.ParentEntity != Entity.Null && !baseContext.TrackedTransformLookup.HasComponent(characterBody.ParentEntity))
            {
                characterBody.ParentEntity = Entity.Null;
            }

            if (characterBody.PreviousParentEntity != Entity.Null && !baseContext.TrackedTransformLookup.HasComponent(characterBody.PreviousParentEntity))
            {
                characterBody.PreviousParentEntity = Entity.Null;
            }

            // Movement from parent body
            characterBody.ParentVelocity = default;
            if (characterBody.ParentEntity != Entity.Null)
            {
                TrackedTransform parentTrackedTransform = baseContext.TrackedTransformLookup[characterBody.ParentEntity];

                // Position
                float3 previousLocalPosition = math.transform(math.inverse(parentTrackedTransform.PreviousFixedRateTransform), characterPosition);
                float3 targetWorldPosition = math.transform(parentTrackedTransform.CurrentFixedRateTransform, previousLocalPosition);

                // Rotation
                quaternion previousLocalRotation = math.mul(math.inverse(parentTrackedTransform.PreviousFixedRateTransform.rot), characterRotation);
                quaternion targetWorldRotation = math.mul(parentTrackedTransform.CurrentFixedRateTransform.rot, previousLocalRotation);

                // Rotation up correction
                if (constrainRotationToGroundingUp)
                {
                    float3 targetWorldAnchorPoint = math.transform(parentTrackedTransform.CurrentFixedRateTransform, characterBody.ParentLocalAnchorPoint);
                    quaternion correctedRotation = MathUtilities.CreateRotationWithUpPriority(characterBody.GroundingUp, MathUtilities.GetForwardFromRotation(targetWorldRotation));
                    MathUtilities.SetRotationAroundPoint(ref targetWorldRotation, ref targetWorldPosition, targetWorldAnchorPoint, correctedRotation);
                }

                // Store data about parent movement
                float3 displacementFromParentMovement = targetWorldPosition - characterPosition;
                characterBody.ParentVelocity = (targetWorldPosition - characterPosition) / baseContext.Time.DeltaTime;
                characterBody.RotationFromParent = math.mul(math.inverse(characterRotation), targetWorldRotation);

                // Move Position
                if (characterProperties.DetectMovementCollisions &&
                    characterProperties.DetectObstructionsForParentBodyMovement &&
                    math.lengthsq(displacementFromParentMovement) > math.EPSILON)
                {
                    float3 castDirection = math.normalizesafe(displacementFromParentMovement);
                    float castLength = math.length(displacementFromParentMovement);

                    ColliderCastInput castInput = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition, characterPosition + (castDirection * castLength), characterRotation);
                    baseContext.TmpColliderCastHits.Clear();
                    AllHitsCollector<ColliderCastHit> collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
                    baseContext.PhysicsWorld.CastCollider(castInput, ref collector);
                    if (FilterColliderCastHitsForMove(in processor, ref context, ref baseContext, ref baseContext.TmpColliderCastHits, !characterProperties.SimulateDynamicBody, castDirection, characterBody.ParentEntity, characterProperties.ShouldIgnoreDynamicBodies(), out ColliderCastHit closestHit, out bool foundAnyOverlaps))
                    {
                        characterPosition += castDirection * closestHit.Fraction * castLength;
                    }
                    else
                    {
                        characterPosition += displacementFromParentMovement;
                    }
                }
                else
                {
                    characterPosition += displacementFromParentMovement;
                }
            }
        }

        /// <summary>
        /// Handles detecting character grounding and storing results in <see cref="KinematicCharacterBody"/>
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Update_Grounding<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;

            // Detect ground
            bool newIsGrounded = false;
            BasicHit newGroundHit = default;
            if (characterProperties.EvaluateGrounding)
            {
                // Calculate ground probe length based on circumstances
                float groundDetectionLength = Constants.CollisionOffset * 3f;
                if (characterProperties.SnapToGround && characterBody.WasGroundedBeforeCharacterUpdate)
                {
                    groundDetectionLength = characterProperties.GroundSnappingDistance;
                }

                GroundDetection(
                    in processor, 
                    ref context, 
                    ref baseContext, 
                    groundDetectionLength, 
                    out newIsGrounded, 
                    out newGroundHit, 
                    out float distanceToGround);

                // Ground snapping
                if (characterProperties.SnapToGround && newIsGrounded)
                {
                    characterPosition -= characterBody.GroundingUp * distanceToGround;
                    characterPosition += characterBody.GroundingUp * Constants.CollisionOffset;
                }

                // Add ground hit as a character hit and project velocity
                if (newIsGrounded)
                {
                    KinematicCharacterHit groundCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(
                        in newGroundHit,
                        characterBody.WasGroundedBeforeCharacterUpdate,
                        characterBody.RelativeVelocity,
                        newIsGrounded);
                    VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(groundCharacterHit));

                    bool tmpIsGrounded = characterBody.WasGroundedBeforeCharacterUpdate;
                    processor.ProjectVelocityOnHits(
                        ref context,
                        ref baseContext,
                        ref characterBody.RelativeVelocity,
                        ref tmpIsGrounded,
                        ref newGroundHit, // in theory this should be the previous ground hit instead, but since it will be a ground-to-ground projection, it doesn't matter. Using previous ground normal here would force us to sync it for networking prediction
                        in VelocityProjectionHits,
                        math.normalizesafe(characterBody.RelativeVelocity));

                    groundCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                    CharacterHitsBuffer.Add(groundCharacterHit);
                }
            }

            characterBody.IsGrounded = newIsGrounded;
            characterBody.GroundHit = newGroundHit;
        }

        /// <summary>
        /// Handles moving the character and solving collisions, based on character velocity, rotation, character grounding, and various other properties
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Update_MovementAndDecollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            
            float3 originalVelocityDirectionBeforeMove = math.normalizesafe(characterBody.RelativeVelocity);

            // Move character based on relativeVelocity
            bool moveConfirmedThereWereNoOverlaps = false;
            MoveWithCollisions(
                in processor, 
                ref context, 
                ref baseContext, 
                ref characterBody,
                ref characterPosition,
                originalVelocityDirectionBeforeMove, 
                out moveConfirmedThereWereNoOverlaps);

            // This has to be after movement has been processed, in order to let our movement to take us 
            // out of the collision with a platform before we try to decollide from it
            if (characterProperties.DecollideFromOverlaps && !moveConfirmedThereWereNoOverlaps)
            {
                SolveOverlaps(
                    in processor, 
                    ref context, 
                    ref baseContext, 
                    ref characterBody,
                    ref characterPosition,
                    originalVelocityDirectionBeforeMove);
            }

            // Process moving body hit velocities
            if (CharacterHitsBuffer.Length > 0)
            {
                ProcessCharacterHitDynamics(
                    in processor, 
                    ref context,
                    ref baseContext,
                    ref characterBody);
            }
        }

        /// <summary>
        /// Handles predicting future slope changes in order to prevent grounding in certain scenarios
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="stepAndSlopeHandling"> Parameters for step and slope handling </param>
        /// <param name="slopeDetectionVerticalOffset"> The vertical distance from ground hit at which slope detection raycasts will start </param>
        /// <param name="slopeDetectionDownDetectionDepth"> The distance of downward slope detection raycasts, added to the initial vertical offset </param>
        /// <param name="slopeDetectionSecondaryNoGroundingCheckDistance"> The forward distance of an extra raycast meant to detect slopes that are slightly further away than where our velocity would bring us over the next update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Update_PreventGroundingFromFutureSlopeChange<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            in BasicStepAndSlopeHandlingParameters stepAndSlopeHandling,
            float slopeDetectionVerticalOffset = 0.05f,
            float slopeDetectionDownDetectionDepth = 0.05f,
            float slopeDetectionSecondaryNoGroundingCheckDistance = 0.25f) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            if (characterBody.IsGrounded && (stepAndSlopeHandling.PreventGroundingWhenMovingTowardsNoGrounding || stepAndSlopeHandling.HasMaxDownwardSlopeChangeAngle))
            {
                DetectFutureSlopeChange(
                    in processor,
                    ref context,
                    ref baseContext,
                    slopeDetectionVerticalOffset,
                    slopeDetectionDownDetectionDepth,
                    baseContext.Time.DeltaTime,
                    slopeDetectionSecondaryNoGroundingCheckDistance,
                    stepAndSlopeHandling.StepHandling,
                    stepAndSlopeHandling.MaxStepHeight,
                    out bool isMovingTowardsNoGrounding,
                    out bool foundSlopeHit,
                    out float futureSlopeChangeAnglesRadians,
                    out RaycastHit futureSlopeHit);

                if ((stepAndSlopeHandling.PreventGroundingWhenMovingTowardsNoGrounding && isMovingTowardsNoGrounding) ||
                    (stepAndSlopeHandling.HasMaxDownwardSlopeChangeAngle && foundSlopeHit && math.degrees(futureSlopeChangeAnglesRadians) < -stepAndSlopeHandling.MaxDownwardSlopeChangeAngle))
                {
                    characterBody.IsGrounded = false;
                }
            }
        }

        /// <summary>
        /// Handles applying ground push forces to the currently-detected ground hit, if applicable
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="gravity"> The effective gravity used to create a force to apply to the ground, in combination with the character mass </param>
        /// <param name="forceMultiplier"> An arbitrary multiplier to apply to the calculated force to apply to the ground </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Update_GroundPushing<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 gravity,
            float forceMultiplier = 1f) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            float3 characterPosition = LocalTransform.ValueRO.Position;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            KinematicCharacterBody characterBody = CharacterBody.ValueRO;

            if (characterBody.IsGrounded && characterProperties.SimulateDynamicBody)
            {
                Entity groundEntity = characterBody.GroundHit.Entity;

                if (groundEntity != Entity.Null &&
                    PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, characterBody.GroundHit.RigidBodyIndex))
                {
                    PhysicsUtilities.GetBodyComponents(
                        in baseContext.PhysicsWorld,
                        characterBody.GroundHit.RigidBodyIndex,
                        out LocalTransform groundLocalTransform,
                        out PhysicsVelocity groundPhysicsVelocity,
                        out PhysicsMass groundPhysicsMass);

                    PhysicsMass selfPhysicsMass = PhysicsUtilities.GetKinematicCharacterPhysicsMass(characterProperties);
                    RigidTransform selfTransform = new RigidTransform(characterRotation, characterPosition);
                    RigidTransform groundTransform = new RigidTransform(groundLocalTransform.Rotation, groundLocalTransform.Position);

                    selfPhysicsMass.InverseMass = 1f / characterProperties.Mass;
                    processor.OverrideDynamicHitMasses(ref context, ref baseContext, ref selfPhysicsMass, ref groundPhysicsMass, characterBody.GroundHit);

                    float3 groundPointVelocity = groundPhysicsVelocity.GetLinearVelocity(
                        groundPhysicsMass,
                        groundLocalTransform.Position,
                        groundLocalTransform.Rotation,
                        characterBody.GroundHit.Position);

                    // Solve impulses
                    PhysicsUtilities.SolveCollisionImpulses(
                        new PhysicsVelocity { Linear = groundPointVelocity + (gravity * baseContext.Time.DeltaTime), Angular = default },
                        groundPhysicsVelocity,
                        selfPhysicsMass,
                        groundPhysicsMass,
                        selfTransform,
                        groundTransform,
                        characterBody.GroundHit.Position,
                        -math.normalizesafe(gravity),
                        out float3 impulseOnSelf,
                        out float3 impulseOnOther);

                    float3 previousLinearVel = groundPhysicsVelocity.Linear;
                    float3 previousAngularVel = groundPhysicsVelocity.Angular;

                    groundPhysicsVelocity.ApplyImpulse(
                        groundPhysicsMass,
                        groundTransform.pos,
                        groundTransform.rot,
                        impulseOnOther * forceMultiplier,
                        characterBody.GroundHit.Position);

                    DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                    {
                        OnEntity = groundEntity,
                        LinearVelocityChange = groundPhysicsVelocity.Linear - previousLinearVel,
                        AngularVelocityChange = groundPhysicsVelocity.Angular - previousAngularVel,
                    });
                }
            }
        }

        /// <summary>
        /// Handles detecting valid moving platforms based on current ground hit, and automatically sets them as the character's parent entity
        /// </summary>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        public void Update_MovingPlatformDetection(ref KinematicCharacterUpdateContext baseContext, ref KinematicCharacterBody characterBody)
        {
            if (characterBody.IsGrounded &&
                baseContext.TrackedTransformLookup.HasComponent(characterBody.GroundHit.Entity))
            {
                RigidTransform groundWorldTransform = baseContext.PhysicsWorld.Bodies[characterBody.GroundHit.RigidBodyIndex].WorldFromBody;
                SetOrUpdateParentBody(ref baseContext, ref characterBody, characterBody.GroundHit.Entity, math.transform(math.inverse(groundWorldTransform), characterBody.GroundHit.Position));
            }
            else
            {
                SetOrUpdateParentBody(ref baseContext, ref characterBody, Entity.Null, default);
            }
        }

        /// <summary>
        /// Handles preserving velocity momentum when getting unparented from a parent body (such as a moving platform).
        /// </summary>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        public void Update_ParentMomentum(ref KinematicCharacterUpdateContext baseContext, ref KinematicCharacterBody characterBody)
        {
            float3 characterPosition = LocalTransform.ValueRO.Position;
            
            // Reset parent if parent entity doesn't exist anymore
            if (characterBody.ParentEntity != Entity.Null && !baseContext.TrackedTransformLookup.HasComponent(characterBody.ParentEntity))
            {
                characterBody.ParentEntity = Entity.Null;
            }
            if (characterBody.PreviousParentEntity != Entity.Null && !baseContext.TrackedTransformLookup.HasComponent(characterBody.PreviousParentEntity))
            {
                characterBody.PreviousParentEntity = Entity.Null;
            }

            // Handle adding parent body momentum
            if (characterBody.ParentEntity != characterBody.PreviousParentEntity)
            {
                // Handle preserving momentum from previous parent when there has been a parent change
                if (characterBody.PreviousParentEntity != Entity.Null)
                {
                    characterBody.RelativeVelocity += characterBody.ParentVelocity;
                }

                // Handle compensating momentum for new parent body
                if (characterBody.ParentEntity != Entity.Null)
                {
                    TrackedTransform parentTrackedTransform = baseContext.TrackedTransformLookup[characterBody.ParentEntity];
                    characterBody.RelativeVelocity -= parentTrackedTransform.CalculatePointVelocity(characterPosition, baseContext.Time.DeltaTime);

                    if (characterBody.IsGrounded)
                    {
                        ProjectVelocityOnGrounding(ref characterBody.RelativeVelocity, characterBody.GroundHit.Normal, characterBody.GroundingUp);
                    }
                }
            }
        }

        /// <summary>
        /// Handles filling the stateful hits buffer on the character entity, with character hits that have an Enter/Exit/Stay state associated to them
        /// </summary>
        public void Update_ProcessStatefulCharacterHits()
        {
            bool OldStatefulHitsContainEntity(in DynamicBuffer<StatefulKinematicCharacterHit> statefulCharacterHitsBuffer, Entity entity, int lastIndexOfOldStatefulHits, out CharacterHitState oldState)
            {
                oldState = default;

                if (lastIndexOfOldStatefulHits < 0)
                {
                    return false;
                }

                for (int i = 0; i <= lastIndexOfOldStatefulHits; i++)
                {
                    StatefulKinematicCharacterHit oldStatefulHit = statefulCharacterHitsBuffer[i];
                    if (oldStatefulHit.Hit.Entity == entity)
                    {
                        oldState = oldStatefulHit.State;
                        return true;
                    }
                }

                return false;
            }

            bool NewStatefulHitsContainEntity(in DynamicBuffer<StatefulKinematicCharacterHit> statefulCharacterHitsBuffer, Entity entity, int firstIndexOfNewStatefulHits)
            {
                if (firstIndexOfNewStatefulHits >= statefulCharacterHitsBuffer.Length)
                {
                    return false;
                }

                for (int i = firstIndexOfNewStatefulHits; i < statefulCharacterHitsBuffer.Length; i++)
                {
                    StatefulKinematicCharacterHit newStatefulHit = statefulCharacterHitsBuffer[i];
                    if (newStatefulHit.Hit.Entity == entity)
                    {
                        return true;
                    }
                }

                return false;
            }

            int lastIndexOfOldStatefulHits = StatefulHitsBuffer.Length - 1;

            // Add new stateful hits
            for (int hitIndex = 0; hitIndex < CharacterHitsBuffer.Length; hitIndex++)
            {
                KinematicCharacterHit characterHit = CharacterHitsBuffer[hitIndex];
                if (!NewStatefulHitsContainEntity(in StatefulHitsBuffer, characterHit.Entity, lastIndexOfOldStatefulHits + 1))
                {
                    StatefulKinematicCharacterHit newStatefulHit = new StatefulKinematicCharacterHit(characterHit);
                    bool entityWasInStatefulHitsBefore = OldStatefulHitsContainEntity(in StatefulHitsBuffer, characterHit.Entity, lastIndexOfOldStatefulHits, out CharacterHitState oldHitState);

                    if (entityWasInStatefulHitsBefore)
                    {
                        switch (oldHitState)
                        {
                            case CharacterHitState.Enter:
                                newStatefulHit.State = CharacterHitState.Stay;
                                break;
                            case CharacterHitState.Stay:
                                newStatefulHit.State = CharacterHitState.Stay;
                                break;
                            case CharacterHitState.Exit:
                                newStatefulHit.State = CharacterHitState.Enter;
                                break;
                        }
                    }
                    else
                    {
                        newStatefulHit.State = CharacterHitState.Enter;
                    }

                    StatefulHitsBuffer.Add(newStatefulHit);
                }
            }

            // Detect Exit states 
            for (int i = 0; i <= lastIndexOfOldStatefulHits; i++)
            {
                StatefulKinematicCharacterHit oldStatefulHit = StatefulHitsBuffer[i];

                // If an old hit entity isn't in new hits, add as Exit state
                if (oldStatefulHit.State != CharacterHitState.Exit && !NewStatefulHitsContainEntity(in StatefulHitsBuffer, oldStatefulHit.Hit.Entity, lastIndexOfOldStatefulHits + 1))
                {
                    oldStatefulHit.State = CharacterHitState.Exit;
                    StatefulHitsBuffer.Add(oldStatefulHit);
                }
            }

            // Remove all old stateful hits
            if (lastIndexOfOldStatefulHits >= 0)
            {
                StatefulHitsBuffer.RemoveRange(0, lastIndexOfOldStatefulHits + 1);
            }
        }
        
        /// <summary>
        /// Detects grounding at the current character pose
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="groundProbingLength"> Ground probing collider cast distance </param>
        /// <param name="isGrounded"> Outputs whether or not valid ground was detected </param>
        /// <param name="groundHit"> Outputs the detected ground hit </param>
        /// <param name="distanceToGround"> Outputs the distance of the detected ground hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public unsafe void GroundDetection<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float groundProbingLength,
            out bool isGrounded,
            out BasicHit groundHit,
            out float distanceToGround) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            isGrounded = false;
            groundHit = default;
            distanceToGround = 0f;
            
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            float3 characterPosition = LocalTransform.ValueRO.Position;
            KinematicCharacterBody characterBody = CharacterBody.ValueRO;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;

            ColliderCastInput input = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition, characterPosition + (-characterBody.GroundingUp * groundProbingLength), characterRotation);
            baseContext.TmpColliderCastHits.Clear();
            AllHitsCollector<ColliderCastHit> collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
            baseContext.PhysicsWorld.CastCollider(input, ref collector);

            if (FilterColliderCastHitsForGroundProbing(in processor, ref context, ref baseContext, ref baseContext.TmpColliderCastHits, -characterBody.GroundingUp, characterProperties.ShouldIgnoreDynamicBodies(), out ColliderCastHit closestHit))
            {
                // Ground hit is closest hit by default
                groundHit = new BasicHit(closestHit);
                distanceToGround = closestHit.Fraction * groundProbingLength;

                // Check grounding status
                if (characterProperties.EvaluateGrounding)
                {
                    bool isGroundedOnClosestHit = processor.IsGroundedOnHit(
                        ref context, 
                        ref baseContext,
                        in groundHit, 
                        (int)GroundingEvaluationType.GroundProbing);
                    if (isGroundedOnClosestHit)
                    {
                        isGrounded = true;
                    }
                    else
                    {
                        // If the closest hit wasn't grounded but other hits were detected, try to find the closest grounded hit within tolerance range
                        if (baseContext.TmpColliderCastHits.Length > 1)
                        {
                            // Sort hits in ascending fraction order
                            // TODO: We are doing a sort because, presumably, it would be faster to sort & have potentially less hits to evaluate for grounding
                            baseContext.TmpColliderCastHits.Sort(default(HitFractionComparer));

                            for (int i = 0; i < baseContext.TmpColliderCastHits.Length; i++)
                            {
                                ColliderCastHit tmpHit = baseContext.TmpColliderCastHits[i];

                                // Skip if this is our ground hit
                                if (tmpHit.RigidBodyIndex == groundHit.RigidBodyIndex && 
                                    tmpHit.ColliderKey.Equals(groundHit.ColliderKey))
                                    continue;

                                //Only accept if within tolerance distance
                                float tmpHitDistance = tmpHit.Fraction * groundProbingLength;
                                if (math.distancesq(tmpHitDistance, distanceToGround) <= Constants.GroundedHitDistanceToleranceSq)
                                {
                                    BasicHit tmpClosestGroundedHit = new BasicHit(tmpHit);
                                    bool isGroundedOnHit = processor.IsGroundedOnHit(
                                        ref context, 
                                        ref baseContext,
                                        in tmpClosestGroundedHit, 
                                        (int)GroundingEvaluationType.GroundProbing);
                                    if (isGroundedOnHit)
                                    {
                                        isGrounded = true;
                                        distanceToGround = tmpHitDistance;
                                        groundHit = tmpClosestGroundedHit; 
                                        break;
                                    }
                                }
                                else
                                {
                                    // if we're starting to see hits with a distance greater than tolerance dist, give up trying to evaluate hits since the list is sorted in ascending fraction order
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Enhanced ground distance computing
                if (characterProperties.EnhancedGroundPrecision && distanceToGround <= 0f)
                {
                    RigidBody otherBody = baseContext.PhysicsWorld.Bodies[closestHit.RigidBodyIndex];
                    if (otherBody.Collider.AsPtr()->GetLeaf(closestHit.ColliderKey, out ChildCollider leafCollider))
                    {
                        RigidTransform characterWorldTransform = new RigidTransform(characterRotation, characterPosition);
                        characterWorldTransform = math.mul(characterWorldTransform, characterPhysicsCollider.ColliderPtr->MassProperties.MassDistribution.Transform);
                        RigidTransform otherBodyWorldTransform = math.mul(otherBody.WorldFromBody, leafCollider.TransformFromChild);
                        RigidTransform characterRelativeToOther = math.mul(math.inverse(otherBodyWorldTransform), characterWorldTransform);

                        ColliderDistanceInput correctionInput = new ColliderDistanceInput(characterPhysicsCollider.Value, 1, characterRelativeToOther);
                        if (otherBody.Collider.AsPtr()->CalculateDistance(correctionInput, out DistanceHit correctionHit))
                        {
                            if(correctionHit.Distance > 0f)
                            { 
                                float3 reconstructedHitNormal = math.mul(otherBodyWorldTransform.rot, correctionHit.SurfaceNormal);
                                if (math.dot(-reconstructedHitNormal, -characterBody.GroundingUp) > 0f)
                                {
                                    float angleBetweenGroundingDownAndClosestPointOnOther = math.PI * 0.5f - MathUtilities.AngleRadians(-reconstructedHitNormal, -characterBody.GroundingUp);
                                    float sineAngle = math.sin(angleBetweenGroundingDownAndClosestPointOnOther);
                                    if(sineAngle > 0f)
                                    {
                                        float correctedDistance = correctionHit.Distance / math.sin(angleBetweenGroundingDownAndClosestPointOnOther);
                                        distanceToGround = math.clamp(correctedDistance, 0f, Constants.CollisionOffset);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles calculating forces resulting from character hits, and these forces may be applied both to the character or to the hit body.
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void ProcessCharacterHitDynamics<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            float3 characterPosition = LocalTransform.ValueRO.Position;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            
            baseContext.TmpRigidbodyIndexesProcessed.Clear();
            
            for (int b = 0; b < CharacterHitsBuffer.Length; b++)
            {
                KinematicCharacterHit characterHit = CharacterHitsBuffer[b];
                if (characterHit.RigidBodyIndex >= 0)
                {
                    int hitBodyIndex = characterHit.RigidBodyIndex;
                    RigidBody hitBody = baseContext.PhysicsWorld.Bodies[hitBodyIndex];
                    Entity hitBodyEntity = hitBody.Entity;

                    if (hitBodyEntity != characterBody.ParentEntity)
                    {
                        bool bodyHasPhysicsVelocityAndMass = PhysicsUtilities.DoesBodyHavePhysicsVelocityAndMass(in baseContext.PhysicsWorld, hitBodyIndex);
                        if (bodyHasPhysicsVelocityAndMass)
                        {
                            if (!baseContext.TmpRigidbodyIndexesProcessed.Contains(characterHit.RigidBodyIndex))
                            {
                                baseContext.TmpRigidbodyIndexesProcessed.Add(characterHit.RigidBodyIndex);

                                PhysicsVelocity selfPhysicsVelocity = new PhysicsVelocity { Linear = characterBody.RelativeVelocity + characterBody.ParentVelocity, Angular = default };
                                PhysicsMass selfPhysicsMass = PhysicsUtilities.GetKinematicCharacterPhysicsMass(characterProperties);
                                RigidTransform selfTransform = new RigidTransform(characterRotation, characterPosition);

                                // Compute other body's data depending on if it's a character or not
                                bool otherIsCharacter = false;
                                bool otherIsDynamic = false;
                                PhysicsVelocity otherPhysicsVelocity = new PhysicsVelocity();
                                PhysicsMass otherPhysicsMass = new PhysicsMass();
                                RigidTransform otherTransform = hitBody.WorldFromBody;
                                if (baseContext.StoredCharacterBodyPropertiesLookup.HasComponent(hitBodyEntity))
                                {
                                    StoredKinematicCharacterData data = baseContext.StoredCharacterBodyPropertiesLookup[hitBodyEntity];
                                    otherIsCharacter = true;
                                    otherIsDynamic = data.SimulateDynamicBody;
                                    otherPhysicsVelocity = new PhysicsVelocity { Linear = data.RelativeVelocity + data.ParentVelocity, Angular = float3.zero };
                                    otherPhysicsMass = PhysicsUtilities.GetKinematicCharacterPhysicsMass(baseContext.StoredCharacterBodyPropertiesLookup[hitBodyEntity]);
                                }
                                else if (PhysicsUtilities.DoesBodyHavePhysicsVelocityAndMass(in baseContext.PhysicsWorld, hitBodyIndex))
                                {
                                    PhysicsUtilities.GetBodyComponents(
                                        in baseContext.PhysicsWorld,
                                        hitBodyIndex,
                                        out LocalTransform transform,
                                        out otherPhysicsVelocity,
                                        out otherPhysicsMass);
                                    
                                    otherIsDynamic = otherPhysicsMass.InverseMass > 0f;
                                }

                                // Correct the normal of the hit based on grounding considerations
                                float3 effectiveHitNormalFromOtherToSelf = characterHit.Normal;
                                if (characterHit.WasCharacterGroundedOnHitEnter && !characterHit.IsGroundedOnHit)
                                {
                                    effectiveHitNormalFromOtherToSelf = math.normalizesafe(MathUtilities.ProjectOnPlane(characterHit.Normal, characterBody.GroundingUp));
                                }
                                else if (characterHit.IsGroundedOnHit)
                                {
                                    effectiveHitNormalFromOtherToSelf = characterBody.GroundingUp;
                                }
                                // Prevent a grounding-reoriented normal for dynamic bodies
                                if (otherIsDynamic && !characterHit.IsGroundedOnHit)
                                {
                                    effectiveHitNormalFromOtherToSelf = characterHit.Normal;
                                }

                                // Mass overrides
                                if (characterProperties.SimulateDynamicBody && otherIsDynamic && !otherIsCharacter)
                                {
                                    if (selfPhysicsMass.InverseMass > 0f && otherPhysicsMass.InverseMass > 0f)
                                    {
                                        processor.OverrideDynamicHitMasses(ref context, ref baseContext, ref selfPhysicsMass, ref otherPhysicsMass, new BasicHit(characterHit));
                                    }
                                }

                                // Special cases with kinematic VS kinematic
                                if (!characterProperties.SimulateDynamicBody && !otherIsDynamic)
                                {
                                    // Pretend we have a mass of 1 against a kinematic body
                                    selfPhysicsMass.InverseMass = 1f;

                                    // When other is kinematic character, cancel their velocity towards us if any, for the sake of impulse calculations. This prevents bumping
                                    if (otherIsCharacter && math.dot(otherPhysicsVelocity.Linear, effectiveHitNormalFromOtherToSelf) > 0f)
                                    {
                                        otherPhysicsVelocity.Linear = MathUtilities.ProjectOnPlane(otherPhysicsVelocity.Linear, effectiveHitNormalFromOtherToSelf);
                                    }
                                }

                                // Restore the portion of the character velocity that got lost during hit projection (so we can re-solve it with dynamics)
                                float3 velocityLostInOriginalProjection = math.projectsafe(characterHit.CharacterVelocityBeforeHit - characterHit.CharacterVelocityAfterHit, effectiveHitNormalFromOtherToSelf);
                                selfPhysicsVelocity.Linear += velocityLostInOriginalProjection;

                                // Solve impulses
                                PhysicsUtilities.SolveCollisionImpulses(
                                    selfPhysicsVelocity,
                                    otherPhysicsVelocity,
                                    selfPhysicsMass,
                                    otherPhysicsMass,
                                    selfTransform,
                                    otherTransform,
                                    characterHit.Position,
                                    effectiveHitNormalFromOtherToSelf,
                                    out float3 impulseOnSelf,
                                    out float3 impulseOnOther);

                                // Apply impulse to self
                                float3 previousCharacterLinearVel = selfPhysicsVelocity.Linear;
                                selfPhysicsVelocity.ApplyLinearImpulse(in selfPhysicsMass, impulseOnSelf);
                                float3 characterLinearVelocityChange = velocityLostInOriginalProjection + (selfPhysicsVelocity.Linear - previousCharacterLinearVel);
                                characterBody.RelativeVelocity += characterLinearVelocityChange;

                                // TODO: this ignores custom vel projection.... any alternatives?
                                // trim off any velocity that goes towards ground (prevents reoriented velocity issue)
                                if (characterHit.IsGroundedOnHit && math.dot(characterBody.RelativeVelocity, characterHit.Normal) < -Constants.DotProductSimilarityEpsilon)
                                {
                                    characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, characterBody.GroundingUp);
                                    characterBody.RelativeVelocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(characterBody.RelativeVelocity, characterHit.Normal, characterBody.GroundingUp);
                                }

                                // if a character is moving towards is, they will also solve the collision themselves in their own update. In order to prevent solving the coll twice, we won't apply any impulse on them in that case
                                bool otherIsCharacterMovingTowardsUs = otherIsCharacter && math.dot(otherPhysicsVelocity.Linear, effectiveHitNormalFromOtherToSelf) > Constants.DotProductSimilarityEpsilon;

                                // Apply velocity change on hit body (only if dynamic and not character. Characters will solve the impulse on themselves)
                                if (!otherIsCharacterMovingTowardsUs && otherIsDynamic && math.lengthsq(impulseOnOther) > 0f)
                                {
                                    float3 previousLinearVel = otherPhysicsVelocity.Linear;
                                    float3 previousAngularVel = otherPhysicsVelocity.Angular;

                                    otherPhysicsVelocity.ApplyImpulse(
                                        otherPhysicsMass,
                                        otherTransform.pos,
                                        otherTransform.rot,
                                        impulseOnOther,
                                        characterHit.Position);

                                    DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                                    {
                                        OnEntity = hitBodyEntity,
                                        LinearVelocityChange = otherPhysicsVelocity.Linear - previousLinearVel,
                                        AngularVelocityChange = otherPhysicsVelocity.Angular - previousAngularVel,
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles casting the character shape in the velocity direction/magnitude order to detect hits, projecting the character velocity on those hits, and moving the character.
        /// The process is repeated until no new hits are detected, or until a certain max amount of iterations is reached.
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="originalVelocityDirection"> Direction of the character velocity before any projection of velocity happened on this update </param>
        /// <param name="confirmedNoOverlapsOnLastMoveIteration"> Whether or not we can confirm that the character wasn't overlapping with any colliders after the last movement iteration. This is used for optimisation purposes as it gives us an opportunity to skip certain physics queries later in the character update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void MoveWithCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            float3 originalVelocityDirection,
            out bool confirmedNoOverlapsOnLastMoveIteration) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            confirmedNoOverlapsOnLastMoveIteration = false;
            
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            
            // Project on ground hit
            if(characterBody.IsGrounded)
            {
                ProjectVelocityOnGrounding(ref characterBody.RelativeVelocity, characterBody.GroundHit.Normal, characterBody.GroundingUp);
            }
            
            float remainingMovementLength = math.length(characterBody.RelativeVelocity) * baseContext.Time.DeltaTime;
            float3 remainingMovementDirection = math.normalizesafe(characterBody.RelativeVelocity);

            // Add all close distance hits to velocity projection hits buffer
            // Helps fix some tunneling issues with rotating character colliders
            if (characterProperties.DetectMovementCollisions && characterProperties.ProjectVelocityOnInitialOverlaps)
            {
                if (CalculateDistanceAllCollisions(
                    in processor,
                    ref context,
                    ref baseContext, 
                    characterPosition,
                    characterRotation,
                    0f,
                    characterProperties.ShouldIgnoreDynamicBodies(),
                    out NativeList<DistanceHit> overlapHits))
                {
                    for (int i = 0; i < overlapHits.Length; i++)
                    {
                        BasicHit movementHit = new BasicHit(overlapHits[i]);

                        if (math.dot(movementHit.Normal, characterBody.RelativeVelocity) < Constants.DotProductSimilarityEpsilon)
                        {
                            bool isGroundedOnTmpHit = false;
                            if (characterProperties.EvaluateGrounding)
                            {
                                isGroundedOnTmpHit = processor.IsGroundedOnHit(
                                    ref context, 
                                    ref baseContext,
                                    in movementHit, 
                                    (int)GroundingEvaluationType.InitialOverlaps);
                            }
                            
                            // Add hit to projection hits
                            KinematicCharacterHit currentCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(
                                in movementHit,
                                characterBody.IsGrounded,
                                characterBody.RelativeVelocity,
                                isGroundedOnTmpHit);
                            VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(currentCharacterHit));

                            processor.OnMovementHit(
                                ref context,
                                ref baseContext,
                                ref currentCharacterHit,
                                ref remainingMovementDirection,
                                ref remainingMovementLength,
                                originalVelocityDirection,
                                0f);

                            currentCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                            CharacterHitsBuffer.Add(currentCharacterHit);
                        }
                    }
                }
            }

            // Movement cast iterations
            if (characterProperties.DetectMovementCollisions)
            {
                int movementCastIterationsMade = 0;
                while (movementCastIterationsMade < characterProperties.MaxContinuousCollisionsIterations && remainingMovementLength > 0f)
                {
                    confirmedNoOverlapsOnLastMoveIteration = false;

                    float3 castStartPosition = characterPosition;
                    float3 castDirection = remainingMovementDirection;
                    float castLength = remainingMovementLength + Constants.CollisionOffset; // TODO: shoud we keep this offset?

                    // Cast collider for movement
                    ColliderCastInput castInput = new ColliderCastInput(characterPhysicsCollider.Value, castStartPosition, castStartPosition + (castDirection * castLength), characterRotation);
                    baseContext.TmpColliderCastHits.Clear();
                    AllHitsCollector<ColliderCastHit> collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
                    baseContext.PhysicsWorld.CastCollider(castInput, ref collector);
                    bool foundMovementHit = FilterColliderCastHitsForMove(in processor, ref context, ref baseContext, ref baseContext.TmpColliderCastHits, !characterProperties.SimulateDynamicBody, castDirection, Entity.Null, characterProperties.ShouldIgnoreDynamicBodies(), out ColliderCastHit closestHit, out bool foundAnyOverlaps);

                    if (!foundAnyOverlaps)
                    {
                        confirmedNoOverlapsOnLastMoveIteration = true;
                    }

                    if (foundMovementHit)
                    {
                        BasicHit movementHit = new BasicHit(closestHit);
                        float movementHitDistance = castLength * closestHit.Fraction;
                        movementHitDistance = math.max(0f, movementHitDistance - Constants.CollisionOffset);

                        bool isGroundedOnMovementHit = false;
                        if (characterProperties.EvaluateGrounding)
                        {
                            // Grounding calculation
                            isGroundedOnMovementHit = processor.IsGroundedOnHit(
                                ref context, 
                                ref baseContext,
                                in movementHit, 
                                (int)GroundingEvaluationType.MovementHit);
                        }

                        // Add hit to projection hits
                        KinematicCharacterHit currentCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(
                            in movementHit,
                            characterBody.IsGrounded,
                            characterBody.RelativeVelocity,
                            isGroundedOnMovementHit);

                        processor.OnMovementHit(
                            ref context,
                            ref baseContext,
                            ref currentCharacterHit,
                            ref remainingMovementDirection,
                            ref remainingMovementLength,
                            originalVelocityDirection,
                            movementHitDistance);

                        currentCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                        CharacterHitsBuffer.Add(currentCharacterHit);
                    }
                    // If no hits detected, just consume the rest of the movement, which will end the iterations
                    else
                    {
                        characterPosition += (remainingMovementDirection * remainingMovementLength);
                        remainingMovementLength = 0f;
                    }

                    movementCastIterationsMade++;
                }

                // If there is still movement left after all iterations (in other words; if we were not able to solve the movement completely)....
                if (remainingMovementLength > 0f)
                {
                    if (characterProperties.KillVelocityWhenExceedMaxIterations)
                    {
                        characterBody.RelativeVelocity = float3.zero;
                    }

                    if (!characterProperties.DiscardMovementWhenExceedMaxIterations)
                    {
                        characterPosition += (remainingMovementDirection * remainingMovementLength);
                    }
                }
            }
            else
            {
                characterPosition += characterBody.RelativeVelocity * baseContext.Time.DeltaTime;
            }
        }

        /// <summary>
        /// Handles the special case of projecting character velocity on a grounded hit, where the velocity magnitude is multiplied by a factor of 1 when it is parallel to the ground, and a factor of 0 when it is parallel to the character's "grounding up direction".
        /// </summary>
        /// <param name="velocity"> The velocity to project </param>
        /// <param name="groundNormal"> The detected ground normal </param>
        /// <param name="groundingUp"> The grounding up direction of the character </param>
        public void ProjectVelocityOnGrounding(ref float3 velocity, float3 groundNormal, float3 groundingUp)
        {
            // Make the velocity be 100% of its magnitude when it is perfectly parallel to ground, 0% when it is towards character up,
            // and interpolated when it's in-between those
            if (math.lengthsq(velocity) > 0f)
            {
                float velocityLength = math.length(velocity);
                float3 originalDirection = math.normalizesafe(velocity);
                float3 reorientedDirection = math.normalizesafe(MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, groundNormal, groundingUp));
                float dotOriginalWithUp = math.dot(originalDirection, groundingUp);
                float dotReorientedWithUp = math.dot(reorientedDirection, groundingUp);

                float ratioFromVerticalToSlopeDirection = 0f;
                // If velocity is going towards ground, interpolate between reoriented direction and down direction (-1f ratio with up)
                if (dotOriginalWithUp < dotReorientedWithUp)
                {
                    ratioFromVerticalToSlopeDirection = math.distance(dotOriginalWithUp, -1f) / math.distance(dotReorientedWithUp, -1f);
                }
                // If velocity is going towards air, interpolate between reoriented direction and up direction (1f ratio with up)
                else
                {
                    ratioFromVerticalToSlopeDirection = math.distance(dotOriginalWithUp, 1f) / math.distance(dotReorientedWithUp, 1f);
                }
                velocity = reorientedDirection * math.lerp(0f, velocityLength, ratioFromVerticalToSlopeDirection);
            }
        }

        /// <summary>
        /// Handles detecting current overlap hits, and decolliding the character from them
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="originalVelocityDirection"> Direction of the character velocity before any projection of velocity happened on this update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void SolveOverlaps<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            float3 originalVelocityDirection) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            baseContext.TmpRigidbodyIndexesProcessed.Clear();
            
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;

            int decollisionIterationsMade = 0;
            while (decollisionIterationsMade < characterProperties.MaxOverlapDecollisionIterations)
            {
                decollisionIterationsMade++;

                ColliderDistanceInput distanceInput = new ColliderDistanceInput(characterPhysicsCollider.Value, 0f, math.RigidTransform(characterRotation, characterPosition));
                baseContext.TmpDistanceHits.Clear();
                AllHitsCollector<DistanceHit> collector = new AllHitsCollector<DistanceHit>(distanceInput.MaxDistance, ref baseContext.TmpDistanceHits);
                baseContext.PhysicsWorld.CalculateDistance(distanceInput, ref collector);
                FilterDistanceHitsForSolveOverlaps(in processor, ref context, ref baseContext, ref baseContext.TmpDistanceHits, out DistanceHit mostPenetratingHit, out DistanceHit mostPenetratingDynamicHit, out DistanceHit mostPenetratingNonDynamicHit);

                bool foundHitForDecollision = false;

                // Dynamic mode
                if (characterProperties.SimulateDynamicBody)
                {
                    DistanceHit chosenDecollisionHit = default;
                    if (mostPenetratingNonDynamicHit.Distance < 0f)
                    {
                        chosenDecollisionHit = mostPenetratingNonDynamicHit; // assume we decollide from closest nondynamic hit by default
                    }
                    bool chosenHitIsDynamic = false;
                    bool isGroundedOnChosenHit = false;
                    bool calculatedChosenHitIsGrounded = false;

                    // Remember all dynamic bodies as hits and push back those that cause an obstructed collision
                    for (int i = 0; i < baseContext.TmpDistanceHits.Length; i++)
                    {
                        DistanceHit dynamicHit = baseContext.TmpDistanceHits[i];
                        BasicHit basicDynamicHit = new BasicHit(dynamicHit);

                        bool isGroundedOnHit = false;
                        if (characterProperties.EvaluateGrounding)
                        {
                            isGroundedOnHit = processor.IsGroundedOnHit(
                                ref context, 
                                ref baseContext,
                                in basicDynamicHit, 
                                (int)GroundingEvaluationType.OverlapDecollision);
                        }

                        // is this happens to be the most penetrating hit, remember as chosen hit
                        if (dynamicHit.RigidBodyIndex == mostPenetratingHit.RigidBodyIndex &&
                           dynamicHit.ColliderKey.Value == mostPenetratingHit.ColliderKey.Value)
                        {
                            chosenDecollisionHit = dynamicHit;

                            chosenHitIsDynamic = true;
                            isGroundedOnChosenHit = isGroundedOnHit;
                            calculatedChosenHitIsGrounded = true;
                        }
                    }

                    if (chosenDecollisionHit.Entity != Entity.Null)
                    {
                        BasicHit basicChosenHit = new BasicHit(chosenDecollisionHit);

                        if (!calculatedChosenHitIsGrounded)
                        {
                            if (characterProperties.EvaluateGrounding)
                            {
                                isGroundedOnChosenHit = processor.IsGroundedOnHit(
                                    ref context, 
                                    ref baseContext,
                                    in basicChosenHit, 
                                    (int)GroundingEvaluationType.OverlapDecollision);
                            }
                        }

                        DecollideFromHit(
                            in processor,
                            ref context,
                            ref baseContext, 
                            ref characterBody,
                            ref characterPosition,
                            in basicChosenHit,
                            -chosenDecollisionHit.Distance,
                            originalVelocityDirection,
                            characterProperties.SimulateDynamicBody,
                            isGroundedOnChosenHit,
                            chosenHitIsDynamic,
                            true,
                            true);
                        
                        foundHitForDecollision = true;
                    }
                }
                // Kinematic mode
                else
                {
                    bool foundValidNonDynamicHitToDecollideFrom = mostPenetratingNonDynamicHit.Entity != Entity.Null && mostPenetratingNonDynamicHit.Distance < 0f;
                    bool isLastIteration = !foundValidNonDynamicHitToDecollideFrom || decollisionIterationsMade >= characterProperties.MaxOverlapDecollisionIterations;

                    // Push back all dynamic bodies & remember as hits, but only on last iteration
                    if (isLastIteration)
                    {
                        for (int i = 0; i < baseContext.TmpDistanceHits.Length; i++)
                        {
                            DistanceHit dynamicHit = baseContext.TmpDistanceHits[i];
                            BasicHit basicDynamicHit = new BasicHit(dynamicHit);

                            // Add as character hit
                            KinematicCharacterHit ovelapHit = KinematicCharacterUtilities.CreateCharacterHit(
                                in basicDynamicHit,
                                characterBody.IsGrounded,
                                characterBody.RelativeVelocity,
                                false);
                            CharacterHitsBuffer.Add(ovelapHit);

                            // Add a position displacement impulse
                            if (!baseContext.TmpRigidbodyIndexesProcessed.Contains(dynamicHit.RigidBodyIndex))
                            {
                                baseContext.TmpRigidbodyIndexesProcessed.Add(dynamicHit.RigidBodyIndex);

                                DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                                {
                                    OnEntity = dynamicHit.Entity,
                                    Displacement = dynamicHit.SurfaceNormal * dynamicHit.Distance,
                                });
                            }
                        }
                    }

                    // Remember that we must decollide only from the closest nonDynamic hit, if any
                    if (foundValidNonDynamicHitToDecollideFrom)
                    {
                        BasicHit basicChosenHit = new BasicHit(mostPenetratingNonDynamicHit);

                        bool isGroundedOnHit = false;
                        if (characterProperties.EvaluateGrounding)
                        {
                            isGroundedOnHit = processor.IsGroundedOnHit(
                                ref context, 
                                ref baseContext,
                                in basicChosenHit, 
                                (int)GroundingEvaluationType.OverlapDecollision);
                        }

                        DecollideFromHit(
                            in processor,
                            ref context,
                            ref baseContext, 
                            ref characterBody,
                            ref characterPosition,
                            in basicChosenHit,
                            -mostPenetratingNonDynamicHit.Distance,
                            originalVelocityDirection,
                            characterProperties.SimulateDynamicBody,
                            isGroundedOnHit,
                            false,
                            true,
                            true);

                        foundHitForDecollision = true;
                    }
                }

                if (!foundHitForDecollision)
                {
                    // Early exit when found no hit to decollide from
                    break;
                }
            }
        }
        
        private void RecalculateDecollisionVector(ref float3 decollisionVector, float3 originalHitNormal, float3 newDecollisionDirection, float decollisionDistance)
        {
            float overlapDistance = math.max(decollisionDistance, 0f);
            if (overlapDistance > 0f)
            {
                decollisionVector += MathUtilities.ReverseProjectOnVector(originalHitNormal * overlapDistance, newDecollisionDirection, overlapDistance * Constants.DefaultReverseProjectionMaxLengthRatio);
            }
        }

        /// <summary>
        /// Decollides character from a specific hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="decollisionDistance"></param>
        /// <param name="originalVelocityDirection"> Direction of the character velocity before any projection of velocity happened on this update </param>
        /// <param name="characterSimulateDynamic"></param>
        /// <param name="isGroundedOnHit"></param>
        /// <param name="hitIsDynamic"></param>
        /// <param name="addToCharacterHits"></param>
        /// <param name="projectVelocityOnHit"></param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void DecollideFromHit<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            in BasicHit hit,
            float decollisionDistance,
            float3 originalVelocityDirection,
            bool characterSimulateDynamic,
            bool isGroundedOnHit,
            bool hitIsDynamic,
            bool addToCharacterHits,
            bool projectVelocityOnHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            
            // Grounding considerations for decollision (modified decollision direction)
            float3 decollisionDirection = hit.Normal;
            float3 decollisionVector = decollisionDirection * decollisionDistance;
            if (isGroundedOnHit)
            {
                if (isGroundedOnHit && math.dot(characterBody.GroundingUp, hit.Normal) > Constants.MinDotRatioForVerticalDecollision)
                {
                    // Always decollide vertically from grounded hits
                    decollisionDirection = characterBody.GroundingUp;
                    RecalculateDecollisionVector(ref decollisionVector, hit.Normal, decollisionDirection, decollisionDistance);
                }
                else if (characterBody.IsGrounded && !hitIsDynamic)
                {
                    // If we are grounded and hit is nongrounded, decollide horizontally on the plane of our ground normal
                    decollisionDirection = math.normalizesafe(MathUtilities.ProjectOnPlane(decollisionDirection, characterBody.GroundHit.Normal));
                    RecalculateDecollisionVector(ref decollisionVector, hit.Normal, decollisionDirection, decollisionDistance);
                }
            }

            // In simulateDynamic mode, before we decollide from a dynamic body, check if the decollision would be obstructed by anything other than the decollided body
            if (characterSimulateDynamic &&
                hitIsDynamic &&
                CastColliderClosestCollisions(
                    in processor,
                    ref context,
                    ref baseContext, 
                    characterPosition,
                    characterRotation,
                    decollisionDirection,
                    decollisionDistance,
                    true,
                    true,
                    out ColliderCastHit closestHit,
                    out float closestHitDistance) &&
                closestHit.Entity != hit.Entity)
            {
                // Move based on how far the obstruction was
                characterPosition += decollisionDirection * closestHitDistance;
                
                // Displacement impulse
                if (!baseContext.TmpRigidbodyIndexesProcessed.Contains(hit.RigidBodyIndex))
                {
                    baseContext.TmpRigidbodyIndexesProcessed.Add(hit.RigidBodyIndex);
                    DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                    {
                        OnEntity = hit.Entity,
                        Displacement = -hit.Normal * (decollisionDistance - closestHitDistance),
                    });
                }
            }
            // fully decollide otherwise
            else
            {
                characterPosition += decollisionVector;
            }

            // Velocity projection
            float3 characterRelativeVelocityBeforeProjection = characterBody.RelativeVelocity;
            if (projectVelocityOnHit)
            {
                VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(hit, isGroundedOnHit));

                // Project velocity on obstructing overlap
                if (math.dot(characterBody.RelativeVelocity, hit.Normal) < 0f)
                {
                    processor.ProjectVelocityOnHits(
                        ref context,
                        ref baseContext,
                        ref characterBody.RelativeVelocity,
                        ref characterBody.IsGrounded,
                        ref characterBody.GroundHit,
                        in VelocityProjectionHits,
                        originalVelocityDirection);
                }
            }

            // Add to character hits
            if (addToCharacterHits)
            {
                KinematicCharacterHit ovelapCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(
                    in hit,
                    characterBody.IsGrounded,
                    characterRelativeVelocityBeforeProjection,
                    isGroundedOnHit);
                ovelapCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                CharacterHitsBuffer.Add(ovelapCharacterHit);
            }
        }
        
        /// <summary>
        /// Determines if grounded status should be prevented, based on the velocity of the character as well as the velocity of the hit body, if any.
        /// </summary>
        /// <param name="physicsWorld"> The physics world in which the hit body exists </param>
        /// <param name="hit"> The hit to evaluate </param>
        /// <param name="wasGroundedBeforeCharacterUpdate"> Whether or not the character was grounded at the start of its update, before ground detection </param>
        /// <param name="relativeVelocity"> The relative velocity of the character</param>
        /// <returns> Whether or not grounding should be set to false </returns>
        public bool ShouldPreventGroundingBasedOnVelocity(
            in PhysicsWorld physicsWorld,
            in BasicHit hit,
            bool wasGroundedBeforeCharacterUpdate,
            float3 relativeVelocity)
        {
            // Prevent grounding if nongrounded and going away from ground normal
            // (this prevents snapping to ground when you are in air, going upwards, and hopping onto the side of a platform)
            if (!wasGroundedBeforeCharacterUpdate &&
                math.dot(relativeVelocity, hit.Normal) > Constants.DotProductSimilarityEpsilon &&
                math.lengthsq(relativeVelocity) > Constants.MinVelocityLengthSqForGroundingIgnoreCheck)
            {
                if (PhysicsUtilities.DoesBodyHavePhysicsVelocityAndMass(in physicsWorld, hit.RigidBodyIndex))
                {
                    PhysicsUtilities.GetBodyComponents(
                        in physicsWorld,
                        hit.RigidBodyIndex,
                        out LocalTransform hitTransform,
                        out PhysicsVelocity hitPhysicsVelocity,
                        out PhysicsMass hitPhysicsMass);
                    
                    float3 groundVelocityAtPoint = hitPhysicsVelocity.GetLinearVelocity(hitPhysicsMass, hitTransform.Position, hitTransform.Rotation, hit.Position);
                    
                    float characterVelocityOnNormal = math.dot(relativeVelocity, hit.Normal);
                    float groundVelocityOnNormal = math.dot(groundVelocityAtPoint, hit.Normal);

                    // Ignore grounding if our velocity is escaping the ground velocity
                    if (characterVelocityOnNormal > groundVelocityOnNormal)
                    {
                        return true;
                    }
                }
                else
                {
                    // If the ground has no velocity and our velocity is going away from it
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if step-handling considerations would make a character be grounded on a hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="maxStepHeight"> The maximum height that the character can step over </param>
        /// <param name="extraStepChecksDistance"> The horizontal distance at which extra downward step-detection raycasts will be made, in order to allow stepping over steps that are slightly angled </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not step-handling would make the character grounded on this hit </returns>
        public bool IsGroundedOnSteps<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit,
            float maxStepHeight,
            float extraStepChecksDistance) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            
            KinematicCharacterBody characterBody = CharacterBody.ValueRO;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            
            if (maxStepHeight > 0f)
            {
                bool isGroundedOnBackStep = false;
                bool isGroundedOnForwardStep = false;
                float3 backCheckDirection = math.normalizesafe(MathUtilities.ProjectOnPlane(hit.Normal, characterBody.GroundingUp));

                // Close back step hit
                bool backStepHitFound = RaycastClosestCollisions(
                    in processor,
                    ref context,
                    ref baseContext, 
                    hit.Position + (backCheckDirection * Constants.StepGroundingDetectionHorizontalOffset),
                    -characterBody.GroundingUp,
                    maxStepHeight,
                    characterProperties.ShouldIgnoreDynamicBodies(),
                    out RaycastHit backStepHit,
                    out float backHitDistance);
                if (backStepHitFound && backHitDistance > 0f)
                {
                    isGroundedOnBackStep = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, backStepHit.SurfaceNormal, characterBody.GroundingUp);
                }

                if (!isGroundedOnBackStep && extraStepChecksDistance > Constants.StepGroundingDetectionHorizontalOffset)
                {
                    // Extra back step hit
                    backStepHitFound = RaycastClosestCollisions(
                        in processor,
                        ref context,
                        ref baseContext, 
                        hit.Position + (backCheckDirection * extraStepChecksDistance),
                        -characterBody.GroundingUp,
                        maxStepHeight,
                        characterProperties.ShouldIgnoreDynamicBodies(),
                        out backStepHit,
                        out backHitDistance);
                    if (backStepHitFound && backHitDistance > 0f)
                    {
                        isGroundedOnBackStep = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, backStepHit.SurfaceNormal, characterBody.GroundingUp);
                    }
                }

                if (isGroundedOnBackStep)
                {
                    float forwardCheckHeight = maxStepHeight - backHitDistance;

                    // Detect forward obstruction
                    bool forwardStepHitFound = RaycastClosestCollisions(
                        in processor,
                        ref context,
                        ref baseContext,
                        hit.Position + (characterBody.GroundingUp * forwardCheckHeight),
                        -backCheckDirection,
                        Constants.StepGroundingDetectionHorizontalOffset,
                        characterProperties.ShouldIgnoreDynamicBodies(),
                        out RaycastHit forwardStepHit,
                        out float forwardHitDistance);
                    if (forwardStepHitFound && forwardHitDistance > 0f)
                    {
                        isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, forwardStepHit.SurfaceNormal, characterBody.GroundingUp);
                    }

                    if (!forwardStepHitFound)
                    {
                        // Close forward step hit
                        forwardStepHitFound = RaycastClosestCollisions(
                            in processor,
                            ref context,
                            ref baseContext,
                            hit.Position + (characterBody.GroundingUp * forwardCheckHeight) + (-backCheckDirection * Constants.StepGroundingDetectionHorizontalOffset),
                            -characterBody.GroundingUp,
                            maxStepHeight,
                            characterProperties.ShouldIgnoreDynamicBodies(),
                            out forwardStepHit,
                            out forwardHitDistance);
                        if (forwardStepHitFound && forwardHitDistance > 0f)
                        {
                            isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, forwardStepHit.SurfaceNormal, characterBody.GroundingUp);
                        }

                        if (!isGroundedOnForwardStep && extraStepChecksDistance > Constants.StepGroundingDetectionHorizontalOffset)
                        {
                            // Extra forward step hit obstruction
                            forwardStepHitFound = RaycastClosestCollisions(
                                in processor,
                                ref context,
                                ref baseContext,
                                hit.Position + (characterBody.GroundingUp * forwardCheckHeight),
                                -backCheckDirection,
                                extraStepChecksDistance,
                                characterProperties.ShouldIgnoreDynamicBodies(),
                                out forwardStepHit,
                                out forwardHitDistance);
                            if (forwardStepHitFound && forwardHitDistance > 0f)
                            {
                                isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, forwardStepHit.SurfaceNormal, characterBody.GroundingUp);
                            }

                            if (!forwardStepHitFound)
                            {
                                // Extra forward step hit
                                forwardStepHitFound = RaycastClosestCollisions(
                                    in processor,
                                    ref context,
                                    ref baseContext,
                                    hit.Position + (characterBody.GroundingUp * forwardCheckHeight) + (-backCheckDirection * extraStepChecksDistance),
                                    -characterBody.GroundingUp,
                                    maxStepHeight,
                                    characterProperties.ShouldIgnoreDynamicBodies(),
                                    out forwardStepHit,
                                    out forwardHitDistance);
                                if (forwardStepHitFound && forwardHitDistance > 0f)
                                {
                                    isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, forwardStepHit.SurfaceNormal, characterBody.GroundingUp);
                                }
                            }
                        }
                    }
                }

                return isGroundedOnBackStep && isGroundedOnForwardStep;
            }

            return false;
        }
        
        /// <summary>
        /// Handles the stepping-up-a-step logic during character movement iterations
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="remainingMovementDirection"></param>
        /// <param name="remainingMovementLength"></param>
        /// <param name="hitDistance"></param>
        /// <param name="stepHandling"></param>
        /// <param name="maxStepHeight"></param>
        /// <param name="characterWidthForStepGroundingCheck"> Character width used to determine grounding for steps. This is for cases where character with a spherical base tries to step onto an angled surface that is near the character's max step height. In thoses cases, the character might be grounded on steps on one frame, but wouldn't be grounded on the next frame as the spherical nature of its shape would push it a bit further up beyond its max step height. </param>
        /// <param name="hasSteppedUp"></param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void CheckForSteppingUpHit<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float hitDistance,
            bool stepHandling,
            float maxStepHeight,
            float characterWidthForStepGroundingCheck,
            out bool hasSteppedUp) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            hasSteppedUp = false;
            
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;

            // Step up hits (only needed if not grounded on that hit)
            if (characterProperties.EvaluateGrounding &&
                stepHandling &&
                !hit.IsGroundedOnHit &&
                maxStepHeight > 0f &&
                !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
            {
                float3 startPositionOfUpCheck = characterPosition;
                float3 upCheckDirection = characterBody.GroundingUp;
                float upCheckDistance = maxStepHeight;

                // Up cast
                bool foundUpStepHit = CastColliderClosestCollisions(
                    in processor,
                    ref context,
                    ref baseContext, 
                    startPositionOfUpCheck,
                    characterRotation,
                    upCheckDirection,
                    upCheckDistance,
                    true,
                    characterProperties.ShouldIgnoreDynamicBodies(),
                    out ColliderCastHit upStepHit,
                    out float upStepHitDistance);

                if (foundUpStepHit)
                {
                    upStepHitDistance = math.max(0f, upStepHitDistance - Constants.CollisionOffset);
                }
                else
                {
                    upStepHitDistance = upCheckDistance;
                }

                if (upStepHitDistance > 0f)
                {
                    float3 startPositionOfForwardCheck = startPositionOfUpCheck + (upCheckDirection * upStepHitDistance);
                    float distanceOverStep = math.length(math.projectsafe(remainingMovementDirection * (remainingMovementLength - hitDistance), hit.Normal));
                    float3 endPositionOfForwardCheck = startPositionOfForwardCheck + (remainingMovementDirection * (remainingMovementLength + Constants.CollisionOffset));
                    float minimumDistanceOverStep = Constants.CollisionOffset * 3f;
                    if (distanceOverStep < minimumDistanceOverStep)
                    {
                        endPositionOfForwardCheck += -hit.Normal * (minimumDistanceOverStep - distanceOverStep);
                    }
                    float3 forwardCheckDirection = math.normalizesafe(endPositionOfForwardCheck - startPositionOfForwardCheck);
                    float forwardCheckDistance = math.length(endPositionOfForwardCheck - startPositionOfForwardCheck);

                    // Forward cast
                    bool foundForwardStepHit = CastColliderClosestCollisions(
                        in processor,
                        ref context,
                        ref baseContext, 
                        startPositionOfForwardCheck,
                        characterRotation,
                        forwardCheckDirection,
                        forwardCheckDistance,
                        true,
                        characterProperties.ShouldIgnoreDynamicBodies(),
                        out ColliderCastHit forwardStepHit,
                        out float forwardStepHitDistance);

                    if (foundForwardStepHit)
                    {
                        forwardStepHitDistance = math.max(0f, forwardStepHitDistance - Constants.CollisionOffset);
                    }
                    else
                    {
                        forwardStepHitDistance = forwardCheckDistance;
                    }

                    if (forwardStepHitDistance > 0f)
                    {
                        float3 startPositionOfDownCheck = startPositionOfForwardCheck + (forwardCheckDirection * forwardStepHitDistance);
                        float3 downCheckDirection = -characterBody.GroundingUp;
                        float downCheckDistance = upStepHitDistance;

                        // Down cast
                        bool foundDownStepHit = CastColliderClosestCollisions(
                            in processor,
                            ref context,
                            ref baseContext, 
                            startPositionOfDownCheck,
                            characterRotation,
                            downCheckDirection,
                            downCheckDistance,
                            true,
                            characterProperties.ShouldIgnoreDynamicBodies(),
                            out ColliderCastHit downStepHit,
                            out float downStepHitDistance);

                        if (foundDownStepHit && downStepHitDistance > 0f)
                        {
                            BasicHit stepHit = new BasicHit(downStepHit);
                            bool isGroundedOnStepHit = false;
                            if (characterProperties.EvaluateGrounding)
                            {
                                isGroundedOnStepHit = processor.IsGroundedOnHit(
                                    ref context, 
                                    ref baseContext,
                                    in stepHit, 
                                    (int)GroundingEvaluationType.StepUpHit);
                            }

                            if (isGroundedOnStepHit)
                            {  
                                float hitHeight = upStepHitDistance - downStepHitDistance;
                                float steppedHeight = hitHeight;
                                steppedHeight = math.max(0f, steppedHeight + Constants.CollisionOffset);
                                    
                                // Add slope & character width consideration to stepped height
                                if(characterWidthForStepGroundingCheck > 0f)
                                {
                                    // Find the effective slope normal
                                    float3 forwardSlopeCheckDirection =  -math.normalizesafe(math.cross(math.cross(characterBody.GroundingUp, stepHit.Normal), stepHit.Normal));
                                    
                                    if (RaycastClosestCollisions(
                                            in processor,
                                            ref context,
                                            ref baseContext,
                                            stepHit.Position + (characterBody.GroundingUp * Constants.CollisionOffset) + (forwardSlopeCheckDirection * Constants.CollisionOffset),
                                            -characterBody.GroundingUp,
                                            maxStepHeight,
                                            characterProperties.ShouldIgnoreDynamicBodies(),
                                            out RaycastHit forwardSlopeCheckHit,
                                            out float forwardSlopeCheckHitDistance))
                                    {
                                        float3 effectiveSlopeNormal = forwardSlopeCheckHit.SurfaceNormal;
                                        float slopeRadians = MathUtilities.AngleRadians(characterBody.GroundingUp, effectiveSlopeNormal);
                                        float extraHeightFromAngleAndCharacterWidth = math.tan(slopeRadians) * characterWidthForStepGroundingCheck * 0.5f;
                                        steppedHeight += extraHeightFromAngleAndCharacterWidth;
                                    }
                                }
                                
                                if (steppedHeight < maxStepHeight)
                                {
                                    // Step up
                                    characterPosition += characterBody.GroundingUp * hitHeight;
                                    characterPosition += forwardCheckDirection * forwardStepHitDistance;

                                    characterBody.IsGrounded = true;
                                    characterBody.GroundHit = stepHit;

                                    // Project vel
                                    float3 characterVelocityBeforeHit = characterBody.RelativeVelocity;
                                    characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, characterBody.GroundingUp);
                                    remainingMovementDirection = math.normalizesafe(characterBody.RelativeVelocity);
                                    remainingMovementLength -= forwardStepHitDistance;
                                    
                                    // Replace hit with step hit
                                    hit = KinematicCharacterUtilities.CreateCharacterHit(stepHit, characterBody.IsGrounded, characterVelocityBeforeHit, isGroundedOnStepHit);
                                    hit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;

                                    hasSteppedUp = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Detects how the ground slope will change over the next character update, based on current character velocity
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="verticalOffset"> Vertical upwards distance where detection raycasts will begin </param>
        /// <param name="downDetectionDepth"> Distance of downwards slope detection raycasts </param>
        /// <param name="deltaTimeIntoFuture"> Time delta into future to detect slopes at with the current character velocity </param>
        /// <param name="secondaryNoGroundingCheckDistance"> Extra horizontal raycast distance for a secondary slope detection raycast </param>
        /// <param name="stepHandling"> Whether step-handling is enabled or not </param>
        /// <param name="maxStepHeight"> Maximum height of steps that can be stepped on </param>
        /// <param name="isMovingTowardsNoGrounding"> Whether or not the character is moving towards a place where it wouldn't be grounded </param>
        /// <param name="foundSlopeHit"> Whether or not we found a slope hit in the future </param>
        /// <param name="futureSlopeChangeAnglesRadians"> The detected slope angle change (in radians) in the future </param>
        /// <param name="futureSlopeHit"> The detected slope hit in the future </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void DetectFutureSlopeChange<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float verticalOffset,
            float downDetectionDepth,
            float deltaTimeIntoFuture,
            float secondaryNoGroundingCheckDistance,
            bool stepHandling, 
            float maxStepHeight,
            out bool isMovingTowardsNoGrounding,
            out bool foundSlopeHit,
            out float futureSlopeChangeAnglesRadians,
            out RaycastHit futureSlopeHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            isMovingTowardsNoGrounding = false;
            foundSlopeHit = false;
            futureSlopeChangeAnglesRadians = 0f;
            futureSlopeHit = default;

            KinematicCharacterBody characterBody = CharacterBody.ValueRO; 
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            
            if (IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, characterBody.GroundHit.Normal, characterBody.GroundingUp))
            {
                if(stepHandling)
                {
                    downDetectionDepth = math.max(maxStepHeight, downDetectionDepth) + verticalOffset;
                }
                else
                {
                    downDetectionDepth = downDetectionDepth + verticalOffset;
                }

                float3 velocityDirection = math.normalizesafe(characterBody.RelativeVelocity);
                float3 rayStartPoint = characterBody.GroundHit.Position + (characterBody.GroundingUp * verticalOffset);
                float3 rayDirection = velocityDirection;
                float rayLength = math.length(characterBody.RelativeVelocity * deltaTimeIntoFuture);

                if (rayLength > math.EPSILON)
                {
                    // Raycast forward 
                    bool forwardHitFound = RaycastClosestCollisions(
                        in processor,
                        ref context,
                        ref baseContext, 
                        rayStartPoint,
                        rayDirection,
                        rayLength,
                        characterProperties.ShouldIgnoreDynamicBodies(),
                        out RaycastHit forwardHit,
                        out float forwardHitDistance);

                    if (forwardHitFound)
                    {
                        foundSlopeHit = true;
                        futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(characterBody.GroundHit.Normal, forwardHit.SurfaceNormal, velocityDirection, characterBody.GroundingUp);
                        futureSlopeHit = forwardHit;
                    }
                    else
                    {
                        rayStartPoint = rayStartPoint + (rayDirection * rayLength);
                        rayDirection = -characterBody.GroundingUp;
                        rayLength = downDetectionDepth;

                        // Raycast down 
                        bool downHitFound = RaycastClosestCollisions(
                            in processor,
                            ref context,
                            ref baseContext, 
                            rayStartPoint,
                            rayDirection,
                            rayLength,
                            characterProperties.ShouldIgnoreDynamicBodies(),
                            out RaycastHit downHit,
                            out float downHitDistance);

                        if (downHitFound)
                        {
                            foundSlopeHit = true;
                            futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(characterBody.GroundHit.Normal, downHit.SurfaceNormal, velocityDirection, characterBody.GroundingUp);
                            futureSlopeHit = downHit;

                            if (!IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, downHit.SurfaceNormal, characterBody.GroundingUp))
                            {
                                isMovingTowardsNoGrounding = true;
                            }
                        }
                        else
                        {
                            isMovingTowardsNoGrounding = true;
                        }

                        if(isMovingTowardsNoGrounding)
                        {
                            rayStartPoint += velocityDirection * secondaryNoGroundingCheckDistance;

                            // Raycast down (secondary)
                            bool secondDownHitFound = RaycastClosestCollisions(
                                in processor,
                                ref context,
                                ref baseContext, 
                                rayStartPoint,
                                rayDirection,
                                rayLength,
                                characterProperties.ShouldIgnoreDynamicBodies(),
                                out RaycastHit secondDownHit,
                                out float secondDownHitDistance);

                            if (secondDownHitFound)
                            {
                                if (!foundSlopeHit)
                                {
                                    foundSlopeHit = true;
                                    futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(characterBody.GroundHit.Normal, secondDownHit.SurfaceNormal, velocityDirection, characterBody.GroundingUp);
                                    futureSlopeHit = secondDownHit;
                                }

                                if (IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, secondDownHit.SurfaceNormal, characterBody.GroundingUp))
                                {
                                    isMovingTowardsNoGrounding = false;
                                }
                            }
                            else
                            {
                                rayStartPoint += rayDirection * rayLength;
                                rayDirection = -velocityDirection;
                                rayLength = math.length(characterBody.RelativeVelocity * deltaTimeIntoFuture) + secondaryNoGroundingCheckDistance;

                                // Raycast backward
                                bool backHitFound = RaycastClosestCollisions(
                                    in processor,
                                    ref context,
                                    ref baseContext, 
                                    rayStartPoint,
                                    rayDirection,
                                    rayLength,
                                    characterProperties.ShouldIgnoreDynamicBodies(),
                                    out RaycastHit backHit,
                                    out float backHitDistance); 
                                
                                if (backHitFound)
                                {
                                    foundSlopeHit = true;
                                    futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(characterBody.GroundHit.Normal, backHit.SurfaceNormal, velocityDirection, characterBody.GroundingUp);
                                    futureSlopeHit = backHit;

                                    if (IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, backHit.SurfaceNormal, characterBody.GroundingUp))
                                    {
                                        isMovingTowardsNoGrounding = false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Determines if the slope angle is within grounded tolerance
        /// </summary>
        /// <param name="maxGroundedSlopeDotProduct"> Dot product between grounding up and maximum slope normal direction </param>
        /// <param name="slopeSurfaceNormal"> Evaluated slope normal </param>
        /// <param name="groundingUp"> Character's grounding up </param>
        /// <returns> Whether or not the character can be grounded on this slope </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsGroundedOnSlopeNormal(
            float maxGroundedSlopeDotProduct,
            float3 slopeSurfaceNormal,
            float3 groundingUp)
        {
            return math.dot(groundingUp, slopeSurfaceNormal) > maxGroundedSlopeDotProduct;
        }

        /// <summary>
        /// Determines if the character movement collision detection would detect non-grounded obstructions with the designated movement vector
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="movement"> The movement vector of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not a non-grounded obstruction would be hit with the designated movement </returns>
        public bool MovementWouldHitNonGroundedObstruction<T, C>(
            in T processor, 
            ref C context, 
            ref KinematicCharacterUpdateContext baseContext,
            float3 movement,
            out ColliderCastHit hit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            hit = default;
            
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
            float3 characterPosition = LocalTransform.ValueRO.Position;
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            
            if (CastColliderClosestCollisions(
                in processor,
                ref context,
                ref baseContext, 
                characterPosition,
                characterRotation,
                math.normalizesafe(movement),
                math.length(movement),
                true,
                characterProperties.ShouldIgnoreDynamicBodies(),
                out hit,
                out float hitDistance))
            {
                if (characterProperties.EvaluateGrounding)
                {
                    if (!processor.IsGroundedOnHit(
                            ref context, 
                            ref baseContext,
                            new BasicHit(hit), 
                            (int)GroundingEvaluationType.Default))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Called on every character physics update in order to set a parent body for the character
        /// </summary>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="parentEntity"> The parent entity of the character </param>
        /// <param name="anchorPointLocalParentSpace"> The contact point between character and parent, in the parent's local space, around which the character will be rotated </param>
        public void SetOrUpdateParentBody(
            ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            Entity parentEntity,
            float3 anchorPointLocalParentSpace)
        {
            if (parentEntity != Entity.Null && baseContext.TrackedTransformLookup.HasComponent(parentEntity))
            {
                characterBody.ParentEntity = parentEntity;
                characterBody.ParentLocalAnchorPoint = anchorPointLocalParentSpace;
            }
            else
            {
                characterBody.ParentEntity = Entity.Null;
                characterBody.ParentLocalAnchorPoint = default;
            }
        }
        
        /// <summary>
        /// Determines the effective signed slope angle of a hit based on character movement direction (negative sign means downward)
        /// </summary>
        /// <param name="currentGroundUp"> Current ground hit normal </param>
        /// <param name="hitNormal"> Evaluated hit normal </param>
        /// <param name="velocityDirection"> Direction of the character's velocity </param>
        /// <param name="groundingUp"> Grounding up of the character </param>
        /// <returns> The signed slope angle of the hit in the character's movement direction </returns>
        public float CalculateAngleOfHitWithGroundUp(float3 currentGroundUp, float3 hitNormal, float3 velocityDirection, float3 groundingUp)
        {
            float3 velocityRight = math.normalizesafe(math.cross(velocityDirection, -groundingUp));
            float3 currentGroundNormalOnPlane = MathUtilities.ProjectOnPlane(currentGroundUp, velocityRight);
            float3 downHitNormalOnPlane = MathUtilities.ProjectOnPlane(hitNormal, velocityRight);
            float slopeChangeAnglesRadians = MathUtilities.AngleRadians(currentGroundNormalOnPlane, downHitNormalOnPlane);

            // invert angle sign if it's a downward slope change
            if(math.dot(currentGroundNormalOnPlane, velocityDirection) < math.dot(downHitNormalOnPlane, velocityDirection))
            {
                slopeChangeAnglesRadians *= -1;
            }

            return slopeChangeAnglesRadians;
        }

        /// <summary>
        /// Default implementation of the "IsGroundedOnHit" processor callback. Calls default grounding evaluation for a hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="stepAndSlopeHandling"> Whether or not step-handling is enabled </param>
        /// <param name="groundingEvaluationType"> Identifier for the type of grounding evaluation that's being requested </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not the character is grounded on the hit </returns>
        public bool Default_IsGroundedOnHit<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            in BasicHit hit,
            in BasicStepAndSlopeHandlingParameters stepAndSlopeHandling,
            int groundingEvaluationType) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            KinematicCharacterProperties characterProperties = CharacterProperties.ValueRO;
            KinematicCharacterBody characterBody = CharacterBody.ValueRO;
            
            PhysicsWorld physicsWorld = baseContext.PhysicsWorld;
            if (ShouldPreventGroundingBasedOnVelocity(in physicsWorld, in hit, characterBody.WasGroundedBeforeCharacterUpdate, characterBody.RelativeVelocity))
            {
                return false;
            }

            bool isGroundedOnSlope = IsGroundedOnSlopeNormal(characterProperties.MaxGroundedSlopeDotProduct, hit.Normal, characterBody.GroundingUp);

            // Handle detecting grounding on step edges if not grounded on slope
            bool isGroundedOnSteps = false;
            if (!isGroundedOnSlope && stepAndSlopeHandling.StepHandling && stepAndSlopeHandling.MaxStepHeight > 0f)
            {
                bool hitIsOnCharacterBottom = math.dot(characterBody.GroundingUp, hit.Normal) > Constants.DotProductSimilarityEpsilon;
                if (hitIsOnCharacterBottom &&
                    (groundingEvaluationType == (int)GroundingEvaluationType.GroundProbing || groundingEvaluationType == (int)GroundingEvaluationType.StepUpHit))
                {
                    // Prevent step grounding detection on dynamic bodies, to prevent cases of character stepping onto sphere rolling towards it
                    if (!PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        isGroundedOnSteps = IsGroundedOnSteps(
                            in processor,
                            ref context,
                            ref baseContext,
                            in hit,
                            stepAndSlopeHandling.MaxStepHeight,
                            stepAndSlopeHandling.ExtraStepChecksDistance);
                    }
                }
            }

            return isGroundedOnSlope || isGroundedOnSteps;
        }
        
        /// <summary>
        /// Default implementation of the "UpdateGroundingUp" processor callback. Sets the character ground up to the character transform's up direction
        /// </summary>
        /// <param name="characterBody"> The character body component </param>
        public void Default_UpdateGroundingUp(ref KinematicCharacterBody characterBody)
        {
            quaternion characterRotation = LocalTransform.ValueRO.Rotation;
        
            // GroundingUp must be a normalized vector representing the "up" direction that we use to evaluate slope angles with.
            // By default this is the up direction of the character transform
            characterBody.GroundingUp = MathUtilities.GetUpFromRotation(characterRotation);
        }

        /// <summary>
        /// Default implementation of the "ProjectVelocityOnHits" processor callback. Projects velocity based on grounding considerations
        /// </summary>
        /// <param name="velocity"> Character velocity </param>
        /// <param name="characterIsGrounded"> Whether character is grounded or not </param>
        /// <param name="characterGroundHit"> The ground hit of the character </param>
        /// <param name="velocityProjectionHits"> List of hits that the velocity must be projected on, from oldest to most recent </param>
        /// <param name="originalVelocityDirection"> Original character velocity direction before any projection happened </param>
        /// <param name="constrainToGoundPlane"> Whether or not to constrain </param>
        public void Default_ProjectVelocityOnHits(
            ref float3 velocity,
            ref bool characterIsGrounded,
            ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
            float3 originalVelocityDirection,
            bool constrainToGoundPlane)
        {
            bool IsSamePlane(float3 planeA, float3 planeB)
            {
                return math.dot(planeA, planeB) > (1f - Constants.DotProductSimilarityEpsilon);
            }

            void ProjectVelocityOnSingleHit(ref float3 velocity, ref bool characterIsGrounded, ref BasicHit characterGroundHit, in KinematicVelocityProjectionHit hit, float3 groundingUp)
            {
                if (characterIsGrounded)
                {
                    if (hit.IsGroundedOnHit)
                    {
                        // Simply reorient velocity
                        velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, hit.Normal, groundingUp);
                    }
                    else
                    {
                        if (constrainToGoundPlane)
                        {
                            // Project velocity on crease formed between ground normal and obstruction
                            float3 groundedCreaseDirection = math.normalizesafe(math.cross(characterGroundHit.Normal, hit.Normal));
                            velocity = math.projectsafe(velocity, groundedCreaseDirection);
                        }
                        else
                        {
                            // Regular projection
                            velocity = MathUtilities.ProjectOnPlane(velocity, hit.Normal);
                        }
                    }
                }
                else
                {
                    if (hit.IsGroundedOnHit)
                    {
                        // Handle grounded landing
                        velocity = MathUtilities.ProjectOnPlane(velocity, groundingUp);
                        velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, hit.Normal, groundingUp);
                    }
                    else
                    {
                        // Regular projection
                        velocity = MathUtilities.ProjectOnPlane(velocity, hit.Normal);
                    }
                }

                // Replace grounding when the hit is grounded (or when not trying to constrain movement to ground plane
                if (hit.IsGroundedOnHit || !constrainToGoundPlane)
                {
                    // This could be a virtual hit, so make sure to only count it if it has a valid rigidbody
                    if (hit.RigidBodyIndex >= 0)
                    {
                        // make sure to only count as ground if the normal is pointing up
                        if (math.dot(groundingUp, hit.Normal) > Constants.DotProductSimilarityEpsilon)
                        {
                            characterIsGrounded = hit.IsGroundedOnHit;
                            characterGroundHit = new BasicHit(hit);
                        }
                    }
                }
            }

            if (math.lengthsq(velocity) <= 0f || math.lengthsq(originalVelocityDirection) <= 0f)
            {
                return;
            }

            KinematicCharacterBody characterBody = CharacterBody.ValueRO;

            int hitsCount = velocityProjectionHits.Length;
            int firstHitIndex = velocityProjectionHits.Length - 1;
            KinematicVelocityProjectionHit firstHit = velocityProjectionHits[firstHitIndex];
            float3 velocityDirection = math.normalizesafe(velocity);

            if (math.dot(velocityDirection, firstHit.Normal) < 0f)
            {
                // Project on first plane
                ProjectVelocityOnSingleHit(ref velocity, ref characterIsGrounded, ref characterGroundHit, in firstHit, characterBody.GroundingUp);
                velocityDirection = math.normalizesafe(velocity);

                // Original velocity direction will act as a plane constraint just like other hits, to prevent our velocity from going back the way it came from. Hit index -1 represents original velocity
                KinematicVelocityProjectionHit originalVelocityHit = default;
                originalVelocityHit.Normal = characterIsGrounded ? math.normalizesafe(MathUtilities.ProjectOnPlane(originalVelocityDirection, characterBody.GroundingUp)) : originalVelocityDirection;

                // Detect creases and corners by observing how the projected velocity would interact with previously-detected planes
                for (int secondHitIndex = -1; secondHitIndex < hitsCount; secondHitIndex++)
                {
                    if (secondHitIndex == firstHitIndex)
                        continue;

                    KinematicVelocityProjectionHit secondHit = originalVelocityHit;
                    if (secondHitIndex >= 0)
                    {
                        secondHit = velocityProjectionHits[secondHitIndex];
                    }

                    if (IsSamePlane(firstHit.Normal, secondHit.Normal))
                        continue;

                    if (math.dot(velocityDirection, secondHit.Normal) > -Constants.DotProductSimilarityEpsilon)
                        continue;

                    // Project on second plane
                    ProjectVelocityOnSingleHit(ref velocity, ref characterIsGrounded, ref characterGroundHit, in secondHit, characterBody.GroundingUp);
                    velocityDirection = math.normalizesafe(velocity);

                    // If the velocity projected on second plane goes back in first plane, it's a crease
                    if (math.dot(velocityDirection, firstHit.Normal) > -Constants.DotProductSimilarityEpsilon)
                        continue;

                    // Special case corner detection when grounded: if crease is made out of 2 non-grounded planes; it's a corner
                    if (characterIsGrounded && !firstHit.IsGroundedOnHit && !secondHit.IsGroundedOnHit)
                    {
                        velocity = default;
                        break;
                    }
                    else
                    {
                        // Velocity projection on crease
                        float3 creaseDirection = math.normalizesafe(math.cross(firstHit.Normal, secondHit.Normal));
                        if (secondHit.IsGroundedOnHit)
                        {
                            velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, secondHit.Normal, characterBody.GroundingUp);
                        }
                        velocity = math.projectsafe(velocity, creaseDirection);
                        velocityDirection = math.normalizesafe(velocity);
                    }

                    // Corner detection: see if projected velocity would enter back a third plane we already detected
                    for (int thirdHitIndex = -1; thirdHitIndex < hitsCount; thirdHitIndex++)
                    {
                        if (thirdHitIndex == firstHitIndex && thirdHitIndex == secondHitIndex)
                            continue;

                        KinematicVelocityProjectionHit thirdHit = originalVelocityHit;
                        if (thirdHitIndex >= 0)
                        {
                            thirdHit = velocityProjectionHits[thirdHitIndex];
                        }

                        if (IsSamePlane(firstHit.Normal, thirdHit.Normal) || IsSamePlane(secondHit.Normal, thirdHit.Normal))
                            continue;

                        if (math.dot(velocityDirection, thirdHit.Normal) < -Constants.DotProductSimilarityEpsilon)
                        {
                            // Velocity projection on corner
                            velocity = default;
                            break;
                        }
                    }

                    if (math.lengthsq(velocity) <= math.EPSILON)
                    {
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Default implementation of the "OnMovementHit" processor callback
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="remainingMovementDirection"> Direction of the character movement that's left to be processed </param>
        /// <param name="remainingMovementLength"> Magnitude of the character movement that's left to be processed </param>
        /// <param name="originalVelocityDirection"> Original character velocity direction before any projection happened </param>
        /// <param name="movementHitDistance"> Distance of the hit </param>
        /// <param name="stepHandling"> Whether step-handling is enabled or not </param>
        /// <param name="maxStepHeight"> Maximum height of steps that can be stepped on </param>
        /// <param name="characterWidthForStepGroundingCheck"> Character width used to determine grounding for steps. This is for cases where character with a spherical base tries to step onto an angled surface that is near the character's max step height. In thoses cases, the character might be grounded on steps on one frame, but wouldn't be grounded on the next frame as the spherical nature of its shape would push it a bit further up beyond its max step height. </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void Default_OnMovementHit<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float movementHitDistance,
            bool stepHandling,
            float maxStepHeight,
            float characterWidthForStepGroundingCheck = 0f) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            bool hasSteppedUp = false;

            if (stepHandling && 
                !hit.IsGroundedOnHit &&
                math.dot(math.normalizesafe(characterBody.RelativeVelocity), characterBody.GroundingUp) > Constants.MinVelocityDotRatioWithGroundingUpForSteppingUpHits)
            {
                CheckForSteppingUpHit(
                    in processor,
                    ref context,
                    ref baseContext, 
                    ref characterBody,
                    ref characterPosition,
                    ref hit,
                    ref remainingMovementDirection,
                    ref remainingMovementLength,
                    movementHitDistance,
                    stepHandling,
                    maxStepHeight,
                    characterWidthForStepGroundingCheck,
                    out hasSteppedUp);
            }
            
            // Add velocityProjection hits only after potential correction from step handling
            VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(hit));

            if (!hasSteppedUp)
            {
                // Advance position to closest hit
                characterPosition += remainingMovementDirection * movementHitDistance;
                remainingMovementLength -= movementHitDistance;

                // Project velocity
                float3 velocityBeforeProjection = characterBody.RelativeVelocity;

                processor.ProjectVelocityOnHits(
                    ref context,
                    ref baseContext,
                    ref characterBody.RelativeVelocity,
                    ref characterBody.IsGrounded,
                    ref characterBody.GroundHit,
                    in VelocityProjectionHits,
                    originalVelocityDirection);
                
                // Recalculate remaining movement after projection
                float projectedVelocityLengthFactor = math.length(characterBody.RelativeVelocity) / math.length(velocityBeforeProjection);
                remainingMovementLength *= projectedVelocityLengthFactor;
                remainingMovementDirection = math.normalizesafe(characterBody.RelativeVelocity);
            }
        }
        
        /// <summary>
        /// Casts the character collider and only returns the closest collideable hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hit"> The closest detected hit </param>
        /// <param name="hitDistance"> The distance of the closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected</returns>
        public bool CastColliderClosestCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float3 characterPosition,
            quaternion characterRotation,
            float3 direction,
            float length,
            bool onlyObstructingHits,
            bool ignoreDynamicBodies,
            out ColliderCastHit hit,
            out float hitDistance) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            
            ColliderCastInput castInput = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition, characterPosition + (direction * length), characterRotation);
            baseContext.TmpColliderCastHits.Clear();
            AllHitsCollector<ColliderCastHit> collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
            baseContext.PhysicsWorld.CastCollider(castInput, ref collector);
            if (FilterColliderCastHitsForClosestCollisions(in processor, ref context, ref baseContext, ref baseContext.TmpColliderCastHits, onlyObstructingHits, direction, ignoreDynamicBodies, out ColliderCastHit closestHit))
            {
                hit = closestHit;
                hitDistance = length * hit.Fraction;
                return true;
            }

            hit = default;
            hitDistance = default;
            return false;
        }

        /// <summary>
        /// Casts the character collider and returns all collideable hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hits"> All valid detected hits </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected </returns>
        public bool CastColliderAllCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float3 characterPosition,
            quaternion characterRotation,
            float3 direction,
            float length,
            bool onlyObstructingHits,
            bool ignoreDynamicBodies,
            out NativeList<ColliderCastHit> hits) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            hits = baseContext.TmpColliderCastHits;

            ColliderCastInput castInput = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition, characterPosition + (direction * length), characterRotation);
            baseContext.TmpColliderCastHits.Clear();
            AllHitsCollector<ColliderCastHit> collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
            baseContext.PhysicsWorld.CastCollider(castInput, ref collector);
            if (FilterColliderCastHitsForAllCollisions(in processor, ref context, ref baseContext, ref baseContext.TmpColliderCastHits, onlyObstructingHits, direction, ignoreDynamicBodies))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Casts a ray and only returns the closest collideable hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="startPoint"> The cast start point </param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hit"> The detected hit </param>
        /// <param name="hitDistance"> The distance of the detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected </returns>
        public bool RaycastClosestCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float3 startPoint,
            float3 direction,
            float length,
            bool ignoreDynamicBodies,
            out RaycastHit hit,
            out float hitDistance) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            
            RaycastInput castInput = new RaycastInput
            {
                Start = startPoint,
                End = startPoint + (direction * length),
                Filter = characterPhysicsCollider.Value.Value.GetCollisionFilter(),
            };
            baseContext.TmpRaycastHits.Clear();
            AllHitsCollector<RaycastHit> collector = new AllHitsCollector<RaycastHit>(1f, ref baseContext.TmpRaycastHits);
            baseContext.PhysicsWorld.CastRay(castInput, ref collector);
            if (FilterRaycastHitsForClosestCollisions(in processor, ref context, ref baseContext, ref baseContext.TmpRaycastHits, ignoreDynamicBodies, out RaycastHit closestHit))
            {
                hit = closestHit;
                hitDistance = length * hit.Fraction;
                return true;
            }

            hit = default;
            hitDistance = default;
            return false;
        }


        /// <summary>
        /// Casts a ray and returns all collideable hits 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="startPoint"> The cast start point </param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hits"> The detected hits </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected </returns>
        public bool RaycastAllCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float3 startPoint,
            float3 direction,
            float length,
            bool ignoreDynamicBodies,
            out NativeList<RaycastHit> hits) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            hits = baseContext.TmpRaycastHits;

            RaycastInput castInput = new RaycastInput
            {
                Start = startPoint,
                End = startPoint + (direction * length),
                Filter = characterPhysicsCollider.Value.Value.GetCollisionFilter(),
            };
            baseContext.TmpRaycastHits.Clear();
            AllHitsCollector<RaycastHit> collector = new AllHitsCollector<RaycastHit>(1f, ref baseContext.TmpRaycastHits);
            baseContext.PhysicsWorld.CastRay(castInput, ref collector);
            if (FilterRaycastHitsForAllCollisions(in processor, ref context, ref baseContext, ref baseContext.TmpRaycastHits, ignoreDynamicBodies))
            {
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Calculates distance from the character collider and only returns the closest collideable hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="maxDistance"> The direction of the case </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hit"> The closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected</returns>
        public bool CalculateDistanceClosestCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float3 characterPosition,
            quaternion characterRotation,
            float maxDistance,
            bool ignoreDynamicBodies,
            out DistanceHit hit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            
            ColliderDistanceInput distanceInput = new ColliderDistanceInput(characterPhysicsCollider.Value, maxDistance, math.RigidTransform(characterRotation, characterPosition));
            baseContext.TmpDistanceHits.Clear();
            AllHitsCollector<DistanceHit> collector = new AllHitsCollector<DistanceHit>(distanceInput.MaxDistance, ref baseContext.TmpDistanceHits);
            baseContext.PhysicsWorld.CalculateDistance(distanceInput, ref collector);
            if (FilterDistanceHitsForClosestCollisions(in processor, ref context, ref baseContext, ref baseContext.TmpDistanceHits, ignoreDynamicBodies, out DistanceHit closestHit))
            {
                hit = closestHit;
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// Calculates distance from the character collider and only returns all collideable hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="maxDistance"> The direction of the case </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hits"> The detected hits </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected</returns>
        public bool CalculateDistanceAllCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float3 characterPosition,
            quaternion characterRotation,
            float maxDistance,
            bool ignoreDynamicBodies,
            out NativeList<DistanceHit> hits) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            PhysicsCollider characterPhysicsCollider = PhysicsCollider.ValueRO;
            hits = baseContext.TmpDistanceHits;

            ColliderDistanceInput distanceInput = new ColliderDistanceInput(characterPhysicsCollider.Value, maxDistance, math.RigidTransform(characterRotation, characterPosition));
            baseContext.TmpDistanceHits.Clear();
            AllHitsCollector<DistanceHit> collector = new AllHitsCollector<DistanceHit>(distanceInput.MaxDistance, ref baseContext.TmpDistanceHits);
            baseContext.PhysicsWorld.CalculateDistance(distanceInput, ref collector);
            if (FilterDistanceHitsForAllCollisions(in processor, ref context, ref baseContext, ref baseContext.TmpDistanceHits, ignoreDynamicBodies))
            {
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Filters a list of hits for ground probing and returns the closest valid hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForGroundProbing<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<ColliderCastHit> hits,
            float3 castDirection,
            bool ignoreDynamicBodies,
            out ColliderCastHit closestHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;

            for (int i = hits.Length - 1; i >= 0; i--)
            {
                bool hitAccepted = false;
                var hit = hits[i];

                if (hit.Entity != Entity)
                {
                    // ignore hits if we're going away from them
                    float dotRatio = math.dot(hit.SurfaceNormal, castDirection);
                    if (dotRatio < -Constants.DotProductSimilarityEpsilon)
                    {
                        if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                hitAccepted = true;

                                if (hit.Fraction < closestHit.Fraction)
                                {
                                    closestHit = hit;
                                }
                            }
                        }
                    }
                }

                if (!hitAccepted)
                {
                    hits.RemoveAtSwapBack(i);
                }
            }

            return closestHit.Entity != Entity.Null;
        }
        
        /// <summary>
        /// Filters a list of hits for character movement and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="characterIsKinematic"> Is the character kinematic (as opposed to simulated dynamic) </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoredEntity"> An optional Entity to force ignore </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest detected hit </param>
        /// <param name="foundAnyOverlaps"> Whether any overlaps were found with other colliders </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForMove<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<ColliderCastHit> hits,
            bool characterIsKinematic,
            float3 castDirection,
            Entity ignoredEntity,
            bool ignoreDynamicBodies,
            out ColliderCastHit closestHit,
            out bool foundAnyOverlaps) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            foundAnyOverlaps = false;
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            float dotRatioOfSelectedHit = float.MaxValue;
            
            for (int i = hits.Length - 1; i >= 0; i--)
            {
                var hit = hits[i];
                if (hit.Entity != ignoredEntity)
                {
                    if (hit.Entity != Entity)
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            bool hitBodyIsDynamic = PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex);

                            // Remember overlaps (must always include dynamic hits or hits we move away from)
                            if (hit.Fraction <= 0f || (characterIsKinematic && hitBodyIsDynamic))
                            {
                                foundAnyOverlaps = true;
                            }

                            if (!ignoreDynamicBodies || !hitBodyIsDynamic)
                            {
                                // ignore hits if we're going away from them
                                float dotRatio = math.dot(hit.SurfaceNormal, castDirection);
                                if (dotRatio < -Constants.DotProductSimilarityEpsilon)
                                {
                                    // only accept closest hit so far
                                    if (hit.Fraction <= closestHit.Fraction)
                                    {
                                        // Accept hit if it's the new closest one, or if equal distance but more obstructing
                                        bool isCloserThanPreviousSelectedHit = hit.Fraction < closestHit.Fraction;
                                        if (isCloserThanPreviousSelectedHit || dotRatio < dotRatioOfSelectedHit)
                                        {
                                            closestHit = hit;
                                            dotRatioOfSelectedHit = dotRatio;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }
        
        /// <summary>
        /// Filters a list of hits and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForClosestCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<ColliderCastHit> hits,
            bool onlyObstructingHits,
            float3 castDirection,
            bool ignoreDynamicBodies,
            out ColliderCastHit closestHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.Fraction <= closestHit.Fraction)
                {
                    if (hit.Entity != Entity)
                    {
                        if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                        {
                            // ignore hits if we're going away from them
                            if (!onlyObstructingHits || math.dot(hit.SurfaceNormal, castDirection) < -Constants.DotProductSimilarityEpsilon)
                            {
                                if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                                {
                                    closestHit = hit;
                                }
                            }
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }
        
        /// <summary>
        /// Filters a list of hits and keeps only valid hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForAllCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<ColliderCastHit> hits,
            bool onlyObstructingHits,
            float3 castDirection,
            bool ignoreDynamicBodies) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            for (int i = hits.Length - 1; i >= 0; i--)
            {
                bool hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        // ignore hits if we're going away from them
                        if (!onlyObstructingHits || math.dot(hit.SurfaceNormal, castDirection) < -Constants.DotProductSimilarityEpsilon)
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                hitAccepted = true;
                            }
                        }
                    }
                }

                if (!hitAccepted)
                {
                    hits.RemoveAtSwapBack(i);
                }
            }

            return hits.Length > 0;
        }
        
        /// <summary>
        /// Filters a list of hits for overlap resolution, and keeps only valid hits. Also returns a variety of closest hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="closestHit"> The closest valid hit </param>
        /// <param name="closestDynamicHit"> The closest valid dynamic hit </param>
        /// <param name="closestNonDynamicHit"> The closest valid non-dynamic hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void FilterDistanceHitsForSolveOverlaps<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<DistanceHit> hits,
            out DistanceHit closestHit,
            out DistanceHit closestDynamicHit,
            out DistanceHit closestNonDynamicHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            closestDynamicHit = default;
            closestDynamicHit.Fraction = float.MaxValue;
            closestNonDynamicHit = default;
            closestNonDynamicHit.Fraction = float.MaxValue;
            
            for (int i = hits.Length - 1; i >= 0; i--)
            {
                bool hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                    {
                        bool isBodyDynamic = PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex);

                        if (hit.Distance < closestHit.Distance)
                        {
                            closestHit = hit;
                        }
                        if (isBodyDynamic && hit.Distance < closestDynamicHit.Distance)
                        {
                            closestDynamicHit = hit;
                        }
                        if (!isBodyDynamic && hit.Distance < closestNonDynamicHit.Distance)
                        {
                            closestNonDynamicHit = hit;
                        }

                        // Keep all dynamic hits in the list (and only those)
                        if (isBodyDynamic)
                        {
                            hitAccepted = true;
                        }
                    }
                }

                if (!hitAccepted)
                {
                    hits.RemoveAtSwapBack(i);
                }
            }
        }
        
        /// <summary>
        /// Filters a list of hits and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest valid hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterDistanceHitsForClosestCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<DistanceHit> hits,
            bool ignoreDynamicBodies,
            out DistanceHit closestHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.Distance < closestHit.Distance)
                {
                    if (hit.Entity != Entity)
                    {
                        if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                closestHit = hit;
                            }
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }
        
        /// <summary>
        /// Filters a list of hits and returns all valid hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterDistanceHitsForAllCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<DistanceHit> hits,
            bool ignoreDynamicBodies) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            for (int i = hits.Length - 1; i >= 0; i--)
            {
                bool hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            hitAccepted = true;
                        }
                    }
                }

                if (!hitAccepted)
                {
                    hits.RemoveAtSwapBack(i);
                }
            }

            return hits.Length > 0;
        }
        
        /// <summary>
        /// Filters a list of hits and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest valid hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterRaycastHitsForClosestCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<RaycastHit> hits,
            bool ignoreDynamicBodies,
            out RaycastHit closestHit) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.Fraction < closestHit.Fraction)
                {
                    if (hit.Entity != Entity)
                    {
                        if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                closestHit = hit;
                            }
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }

        
        /// <summary>
        /// Filters a list of hits and returns all valid hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterRaycastHitsForAllCollisions<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref NativeList<RaycastHit> hits,
            bool ignoreDynamicBodies) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            for (int i = hits.Length - 1; i >= 0; i--)
            {
                bool hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies || !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            hitAccepted = true;
                        }
                    }
                }

                if (!hitAccepted)
                {
                    hits.RemoveAtSwapBack(i);
                }
            }

            return hits.Length > 0;
        }
    }
}