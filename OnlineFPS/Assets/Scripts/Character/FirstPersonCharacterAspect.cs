using System;
using System.Collections.Generic;
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
using Unity.CharacterController;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging;
using Unity.NetCode;
using UnityEngine;

namespace OnlineFPS
{
    public struct FirstPersonCharacterUpdateContext
    {
        // Here, you may add additional global data for your character updates, such as ComponentLookups, Singletons, NativeCollections, etc...
        // The data you add here will be accessible in your character updates and all of your character "callbacks".
        [ReadOnly] public ComponentLookup<WeaponVisualFeedback> WeaponVisualFeedbackLookup;
        [ReadOnly] public ComponentLookup<WeaponControl> WeaponControlLookup;

        public void OnSystemCreate(ref SystemState state)
        {
            WeaponVisualFeedbackLookup = state.GetComponentLookup<WeaponVisualFeedback>(true);
            WeaponControlLookup = state.GetComponentLookup<WeaponControl>(true);
        }

        public void OnSystemUpdate(ref SystemState state)
        {
            WeaponVisualFeedbackLookup.Update(ref state);
            WeaponControlLookup.Update(ref state);
        }
    }

    public readonly partial struct FirstPersonCharacterAspect : IAspect,
        IKinematicCharacterProcessor<FirstPersonCharacterUpdateContext>
    {
        public readonly KinematicCharacterAspect CharacterAspect;
        public readonly RefRW<FirstPersonCharacterComponent> CharacterComponent;
        public readonly RefRW<FirstPersonCharacterControl> CharacterControl;
        public readonly RefRW<ActiveWeapon> ActiveWeapon;

        public void PhysicsUpdate(ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext)
        {
            ref FirstPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
            ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;
            ref float3 characterPosition = ref CharacterAspect.LocalTransform.ValueRW.Position;

            // First phase of default character update
            CharacterAspect.Update_Initialize(in this, ref context, ref baseContext, ref characterBody,
                baseContext.Time.DeltaTime);
            CharacterAspect.Update_ParentMovement(in this, ref context, ref baseContext, ref characterBody,
                ref characterPosition, characterBody.WasGroundedBeforeCharacterUpdate);
            CharacterAspect.Update_Grounding(in this, ref context, ref baseContext, ref characterBody,
                ref characterPosition);

            // Update desired character velocity after grounding was detected, but before doing additional processing that depends on velocity
            HandleVelocityControl(ref context, ref baseContext);

            // Second phase of default character update
            CharacterAspect.Update_PreventGroundingFromFutureSlopeChange(in this, ref context, ref baseContext,
                ref characterBody, in characterComponent.StepAndSlopeHandling);
            CharacterAspect.Update_GroundPushing(in this, ref context, ref baseContext, characterComponent.Gravity);
            CharacterAspect.Update_MovementAndDecollisions(in this, ref context, ref baseContext, ref characterBody,
                ref characterPosition);
            CharacterAspect.Update_MovingPlatformDetection(ref baseContext, ref characterBody);
            CharacterAspect.Update_ParentMomentum(ref baseContext, ref characterBody);
            CharacterAspect.Update_ProcessStatefulCharacterHits();
        }

        private void HandleVelocityControl(ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext)
        {
            float deltaTime = baseContext.Time.DeltaTime;
            ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;
            ref FirstPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
            ref FirstPersonCharacterControl characterControl = ref CharacterControl.ValueRW;

            // Rotate move input and velocity to take into account parent rotation
            if (characterBody.ParentEntity != Entity.Null)
            {
                characterControl.MoveVector =
                    math.rotate(characterBody.RotationFromParent, characterControl.MoveVector);
                characterBody.RelativeVelocity =
                    math.rotate(characterBody.RotationFromParent, characterBody.RelativeVelocity);
            }

            if (characterBody.IsGrounded)
            {
                // Move on ground
                float3 targetVelocity = characterControl.MoveVector * characterComponent.GroundMaxSpeed;
                CharacterControlUtilities.StandardGroundMove_Interpolated(ref characterBody.RelativeVelocity,
                    targetVelocity, characterComponent.GroundedMovementSharpness, deltaTime, characterBody.GroundingUp,
                    characterBody.GroundHit.Normal);

                // Jump
                if (characterControl.Jump)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody,
                        characterBody.GroundingUp * characterComponent.JumpSpeed, true, characterBody.GroundingUp);
                }
            }
            else
            {
                // Move in air
                float3 airAcceleration = characterControl.MoveVector * characterComponent.AirAcceleration;
                if (math.lengthsq(airAcceleration) > 0f)
                {
                    float3 tmpVelocity = characterBody.RelativeVelocity;
                    CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration,
                        characterComponent.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

                    // Cancel air acceleration from input if we would hit a non-grounded surface (prevents air-climbing slopes at high air accelerations)
                    if (characterComponent.PreventAirAccelerationAgainstUngroundedHits &&
                        CharacterAspect.MovementWouldHitNonGroundedObstruction(in this, ref context, ref baseContext,
                            characterBody.RelativeVelocity * deltaTime, out ColliderCastHit hit))
                    {
                        characterBody.RelativeVelocity = tmpVelocity;
                    }
                }

                // Gravity
                CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity,
                    characterComponent.Gravity, deltaTime);

                // Drag
                CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime,
                    characterComponent.AirDrag);
            }
        }

        public void VariableUpdate(ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext)
        {
            ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;
            ref FirstPersonCharacterComponent characterComponent = ref CharacterComponent.ValueRW;
            ref quaternion characterRotation = ref CharacterAspect.LocalTransform.ValueRW.Rotation;
            ref FirstPersonCharacterControl characterControl = ref CharacterControl.ValueRW;
            ActiveWeapon activeWeapon = ActiveWeapon.ValueRO;

            // Add rotation from parent body to the character rotation
            // TODO: Commented out in this sample because we use special character rotation handling based on a Y angle instead of full rotation. 
            //       Need a utility function to rotate this angle from parent rotation.
            // KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref characterRotation, characterBody.RotationFromParent, baseContext.Time.DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);

            // Handle aiming look sensitivity
            if (context.WeaponControlLookup.TryGetComponent(activeWeapon.Entity, out WeaponControl weaponControl))
            {
                if (weaponControl.AimHeld)
                {
                    if (context.WeaponVisualFeedbackLookup.TryGetComponent(activeWeapon.Entity,
                            out WeaponVisualFeedback weaponFeedback))
                    {
                        characterControl.LookYawPitchDegreesDelta *=
                            weaponFeedback.LookSensitivityMultiplierWhileAiming;
                    }
                }
            }

            // Compute character & view rotations from rotation input
            FirstPersonCharacterUtilities.ComputeFinalRotationsFromRotationDelta(
                ref characterComponent.ViewPitchDegrees,
                ref characterComponent.CharacterYDegrees,
                math.up(),
                characterControl.LookYawPitchDegreesDelta,
                0, // don't include roll angle in simulation
                characterComponent.MinViewAngle,
                characterComponent.MaxViewAngle,
                out characterRotation,
                out float canceledPitchDegrees,
                out characterComponent.ViewLocalRotation);
        }

        #region Character Processor Callbacks

        public void UpdateGroundingUp(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext)
        {
            ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;

            CharacterAspect.Default_UpdateGroundingUp(ref characterBody);
        }

        public bool CanCollideWithHit(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit)
        {
            return PhysicsUtilities.IsCollidable(hit.Material);
        }

        public bool IsGroundedOnHit(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit,
            int groundingEvaluationType)
        {
            FirstPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;

            return CharacterAspect.Default_IsGroundedOnHit(
                in this,
                ref context,
                ref baseContext,
                in hit,
                in characterComponent.StepAndSlopeHandling,
                groundingEvaluationType);
        }

        public void OnMovementHit(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float hitDistance)
        {
            ref KinematicCharacterBody characterBody = ref CharacterAspect.CharacterBody.ValueRW;
            ref float3 characterPosition = ref CharacterAspect.LocalTransform.ValueRW.Position;
            FirstPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;

            CharacterAspect.Default_OnMovementHit(
                in this,
                ref context,
                ref baseContext,
                ref characterBody,
                ref characterPosition,
                ref hit,
                ref remainingMovementDirection,
                ref remainingMovementLength,
                originalVelocityDirection,
                hitDistance,
                characterComponent.StepAndSlopeHandling.StepHandling,
                characterComponent.StepAndSlopeHandling.MaxStepHeight,
                characterComponent.StepAndSlopeHandling.CharacterWidthForStepGroundingCheck);
        }

        public void OverrideDynamicHitMasses(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref PhysicsMass characterMass,
            ref PhysicsMass otherMass,
            BasicHit hit)
        {
        }

        public void ProjectVelocityOnHits(
            ref FirstPersonCharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref float3 velocity,
            ref bool characterIsGrounded,
            ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
            float3 originalVelocityDirection)
        {
            FirstPersonCharacterComponent characterComponent = CharacterComponent.ValueRO;

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
}
