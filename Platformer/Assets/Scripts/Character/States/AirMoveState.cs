using Unity.Mathematics;
using Unity.Physics;
using Unity.Entities;
using Unity.CharacterController;

public struct AirMoveState : IPlatformerCharacterState
{
    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        
        aspect.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    { }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        float elapsedTime = (float)baseContext.Time.ElapsedTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;
        
        aspect.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, true);

        // Move
        float3 airAcceleration = characterControl.MoveVector * character.AirAcceleration;
        if (math.lengthsq(airAcceleration) > 0f)
        {
            float3 tmpVelocity = characterBody.RelativeVelocity;
            CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration, character.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

            // Cancel air acceleration from input if we would hit a non-grounded surface (prevents air-climbing slopes at high air accelerations)
            if (aspect.CharacterAspect.MovementWouldHitNonGroundedObstruction(in aspect, ref context, ref baseContext, characterBody.RelativeVelocity * deltaTime, out ColliderCastHit hit))
            {
                characterBody.RelativeVelocity = tmpVelocity;
                
                character.HasDetectedMoveAgainstWall = true;
                character.LastKnownWallNormal = hit.SurfaceNormal;
            }
        }
        
        // Jumping
        {
            if (characterControl.JumpPressed)
            {
                // Allow jumping shortly after getting degrounded
                if (character.AllowJumpAfterBecameUngrounded && elapsedTime < character.LastTimeWasGrounded + character.JumpAfterUngroundedGraceTime)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * character.GroundJumpSpeed, true, characterBody.GroundingUp);
                    character.HeldJumpTimeCounter = 0f;
                }
                // Air jumps
                else if (character.CurrentUngroundedJumps < character.MaxUngroundedJumps)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * character.AirJumpSpeed, true, characterBody.GroundingUp);
                    character.CurrentUngroundedJumps++;
                }
                // Remember that we wanted to jump before we became grounded
                else
                {
                    character.JumpPressedBeforeBecameGrounded = true;
                }

                character.AllowJumpAfterBecameUngrounded = false;
            }
            
            // Additional jump power when holding jump
            if (character.AllowHeldJumpInAir && characterControl.JumpHeld && character.HeldJumpTimeCounter < character.MaxHeldJumpTime)
            {
                characterBody.RelativeVelocity += characterBody.GroundingUp * character.JumpHeldAcceleration * deltaTime;
            }
        }
        
        // Gravity
        CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, customGravity.Gravity, deltaTime);

        // Drag
        CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, character.AirDrag);

        aspect.HandlePhysicsUpdatePhase2(ref context, ref baseContext, true, true, true, true, true);

        DetectTransitions(ref context, ref baseContext, in aspect);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;
        
        if (math.lengthsq(characterControl.MoveVector) > 0f)
        {
            CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation, deltaTime, math.normalizesafe(characterControl.MoveVector), MathUtilities.GetUpFromRotation(characterRotation), character.AirRotationSharpness);
        }
        CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation, deltaTime, math.normalizesafe(-customGravity.Gravity), character.UpOrientationAdaptationSharpness);
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
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref PlatformerCharacterStateMachine stateMachine = ref aspect.StateMachine.ValueRW;
        
        if (characterControl.RopePressed && RopeSwingState.DetectRopePoints(in baseContext.PhysicsWorld, in aspect, out float3 detectedRopeAnchorPoint))
        {
            stateMachine.RopeSwingState.AnchorPoint = detectedRopeAnchorPoint;
            stateMachine.TransitionToState(CharacterState.RopeSwing, ref context, ref baseContext, in aspect);
            return true;
        }

        if (characterControl.RollHeld)
        {
            stateMachine.TransitionToState(CharacterState.Rolling, ref context, ref baseContext, in aspect);
            return true;
        }

        if (characterControl.DashPressed)
        {
            stateMachine.TransitionToState(CharacterState.Dashing, ref context, ref baseContext, in aspect);
            return true;
        }

        if (characterBody.IsGrounded)
        {
            stateMachine.TransitionToState(CharacterState.GroundMove, ref context, ref baseContext, in aspect);
            return true;
        }

        if (characterControl.SprintHeld && character.HasDetectedMoveAgainstWall)
        {
            stateMachine.TransitionToState(CharacterState.WallRun, ref context, ref baseContext, in aspect);
            return true;
        }

        if (LedgeGrabState.CanGrabLedge(ref context, ref baseContext, in aspect, out Entity ledgeEntity, out ColliderCastHit ledgeSurfaceHit))
        {
            stateMachine.TransitionToState(CharacterState.LedgeGrab, ref context, ref baseContext, in aspect);
            aspect.CharacterAspect.SetOrUpdateParentBody(ref baseContext, ref characterBody, ledgeEntity, ledgeSurfaceHit.Position); 
            return true;
        }

        if (characterControl.ClimbPressed)
        {
            if (ClimbingState.CanStartClimbing(ref context, ref baseContext, in aspect))
            {
                stateMachine.TransitionToState(CharacterState.Climbing, ref context, ref baseContext, in aspect);
                return true;
            }
        }

        return aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }
}