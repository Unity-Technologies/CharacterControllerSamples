using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public struct CrouchedState : IPlatformerCharacterState
{
    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;

        aspect.SetCapsuleGeometry(character.CrouchingGeometry.ToCapsuleGeometry());
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;

        character.IsOnStickySurface = false;
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;

        aspect.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, true);

        // Rotate move input and velocity to take into account parent rotation
        if(characterBody.ParentEntity != Entity.Null)
        {
            characterControl.MoveVector = math.rotate(characterBody.RotationFromParent, characterControl.MoveVector);
            characterBody.RelativeVelocity = math.rotate(characterBody.RotationFromParent, characterBody.RelativeVelocity);
        }

        float chosenMaxSpeed = character.CrouchedMaxSpeed;
        
        float chosenSharpness = character.CrouchedMovementSharpness;
        if (context.CharacterFrictionModifierLookup.TryGetComponent(characterBody.GroundHit.Entity, out CharacterFrictionModifier frictionModifier))
        {
            chosenSharpness *= frictionModifier.Friction;
        }
        
        float3 moveVectorOnPlane = math.normalizesafe(MathUtilities.ProjectOnPlane(characterControl.MoveVector, characterBody.GroundingUp)) * math.length(characterControl.MoveVector);
        float3 targetVelocity = moveVectorOnPlane * chosenMaxSpeed;
        CharacterControlUtilities.StandardGroundMove_Interpolated(ref characterBody.RelativeVelocity, targetVelocity, chosenSharpness, deltaTime, characterBody.GroundingUp, characterBody.GroundHit.Normal);
        
        aspect.HandlePhysicsUpdatePhase2(ref context, ref baseContext, true, true, true, true, true);

        DetectTransitions(ref context, ref baseContext, in aspect);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;
        
        if (math.lengthsq(characterControl.MoveVector) > 0f)
        {
            CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation, deltaTime, math.normalizesafe(characterControl.MoveVector), MathUtilities.GetUpFromRotation(characterRotation), character.CrouchedRotationSharpness);
        }
        
        character.IsOnStickySurface = PhysicsUtilities.HasPhysicsTag(in baseContext.PhysicsWorld, characterBody.GroundHit.RigidBodyIndex, character.StickySurfaceTag);
        if (character.IsOnStickySurface)
        {
            CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation, deltaTime, characterBody.GroundHit.Normal, character.UpOrientationAdaptationSharpness);
        }
        else 
        {
            CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation, deltaTime, math.normalizesafe(-customGravity.Gravity), character.UpOrientationAdaptationSharpness);
        }
    }

    public void GetCameraParameters(in PlatformerCharacterComponent character, out Entity cameraTarget, out bool calculateUpFromGravity)
    {
        cameraTarget = character.CrouchingCameraTargetEntity;
        calculateUpFromGravity = !character.IsOnStickySurface;
    }

    public void GetMoveVectorFromPlayerInput(in PlatformerPlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
    {
        PlatformerCharacterAspect.GetCommonMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
    }

    public bool DetectTransitions(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref PlatformerCharacterStateMachine stateMachine = ref aspect.StateMachine.ValueRW;
        
        if (characterControl.CrouchPressed)
        {
            if (aspect.CanStandUp(ref context, ref baseContext))
            {
                if (characterBody.IsGrounded)
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

        if (!characterBody.IsGrounded)
        {
            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
            return true;
        }

        return aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }
}