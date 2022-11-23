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
using Rival;
using UnityEngine;
using Unity.Physics.Authoring;

public struct ThirdPersonCharacterUpdateContext
{
    // Here, you may add additional global data for your character updates, such as ComponentLookups, Singletons, NativeCollections, etc...
    // The data you add here will be accessible in your character updates and all of your character "callbacks".
    [ReadOnly]
    public ComponentLookup<CharacterFrictionSurface> CharacterFrictionSurfaceLookup;

    // This is called by systems that schedule jobs that update the character aspect, in their OnCreate().
    // Here, you can get the component lookups.
    public void OnSystemCreate(ref SystemState state)
    {
        CharacterFrictionSurfaceLookup = state.GetComponentLookup<CharacterFrictionSurface>(true);
    }

    // This is called by systems that schedule jobs that update the character aspect, in their OnUpdate()
    // Here, you can update the component lookups.
    public void OnSystemUpdate(ref SystemState state)
    {
        CharacterFrictionSurfaceLookup.Update(ref state);
    }
}

public readonly partial struct ThirdPersonCharacterAspect : IAspect, IKinematicCharacterProcessor<ThirdPersonCharacterUpdateContext>
{
    public readonly KinematicCharacterAspect CharacterAspect;
    public readonly RefRW<ThirdPersonCharacterComponent> CharacterComponent;
    public readonly RefRW<ThirdPersonCharacterControl> CharacterControl;

    public void PhysicsUpdate(ref ThirdPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        ref ThirdPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
        ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;

        // First phase of default character update
        CharacterAspect.Update_Initialize(baseContext.Time.DeltaTime);
        UpdateGroundingUp();
        CharacterAspect.Update_ParentMovement(in this, ref context, ref baseContext, characterBody.WasGroundedBeforeCharacterUpdate);
        CharacterAspect.Update_Grounding(in this, ref context, ref baseContext);
        
        // Update desired character velocity after grounding was detected, but before doing additional processing that depends on velocity
        HandleVelocityControl(ref context, ref baseContext);

        // Second phase of default character update
        CharacterAspect.Update_PreventGroundingFromFutureSlopeChange(in this, ref context, ref baseContext, in characterComponent.StepAndSlopeHandling);
        CharacterAspect.Update_GroundPushing(in this, ref context, ref baseContext, characterComponent.Gravity);
        CharacterAspect.Update_MovementAndDecollisions(in this, ref context, ref baseContext);
        CharacterAspect.Update_MovingPlatformDetection(ref baseContext); 
        CharacterAspect.Update_ParentMomentum(ref baseContext);
        CharacterAspect.Update_ProcessStatefulCharacterHits();
    }

    private void HandleVelocityControl(ref ThirdPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;
        ref ThirdPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
        ref ThirdPersonCharacterControl characterControl = ref CharacterControl.ValueRW;

        if (characterBody.IsGrounded)
        {
            // Move on ground
            float3 targetVelocity = characterControl.MoveVector * characterComponent.GroundMaxSpeed;
            
            // Sprint
            if (characterControl.Sprint)
            {
                targetVelocity *= characterComponent.SprintSpeedMultiplier;
            }
            
            // Friction surfaces
            if (context.CharacterFrictionSurfaceLookup.TryGetComponent(characterBody.GroundHit.Entity, out CharacterFrictionSurface frictionSurface))
            {
                targetVelocity *= frictionSurface.VelocityFactor;
            }
            
            CharacterControlUtilities.StandardGroundMove_Interpolated(ref characterBody.RelativeVelocity, targetVelocity, characterComponent.GroundedMovementSharpness, deltaTime, characterBody.GroundingUp, characterBody.GroundHit.Normal);
            
            // Jump
            if (characterControl.Jump)
            {
                CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * characterComponent.JumpSpeed, true, characterBody.GroundingUp);
            }

            // Reset air jumps when grounded
            characterComponent.CurrentAirJumps = 0;
        }
        else
        {
            // Move in air
            float3 airAcceleration = characterControl.MoveVector * characterComponent.AirAcceleration;
            if (math.lengthsq(airAcceleration) > 0f)
            {
                float3 tmpVelocity = characterBody.RelativeVelocity;
                CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration, characterComponent.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

                // Cancel air acceleration from input if we would hit a non-grounded surface (prevents air-climbing slopes at high air accelerations)
                if (characterComponent.PreventAirAccelerationAgainstUngroundedHits && CharacterAspect.MovementWouldHitNonGroundedObstruction(in this, ref context, ref baseContext, characterBody.RelativeVelocity * deltaTime, out ColliderCastHit hit))
                {
                    characterBody.RelativeVelocity = tmpVelocity;
                }
            }

            // Air Jumps
            if (characterControl.Jump && characterComponent.CurrentAirJumps < characterComponent.MaxAirJumps)
            {
                CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * characterComponent.JumpSpeed, true, characterBody.GroundingUp);
                characterComponent.CurrentAirJumps++;
            }
            
            // Gravity
            CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, characterComponent.Gravity, deltaTime);

            // Drag
            CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, characterComponent.AirDrag);
        }
    }

    public void VariableUpdate(ref ThirdPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;
        ref ThirdPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
        ref ThirdPersonCharacterControl characterControl = ref CharacterControl.ValueRW;
        ref quaternion characterRotation = ref CharacterAspect.LocalTransform.ValueRW.Rotation;

        // Add rotation from parent body to the character rotation
        // (this is for allowing a rotating moving platform to rotate your character as well, and handle interpolation properly)
        KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref characterRotation, characterBody.RotationFromParent, baseContext.Time.DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
        
        // Rotate towards move direction
        if (math.lengthsq(characterControl.MoveVector) > 0f)
        {
            CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation, baseContext.Time.DeltaTime, math.normalizesafe(characterControl.MoveVector), MathUtilities.GetUpFromRotation(characterRotation), characterComponent.RotationSharpness);
        }
    }
    
    #region Character Processor Callbacks
    public void UpdateGroundingUp()
    {
        CharacterAspect.Default_UpdateGroundingUp();
    }
    
    public bool CanCollideWithHit(
        ref ThirdPersonCharacterUpdateContext context, 
        ref KinematicCharacterUpdateContext baseContext,
        in BasicHit hit)
    {
        ThirdPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;
        
        // First, see if we'd have to ignore based on the default implementation
        if (!KinematicCharacterUtilities.IsHitCollidableOrCharacter(
                in baseContext.StoredCharacterBodyPropertiesLookup, 
                hit.Material, 
                hit.Entity))
        {
            return false;
        }

        // if not, check for the ignored tag
        if (PhysicsUtilities.HasPhysicsTag(in baseContext.PhysicsWorld, hit.RigidBodyIndex, characterComponent.IgnoredPhysicsTags))
        {
            return false;
        }

        return true;
    }

    public bool IsGroundedOnHit(
        ref ThirdPersonCharacterUpdateContext context, 
        ref KinematicCharacterUpdateContext baseContext,
        in BasicHit hit, 
        int groundingEvaluationType)
    {
        ThirdPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;
        
        return CharacterAspect.Default_IsGroundedOnHit(
            in this,
            ref context,
            ref baseContext,
            in hit,
            in characterComponent.StepAndSlopeHandling,
            groundingEvaluationType);
    }

    public void OnMovementHit(
            ref ThirdPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float hitDistance)
    {
        ThirdPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;
        
        CharacterAspect.Default_OnMovementHit(
            in this,
            ref context,
            ref baseContext,
            ref hit,
            ref remainingMovementDirection,
            ref remainingMovementLength,
            originalVelocityDirection,
            hitDistance,
            characterComponent.StepAndSlopeHandling.StepHandling,
            characterComponent.StepAndSlopeHandling.MaxStepHeight);
    }

    public void OverrideDynamicHitMasses(
        ref ThirdPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        ref PhysicsMass characterMass,
        ref PhysicsMass otherMass,
        BasicHit hit)
    {
    }

    public void ProjectVelocityOnHits(
        ref ThirdPersonCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        ref float3 velocity,
        ref bool characterIsGrounded,
        ref BasicHit characterGroundHit,
        in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
        float3 originalVelocityDirection)
    {
        ThirdPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;
        
        CharacterAspect.Default_ProjectVelocityOnHits(
            ref velocity,
            ref characterIsGrounded,
            ref characterGroundHit,
            in velocityProjectionHits,
            originalVelocityDirection,
            characterComponent.StepAndSlopeHandling.ConstrainVelocityToGroundPlane);
    }
    #endregion
}
