using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public struct SwimmingState : IPlatformerCharacterState
{
    public bool HasJumpedWhileSwimming;
    public bool HasDetectedGrounding;
    public bool ShouldExitSwimming;

    private const float kDistanceFromSurfaceToAllowJumping = -0.05f;
    private const float kForcedDistanceFromSurface = 0.01f;

    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;

        aspect.SetCapsuleGeometry(character.SwimmingGeometry.ToCapsuleGeometry());

        characterProperties.SnapToGround = false;
        characterBody.IsGrounded = false;

        HasJumpedWhileSwimming = false;
        ShouldExitSwimming = false;
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;

        characterProperties.SnapToGround = true;
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        aspect.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, true);
        
        PreMovementUpdate(ref context, ref baseContext, in aspect);

        aspect.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, true, false, true);

        PostMovementUpdate(ref context, ref baseContext, in aspect);

        DetectTransitions(ref context, ref baseContext, in aspect);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;

        if (!ShouldExitSwimming)
        {
            if (character.DistanceFromWaterSurface > character.SwimmingStandUpDistanceFromSurface)
            {
                // when close to surface, orient self up
                float3 upPlane = -math.normalizesafe(customGravity.Gravity);
                float3 targetForward = default;
                if (math.lengthsq(characterControl.MoveVector) > 0f)
                {
                    targetForward = math.normalizesafe(MathUtilities.ProjectOnPlane(characterControl.MoveVector, upPlane));
                }
                else
                {
                    targetForward = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(characterRotation), upPlane));
                    if (math.dot(characterBody.GroundingUp, upPlane) < 0f)
                    {
                        targetForward = -targetForward;
                    }
                }
                quaternion targetRotation = MathUtilities.CreateRotationWithUpPriority(upPlane, targetForward);
                targetRotation = math.slerp(characterRotation, targetRotation, MathUtilities.GetSharpnessInterpolant(character.SwimmingRotationSharpness, deltaTime));
                MathUtilities.SetRotationAroundPoint(ref characterRotation, ref characterPosition, aspect.GetGeometryCenter(character.SwimmingGeometry), targetRotation);
            }
            else
            {
                if (math.lengthsq(characterControl.MoveVector) > 0f)
                {
                    // Make character up face the movement direction, and character forward face gravity direction as much as it can
                    quaternion targetRotation = MathUtilities.CreateRotationWithUpPriority(math.normalizesafe(characterControl.MoveVector), math.normalizesafe(customGravity.Gravity));
                    targetRotation = math.slerp(characterRotation, targetRotation, MathUtilities.GetSharpnessInterpolant(character.SwimmingRotationSharpness, deltaTime));
                    MathUtilities.SetRotationAroundPoint(ref characterRotation, ref characterPosition, aspect.GetGeometryCenter(character.SwimmingGeometry), targetRotation);
                }
            }
        }
    }

    public void GetCameraParameters(in PlatformerCharacterComponent character, out Entity cameraTarget, out bool calculateUpFromGravity)
    {
        cameraTarget = character.SwimmingCameraTargetEntity;
        calculateUpFromGravity = true;
    }

    public void GetMoveVectorFromPlayerInput(in PlatformerPlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
    {
        float3 cameraFwd = math.mul(cameraRotation, math.forward());
        float3 cameraRight = math.mul(cameraRotation, math.right());
        float3 cameraUp = math.mul(cameraRotation, math.up());
        
        moveVector = (cameraRight * inputs.Move.x) + (cameraFwd * inputs.Move.y);
        if (inputs.JumpHeld)
        {
            moveVector += cameraUp;
        }
        if (inputs.RollHeld)
        {
            moveVector -= cameraUp;
        }
        moveVector = MathUtilities.ClampToMaxLength(moveVector, 1f);
    }

    public void PreMovementUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;

        HasDetectedGrounding = characterBody.IsGrounded;
        characterBody.IsGrounded = false;
        
        if (DetectWaterZones(ref context, ref baseContext, in aspect, out character.DirectionToWaterSurface, out character.DistanceFromWaterSurface))
        {
            // Movement
            float3 addedMoveVector = float3.zero;
            if (character.DistanceFromWaterSurface > character.SwimmingStandUpDistanceFromSurface)
            {
                // When close to water surface, prevent moving down unless the input points strongly down
                float dotMoveDirectionWithSurface = math.dot(math.normalizesafe(characterControl.MoveVector), character.DirectionToWaterSurface);
                if (dotMoveDirectionWithSurface > character.SwimmingSurfaceDiveThreshold)
                {
                    characterControl.MoveVector = MathUtilities.ProjectOnPlane(characterControl.MoveVector, character.DirectionToWaterSurface);
                }

                // Add an automatic move towards surface
                addedMoveVector = character.DirectionToWaterSurface * 0.1f;
            }
            float3 acceleration = (characterControl.MoveVector + addedMoveVector) * character.SwimmingAcceleration;
            CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, acceleration, character.SwimmingMaxSpeed, -MathUtilities.GetForwardFromRotation(characterRotation), deltaTime, true);

            // Water drag
            CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, character.SwimmingDrag);

            // Handle jumping out of water when close to water surface
            HasJumpedWhileSwimming = false;
            if (characterControl.JumpPressed && character.DistanceFromWaterSurface > kDistanceFromSurfaceToAllowJumping)
            {
                CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * character.SwimmingJumpSpeed, true, characterBody.GroundingUp);
                HasJumpedWhileSwimming = true;
            }
        }
        else
        {
            ShouldExitSwimming = true;
        }
    }

    public void PostMovementUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;

        bool determinedHasExitedWater = false;
        if (DetectWaterZones(ref context, ref baseContext, in aspect, out character.DirectionToWaterSurface, out character.DistanceFromWaterSurface))
        {
            // Handle snapping to water surface when trying to swim out of the water
            if (character.DistanceFromWaterSurface > -kForcedDistanceFromSurface)
            {
                float currentDistanceToTargetDistance = -kForcedDistanceFromSurface - character.DistanceFromWaterSurface;
                float3 translationSnappedToWaterSurface = characterPosition + (character.DirectionToWaterSurface * currentDistanceToTargetDistance);

                // Only snap to water surface if we're not jumping out of the water, or if we'd be obstructed when trying to snap back (allows us to walk out of water)
                if (HasJumpedWhileSwimming || characterBody.GroundHit.Entity != Entity.Null)
                {
                    determinedHasExitedWater = true;
                }
                else
                {
                    // Snap position bact to water surface
                    characterPosition = translationSnappedToWaterSurface;

                    // Project velocity on water surface normal
                    characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, character.DirectionToWaterSurface);
                }
            }
        }

        ShouldExitSwimming = determinedHasExitedWater;
    }

    public bool DetectTransitions(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref PlatformerCharacterStateMachine stateMachine = ref aspect.StateMachine.ValueRW;

        if (ShouldExitSwimming || HasDetectedGrounding)
        {
            if (HasDetectedGrounding)
            {
                stateMachine.TransitionToState(CharacterState.GroundMove, ref context, ref baseContext, in aspect);
                return true;
            }
            else
            {
                stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
                return true;
            }
        }

        return aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }

    public unsafe static bool DetectWaterZones(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect, out float3 directionToWaterSurface, out float waterSurfaceDistance)
    {
        directionToWaterSurface = default;
        waterSurfaceDistance = 0f;
        
        ref PhysicsCollider physicsCollider = ref aspect.CharacterAspect.PhysicsCollider.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;

        RigidTransform characterRigidTransform = new RigidTransform(characterRotation, characterPosition);
        float3 swimmingDetectionPointWorldPosition = math.transform(characterRigidTransform, character.LocalSwimmingDetectionPoint);
        CollisionFilter waterDetectionFilter = new CollisionFilter
        {
            BelongsTo = physicsCollider.ColliderPtr->GetCollisionFilter().BelongsTo,
            CollidesWith = character.WaterPhysicsCategory.Value,
        };

        PointDistanceInput pointInput = new PointDistanceInput
        {
            Filter = waterDetectionFilter,
            MaxDistance = character.WaterDetectionDistance,
            Position = swimmingDetectionPointWorldPosition,
        };

        if (baseContext.PhysicsWorld.CalculateDistance(pointInput, out DistanceHit closestHit))
        {
            directionToWaterSurface = closestHit.SurfaceNormal; // always goes in the direction of decolliding from the target collider
            waterSurfaceDistance = closestHit.Distance; // positive means above surface
            return true;
        }

        return false;
    }
}