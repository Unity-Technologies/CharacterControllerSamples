using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public struct LedgeGrabState : IPlatformerCharacterState
{
    private bool DetectedMustExitLedge;
    private float3 ForwardHitNormal;

    const float collisionOffset = 0.02f;
    
    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        
        aspect.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
        
        characterProperties.EvaluateGrounding = false;
        characterProperties.DetectMovementCollisions = false;
        characterProperties.DecollideFromOverlaps = false;

        characterBody.RelativeVelocity = float3.zero;
        characterBody.IsGrounded = false;
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        
        if (nextState != CharacterState.LedgeStandingUp)
        {
            characterProperties.EvaluateGrounding = true;
            characterProperties.DetectMovementCollisions = true;
            characterProperties.DecollideFromOverlaps = true;

            aspect.CharacterAspect.SetOrUpdateParentBody(ref baseContext, ref characterBody, default, default); 
        }

        characterBody.RelativeVelocity = float3.zero;
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        
        aspect.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, false);

        DetectedMustExitLedge = false;
        characterBody.RelativeVelocity = float3.zero;

        LedgeDetection(
            ref context,
            ref baseContext,
            in aspect,
            characterPosition,
            characterRotation,
            out bool ledgeIsValid,
            out ColliderCastHit surfaceHit,
            out ColliderCastHit forwardHit,
            out float3 characterTranslationAtLedgeSurface,
            out bool wouldBeGroundedOnLedgeSurfaceHit,
            out float forwardHitDistance,
            out bool isObstructedAtSurface,
            out bool isObstructedAtCurrentPosition,
            out float upOffsetToPlaceLedgeDetectionPointAtLedgeLevel);

        if (ledgeIsValid && !isObstructedAtSurface)
        {
            ForwardHitNormal = forwardHit.SurfaceNormal;

            // Stick to wall
            float3 characterForward = MathUtilities.GetForwardFromRotation(characterRotation);
            characterPosition += characterForward * (forwardHitDistance - collisionOffset);

            // Adjust to ledge height
            characterPosition += characterBody.GroundingUp * (upOffsetToPlaceLedgeDetectionPointAtLedgeLevel - collisionOffset);

            if (math.lengthsq(characterControl.MoveVector) > 0f)
            {
                // Move input
                float3 ledgeDirection = math.normalizesafe(math.cross(surfaceHit.SurfaceNormal, forwardHit.SurfaceNormal));
                float3 moveInputOnLedgeDirection = math.projectsafe(characterControl.MoveVector, ledgeDirection);

                // Check for move obstructions
                float3 targetTranslationAfterMove = characterPosition + (moveInputOnLedgeDirection * character.LedgeMoveSpeed * deltaTime);
                LedgeDetection(
                    ref context,
                    ref baseContext,
                    in aspect,
                    targetTranslationAfterMove,
                    characterRotation,
                    out bool afterMoveLedgeIsValid,
                    out ColliderCastHit afterMoveSurfaceHit,
                    out ColliderCastHit afterMoveForwardHit,
                    out float3 afterMoveCharacterTranslationAtLedgeSurface,
                    out bool afterMoveWouldBeGroundedOnLedgeSurfaceHit,
                    out float afterMoveForwardHitDistance,
                    out bool afterMoveIsObstructedAtSurface,
                    out bool afterMoveIsObstructedAtCurrentPosition,
                    out float afterMoveUpOffsetToPlaceLedgeDetectionPointAtLedgeLevel);

                if (afterMoveLedgeIsValid && !afterMoveIsObstructedAtSurface)
                {
                    characterBody.RelativeVelocity = moveInputOnLedgeDirection * character.LedgeMoveSpeed;
            
                    // Apply velocity to position
                    characterPosition += characterBody.RelativeVelocity * baseContext.Time.DeltaTime;
                }
            }
            
            aspect.CharacterAspect.SetOrUpdateParentBody(ref baseContext, ref characterBody, forwardHit.Entity, forwardHit.Position); 
        }
        else
        {
            DetectedMustExitLedge = true;
        }

        // Detect letting go of ledge
        if (characterControl.CrouchPressed || characterControl.DashPressed)
        {
            character.LedgeGrabBlockCounter = 0.3f;
        }

        aspect.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, false, false, true);

        DetectTransitions(ref context, ref baseContext, in aspect);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;

        // Adjust rotation to face current ledge wall
        quaternion targetRotation = quaternion.LookRotationSafe(math.normalizesafe(MathUtilities.ProjectOnPlane(-ForwardHitNormal, characterBody.GroundingUp)), characterBody.GroundingUp);
        characterRotation = math.slerp(characterRotation, targetRotation, MathUtilities.GetSharpnessInterpolant(character.LedgeRotationSharpness, deltaTime));
    }

    public void GetCameraParameters(in PlatformerCharacterComponent character, out Entity cameraTarget, out bool calculateUpFromGravity)
    {
        cameraTarget = character.DefaultCameraTargetEntity;
        calculateUpFromGravity = true;
    }

    public void GetMoveVectorFromPlayerInput(in PlatformerPlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
    {
        PlatformerCharacterAspect.GetCommonMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
    }

    public bool DetectTransitions(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        ref PlatformerCharacterStateMachine stateMachine = ref aspect.StateMachine.ValueRW;
        
        if (IsLedgeGrabBlocked(in character) || DetectedMustExitLedge)
        {
            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
            return true;
        }

        if (characterControl.JumpPressed)
        {
            LedgeDetection(
                ref context,
                ref baseContext,
                in aspect,
                characterPosition,
                characterRotation,
                out bool ledgeIsValid,
                out ColliderCastHit surfaceHit,
                out ColliderCastHit forwardHit,
                out float3 characterTranslationAtLedgeSurface,
                out bool wouldBeGroundedOnLedgeSurfaceHit,
                out float forwardHitDistance,
                out bool isObstructedAtSurface,
                out bool isObstructedAtCurrentPosition,
                out float upOffsetToPlaceLedgeDetectionPointAtLedgeLevel);

            if (ledgeIsValid && !isObstructedAtSurface && wouldBeGroundedOnLedgeSurfaceHit)
            {
                stateMachine.LedgeStandingUpState.StandingPoint = surfaceHit.Position;
                stateMachine.TransitionToState(CharacterState.LedgeStandingUp, ref context, ref baseContext, in aspect);
                return true;
            }
        }

        return aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }

    public static bool IsLedgeGrabBlocked(in PlatformerCharacterComponent character)
    {
        return character.LedgeGrabBlockCounter > 0f;
    }

    public static bool CanGrabLedge(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect, out Entity ledgeEntity, out ColliderCastHit ledgeSurfaceHit)
    {
        ledgeEntity = Entity.Null;
        ledgeSurfaceHit = default;
        
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;

        if (IsLedgeGrabBlocked(in character))
        {
            return false;
        }

        LedgeDetection(
            ref context,
            ref baseContext,
            in aspect,
            characterPosition,
            characterRotation,
            out bool ledgeIsValid,
            out ledgeSurfaceHit,
            out ColliderCastHit forwardHit,
            out float3 characterTranslationAtLedgeSurface,
            out bool wouldBeGroundedOnLedgeSurfaceHit,
            out float forwardHitDistance,
            out bool isObstructedAtSurface,
            out bool isObstructedAtCurrentPosition,
            out float upOffsetToPlaceLedgeDetectionPointAtLedgeLevel);

        // Prevent detecting valid grab if going up
        if (math.dot(characterBody.RelativeVelocity, ledgeSurfaceHit.SurfaceNormal) > 0f)
        {
            ledgeIsValid = false;
        }

        if (ledgeIsValid)
        {
            ledgeEntity = ledgeSurfaceHit.Entity;
        }

        return ledgeIsValid && !isObstructedAtSurface;
    }

    public static void LedgeDetection(
        ref PlatformerCharacterUpdateContext context,
        ref KinematicCharacterUpdateContext baseContext,
        in PlatformerCharacterAspect aspect,
        float3 atCharacterTranslation,
        quaternion atCharacterRotation,
        out bool ledgeIsValid,
        out ColliderCastHit surfaceHit,
        out ColliderCastHit forwardHit,
        out float3 characterTranslationAtLedgeSurface,
        out bool wouldBeGroundedOnLedgeSurfaceHit,
        out float forwardHitDistance,
        out bool isObstructedAtSurface,
        out bool isObstructedAtCurrentPosition,
        out float upOffsetToPlaceLedgeDetectionPointAtLedgeLevel)
    {
        const float ledgeProbingToleranceOffset = 0.04f;

        ledgeIsValid = false;
        surfaceHit = default;
        forwardHit = default;
        characterTranslationAtLedgeSurface = default;
        wouldBeGroundedOnLedgeSurfaceHit = false;
        forwardHitDistance = -1f;
        isObstructedAtSurface = false;
        isObstructedAtCurrentPosition = false;
        upOffsetToPlaceLedgeDetectionPointAtLedgeLevel = -1f;
        
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        float characterScale = aspect.CharacterAspect.LocalTransform.ValueRO.Scale;

        float3 currentCharacterForward = MathUtilities.GetForwardFromRotation(atCharacterRotation);
        float3 currentCharacterRight = MathUtilities.GetRightFromRotation(atCharacterRotation);
        RigidTransform currentCharacterRigidTransform = math.RigidTransform(atCharacterRotation, atCharacterTranslation);
        float3 worldSpaceLedgeDetectionPoint = math.transform(currentCharacterRigidTransform, character.LocalLedgeDetectionPoint);
        float forwardDepthOfLedgeDetectionPoint = math.length(math.projectsafe(worldSpaceLedgeDetectionPoint - atCharacterTranslation, currentCharacterForward));

        // Forward detection against the ledge wall
        bool forwardHitDetected = false;
        if (aspect.CharacterAspect.CastColliderClosestCollisions(
                in aspect,
                ref context,
                ref baseContext,
                atCharacterTranslation,
                atCharacterRotation,
                characterScale,
                currentCharacterForward,
                forwardDepthOfLedgeDetectionPoint,
                false,
                characterProperties.ShouldIgnoreDynamicBodies(),
                out forwardHit,
                out forwardHitDistance))
        {
            forwardHitDetected = true;

            if (aspect.CharacterAspect.CalculateDistanceClosestCollisions(
                    in aspect,
                    ref context,
                    ref baseContext,
                    atCharacterTranslation,
                    atCharacterRotation,
                    characterScale,
                    0f,
                    characterProperties.ShouldIgnoreDynamicBodies(),
                    out DistanceHit closestOverlapHit))
            {
                if (closestOverlapHit.Distance <= 0f)
                {
                    isObstructedAtCurrentPosition = true;
                }
            }
        }

        // Cancel rest of detection if no forward hit detected
        if (!forwardHitDetected)
        {
            return;
        }

        // Cancel rest of detection if currently obstructed
        if (isObstructedAtCurrentPosition)
        {
            return;
        }

        // Raycast downward at detectionPoint to find a surface hit
        bool surfaceRaycastHitDetected = false;
        float3 startPointOfSurfaceDetectionRaycast = worldSpaceLedgeDetectionPoint + (characterBody.GroundingUp * character.LedgeSurfaceProbingHeight);
        float surfaceRaycastLength = character.LedgeSurfaceProbingHeight + ledgeProbingToleranceOffset;
        if (aspect.CharacterAspect.RaycastClosestCollisions(
                in aspect,
                ref context,
                ref baseContext,
                startPointOfSurfaceDetectionRaycast,
                -characterBody.GroundingUp,
                surfaceRaycastLength,
                characterProperties.ShouldIgnoreDynamicBodies(),
                out RaycastHit surfaceRaycastHit,
                out float surfaceRaycastHitDistance))
        {
            if (surfaceRaycastHit.Fraction > 0f)
            {
                surfaceRaycastHitDetected = true;
            }
        }

        // If no ray hit found, do more raycast tests on the sides
        if (!surfaceRaycastHitDetected)
        {
            float3 rightStartPointOfSurfaceDetectionRaycast = startPointOfSurfaceDetectionRaycast + (currentCharacterRight * character.LedgeSideProbingLength);
            if (aspect.CharacterAspect.RaycastClosestCollisions(
                    in aspect,
                    ref context,
                    ref baseContext,
                    rightStartPointOfSurfaceDetectionRaycast,
                    -characterBody.GroundingUp,
                    surfaceRaycastLength,
                    characterProperties.ShouldIgnoreDynamicBodies(),
                    out surfaceRaycastHit,
                    out surfaceRaycastHitDistance))
            {
                if (surfaceRaycastHit.Fraction > 0f)
                {
                    surfaceRaycastHitDetected = true;
                }
            }
        }
        if (!surfaceRaycastHitDetected)
        {
            float3 leftStartPointOfSurfaceDetectionRaycast = startPointOfSurfaceDetectionRaycast - (currentCharacterRight * character.LedgeSideProbingLength);
            if (aspect.CharacterAspect.RaycastClosestCollisions(
                    in aspect,
                    ref context,
                    ref baseContext,
                    leftStartPointOfSurfaceDetectionRaycast,
                    -characterBody.GroundingUp,
                    surfaceRaycastLength,
                    characterProperties.ShouldIgnoreDynamicBodies(),
                    out surfaceRaycastHit,
                    out surfaceRaycastHitDistance))
            {
                if (surfaceRaycastHit.Fraction > 0f)
                {
                    surfaceRaycastHitDetected = true;
                }
            }
        }

        // Cancel rest of detection if no surface raycast hit detected
        if (!surfaceRaycastHitDetected)
        {
            return;
        }

        // Cancel rest of detection if surface hit is dynamic
        if (PhysicsUtilities.IsBodyDynamic(baseContext.PhysicsWorld, surfaceRaycastHit.RigidBodyIndex))
        {
            return;
        }

        ledgeIsValid = true;

        upOffsetToPlaceLedgeDetectionPointAtLedgeLevel = surfaceRaycastLength - surfaceRaycastHitDistance;

        // Note: this assumes that our transform pivot is at the base of our capsule collider
        float3 startPointOfSurfaceObstructionDetectionCast = surfaceRaycastHit.Position + (characterBody.GroundingUp * character.LedgeSurfaceObstructionProbingHeight);

        // Check obstructions at surface hit point
        if (aspect.CharacterAspect.CastColliderClosestCollisions(
                in aspect,
                ref context,
                ref baseContext,
                startPointOfSurfaceObstructionDetectionCast,
                atCharacterRotation,
                characterScale,
                -characterBody.GroundingUp,
                character.LedgeSurfaceObstructionProbingHeight + ledgeProbingToleranceOffset,
                false,
                characterProperties.ShouldIgnoreDynamicBodies(),
                out surfaceHit,
                out float closestSurfaceObstructionHitDistance))
        {
            if (surfaceHit.Fraction <= 0f)
            {
                isObstructedAtSurface = true;
            }
        }

        // Cancel rest of detection if obstruction at surface
        if (isObstructedAtSurface)
        {
            return;
        }

        // Cancel rest of detection if found no surface hit
        if (surfaceHit.Entity == Entity.Null)
        {
            return;
        }

        characterTranslationAtLedgeSurface = startPointOfSurfaceObstructionDetectionCast + (-characterBody.GroundingUp * closestSurfaceObstructionHitDistance);

        wouldBeGroundedOnLedgeSurfaceHit = aspect.IsGroundedOnHit(ref context, ref baseContext, new BasicHit(surfaceHit), 0);
    }
}