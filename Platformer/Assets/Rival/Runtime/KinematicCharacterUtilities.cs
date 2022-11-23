using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
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
    /// The state of a character hit (enter, exit, stay)
    /// </summary>
    public enum CharacterHitState
    {
        Enter,
        Stay,
        Exit,
    }

    /// <summary>
    /// Identifier for a type of grounding evaluation
    /// </summary>
    public enum GroundingEvaluationType
    {
        Default,
        GroundProbing,
        OverlapDecollision,
        InitialOverlaps,
        MovementHit,
        StepUpHit,
    }

    /// <summary>
    /// Comparer for sorting collider cast hits by hit fraction (distance)
    /// </summary>
    public struct HitFractionComparer : IComparer<ColliderCastHit>
    {
        public int Compare(ColliderCastHit x, ColliderCastHit y)
        {
            if (x.Fraction > y.Fraction)
            {
                return 1;
            }
            else if (x.Fraction < y.Fraction)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// A common hit struct for cast hits and distance hits
    /// </summary>
    [System.Serializable]
    public struct BasicHit
    {
        /// <summary>
        /// Hit entity
        /// </summary>
        public Entity Entity;
        /// <summary>
        /// Hit rigidbody index
        /// </summary>
        public int RigidBodyIndex;
        /// <summary>
        /// Hit collider key
        /// </summary>
        public ColliderKey ColliderKey;
        /// <summary>
        /// Hit point
        /// </summary>
        public float3 Position;
        /// <summary>
        /// Hit normal
        /// </summary>
        public float3 Normal;
        /// <summary>
        /// Hit material
        /// </summary>
        public Material Material;

        public BasicHit(RaycastHit hit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.SurfaceNormal;
            Material = hit.Material;
        }

        public BasicHit(ColliderCastHit hit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.SurfaceNormal;
            Material = hit.Material;
        }

        public BasicHit(DistanceHit hit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.SurfaceNormal;
            Material = hit.Material;
        }

        public BasicHit(KinematicCharacterHit hit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.Normal;
            Material = hit.Material;
        }

        public BasicHit(KinematicVelocityProjectionHit hit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.Normal;
            Material = hit.Material;
        }
    }

    /// <summary>
    /// Collection of utility functions for characters
    /// </summary>
    public static class KinematicCharacterUtilities
    {
        /// <summary>
        /// Returns an entity query builder that includes all basic components of a character
        /// </summary>
        /// <returns></returns>
        public static EntityQueryBuilder GetBaseCharacterQueryBuilder()
        {
            return new EntityQueryBuilder(Allocator.Temp)
                .WithAll<
                    LocalTransform, 
                    PhysicsCollider,
                    PhysicsVelocity,
                    PhysicsMass,
                    PhysicsWorldIndex>()
                .WithAll<
                    KinematicCharacterProperties,
                    KinematicCharacterBody,
                    StoredKinematicCharacterData>()
                .WithAll<
                    KinematicCharacterHit,
                    StatefulKinematicCharacterHit,
                    KinematicCharacterDeferredImpulse,
                    KinematicVelocityProjectionHit>();
        }

        /// <summary>
        /// Returns an entity query builder that includes all basic components of a character as well as the interpolation component
        /// </summary>
        /// <returns></returns>
        public static EntityQueryBuilder GetInterpolatedCharacterQueryBuilder()
        {
            return GetBaseCharacterQueryBuilder()
                .WithAll<CharacterInterpolation>();
        }

        /// <summary>
        /// Adds all the required character components to an entity
        /// </summary>
        public static void CreateCharacter(
            EntityManager dstManager,
            Entity entity,
            AuthoringKinematicCharacterProperties authoringProperties,
            RigidTransform transform)
        {
            // Base character components
            dstManager.AddComponentData(entity, new KinematicCharacterProperties(authoringProperties));
            dstManager.AddComponentData(entity, KinematicCharacterBody.GetDefault());
            dstManager.AddComponentData(entity, new StoredKinematicCharacterData());

            dstManager.AddBuffer<KinematicCharacterHit>(entity);
            dstManager.AddBuffer<KinematicCharacterDeferredImpulse>(entity);
            dstManager.AddBuffer<StatefulKinematicCharacterHit>(entity);
            dstManager.AddBuffer<KinematicVelocityProjectionHit>(entity);

            // Kinematic physics body components
            dstManager.AddComponentData(entity, new PhysicsVelocity());
            dstManager.AddComponentData(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            dstManager.AddComponentData(entity, new PhysicsGravityFactor { Value = 0f });
            dstManager.AddComponentData(entity, new PhysicsCustomTags { Value = authoringProperties.CustomPhysicsBodyTags.Value });

            // Interpolation
            if (authoringProperties.InterpolatePosition || authoringProperties.InterpolateRotation)
            {
                dstManager.AddComponentData(entity, new CharacterInterpolation
                {
                    InterpolateRotation = authoringProperties.InterpolateRotation ? (byte)1 : (byte)0,
                    InterpolatePosition = authoringProperties.InterpolatePosition ? (byte)1 : (byte)0,
                });
                dstManager.AddComponentData(entity, new PropagateLocalToWorld());
            }
        }

        /// <summary>
        /// Adds all the required character components to an entity
        /// </summary>
        public static void CreateCharacter(
            EntityCommandBuffer commandBuffer,
            Entity entity,
            AuthoringKinematicCharacterProperties authoringProperties,
            RigidTransform transform)
        {
            // Base character components
            commandBuffer.AddComponent(entity, new KinematicCharacterProperties(authoringProperties));
            commandBuffer.AddComponent(entity, KinematicCharacterBody.GetDefault());
            commandBuffer.AddComponent(entity, new StoredKinematicCharacterData());

            commandBuffer.AddBuffer<KinematicCharacterHit>(entity);
            commandBuffer.AddBuffer<KinematicCharacterDeferredImpulse>(entity);
            commandBuffer.AddBuffer<StatefulKinematicCharacterHit>(entity);
            commandBuffer.AddBuffer<KinematicVelocityProjectionHit>(entity);

            // Kinematic physics body components
            commandBuffer.AddComponent(entity, new PhysicsVelocity());
            commandBuffer.AddComponent(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            commandBuffer.AddComponent(entity, new PhysicsGravityFactor { Value = 0f });
            commandBuffer.AddComponent(entity, new PhysicsCustomTags { Value = authoringProperties.CustomPhysicsBodyTags.Value });

            // Interpolation
            if (authoringProperties.InterpolatePosition || authoringProperties.InterpolateRotation)
            {
                commandBuffer.AddComponent(entity, new CharacterInterpolation
                {
                    InterpolateRotation = authoringProperties.InterpolateRotation ? (byte)1 : (byte)0,
                    InterpolatePosition = authoringProperties.InterpolatePosition ? (byte)1 : (byte)0,
                });
                commandBuffer.AddComponent(entity, new PropagateLocalToWorld());
            }
        }

        /// <summary>
        /// Handles the conversion from GameObject to Entity for a character
        /// </summary>
        public static void BakeCharacter<T>(
            Baker<T> baker,
            T authoring,
            AuthoringKinematicCharacterProperties authoringProperties) where T : MonoBehaviour
        {
            if (authoring.transform.lossyScale != UnityEngine.Vector3.one)
            {
                UnityEngine.Debug.LogError("ERROR: kinematic character objects do not support having a scale other than (1,1,1). Conversion will be aborted");
                return;
            }
            if (authoring.gameObject.GetComponent<PhysicsBodyAuthoring>() != null)
            {
                UnityEngine.Debug.LogError("ERROR: kinematic character objects cannot have a PhysicsBodyAuthoring component. The correct physics components will be setup automatically during conversion. Conversion will be aborted");
                return;
            }

            // Base character components
            baker.AddComponent(new KinematicCharacterProperties(authoringProperties));
            baker.AddComponent(KinematicCharacterBody.GetDefault());
            baker.AddComponent(new StoredKinematicCharacterData());

            baker.AddBuffer<KinematicCharacterHit>();
            baker.AddBuffer<KinematicCharacterDeferredImpulse>();
            baker.AddBuffer<StatefulKinematicCharacterHit>();
            baker.AddBuffer<KinematicVelocityProjectionHit>();

            // Kinematic physics body components
            baker.AddComponent(new PhysicsVelocity());
            baker.AddComponent(PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            baker.AddComponent(new PhysicsGravityFactor { Value = 0f });
            baker.AddComponent(new PhysicsCustomTags { Value = authoringProperties.CustomPhysicsBodyTags.Value });

            // Interpolation
            if (authoringProperties.InterpolatePosition || authoringProperties.InterpolateRotation)
            {
                baker.AddComponent(new CharacterInterpolation
                {
                    InterpolateRotation = authoringProperties.InterpolateRotation ? (byte)1 : (byte)0,
                    InterpolatePosition = authoringProperties.InterpolatePosition ? (byte)1 : (byte)0,
                });
                baker.AddComponent(new PropagateLocalToWorld());
            }
        }

        /// <summary>
        /// Creates a character hit buffer element based on the provided parameters
        /// </summary>
        /// <param name="newHit"> The detected hit </param>
        /// <param name="characterIsGrounded"> Whether or not the character is currently grounded </param>
        /// <param name="characterRelativeVelocity"> The character's relative velocity </param>
        /// <param name="isGroundedOnHit"> Whether or not the character would be grounded on this hit </param>
        /// <returns> The resulting character hit </returns>
        public static KinematicCharacterHit CreateCharacterHit(
            in BasicHit newHit,
            bool characterIsGrounded,
            float3 characterRelativeVelocity,
            bool isGroundedOnHit)
        {
            KinematicCharacterHit newCharacterHit = new KinematicCharacterHit
            {
                Entity = newHit.Entity,
                RigidBodyIndex = newHit.RigidBodyIndex,
                ColliderKey = newHit.ColliderKey,
                Normal = newHit.Normal,
                Position = newHit.Position,
                WasCharacterGroundedOnHitEnter = characterIsGrounded,
                IsGroundedOnHit = isGroundedOnHit,
                CharacterVelocityBeforeHit = characterRelativeVelocity,
                CharacterVelocityAfterHit = characterRelativeVelocity,
            };

            return newCharacterHit;
        }
        
        /// <summary>
        /// Incrementally rotates a rotation at a variable rate, based on a rotation delta that should happen over a fixed time delta
        /// </summary>
        /// <param name="modifiedRotation"> The source rotation being modified </param>
        /// <param name="fixedRateRotation"> The rotation that needs to happen over a fixed time delta </param>
        /// <param name="deltaTime"> The variable time delta </param>
        /// <param name="fixedDeltaTime"> The reference fixed time delta </param>
        public static void AddVariableRateRotationFromFixedRateRotation(ref quaternion modifiedRotation, quaternion fixedRateRotation, float deltaTime, float fixedDeltaTime)
        {
            if (fixedDeltaTime > 0f)
            {
                float rotationRatio = math.clamp(deltaTime / fixedDeltaTime, 0f, 1f);
                quaternion rotationFromCharacterParent = math.slerp(quaternion.identity, fixedRateRotation, rotationRatio);
                modifiedRotation = math.mul(modifiedRotation, rotationFromCharacterParent);
            }
        }

        /// <summary>
        /// Determines if a hit has "Collide" collision response, or is a collideable character
        /// </summary>
        /// <param name="storedCharacterBodyPropertiesLookup"> Lookup for the component that stores character data on character entities </param>
        /// <param name="hitMaterial"> The hit material </param>
        /// <param name="hitEntity"> The hit entity </param>
        /// <returns></returns>
        public static bool IsHitCollidableOrCharacter(
            in ComponentLookup<StoredKinematicCharacterData> storedCharacterBodyPropertiesLookup, 
            Material hitMaterial,
            Entity hitEntity)
        {
            // Only collide with collidable colliders
            if (hitMaterial.CollisionResponse == CollisionResponsePolicy.Collide ||
                hitMaterial.CollisionResponse == CollisionResponsePolicy.CollideRaiseCollisionEvents)
            {
                return true;
            }
            
            // If collider's collision response is Trigger or None, it could potentially be a Character. So make a special exception in that case
            if (storedCharacterBodyPropertiesLookup.HasComponent(hitEntity))
            {
                return true;
            }

            return false;
        }
    }
}