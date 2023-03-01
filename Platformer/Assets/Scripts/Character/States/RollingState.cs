using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;

public struct RollingState : IPlatformerCharacterState
{
    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        
        aspect.SetCapsuleGeometry(character.RollingGeometry.ToCapsuleGeometry());
        characterProperties.EvaluateGrounding = false;
        characterBody.IsGrounded = false;

        PlatformerUtilities.SetEntityHierarchyEnabledParallel(true, character.RollballMeshEntity, context.EndFrameECB, context.ChunkIndex, context.LinkedEntityGroupLookup);
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;

        characterProperties.EvaluateGrounding = true;

        PlatformerUtilities.SetEntityHierarchyEnabledParallel(false, character.RollballMeshEntity, context.EndFrameECB, context.ChunkIndex, context.LinkedEntityGroupLookup);
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;

        aspect.HandlePhysicsUpdatePhase1(ref context, ref baseContext, true, false);

        CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, characterControl.MoveVector * character.RollingAcceleration, deltaTime);
        CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, customGravity.Gravity, deltaTime);
        
        aspect.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, true, false, true);

        DetectTransitions(ref context, ref baseContext, in aspect);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;

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
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref PlatformerCharacterStateMachine stateMachine = ref aspect.StateMachine.ValueRW;
        
        if (!characterControl.RollHeld && aspect.CanStandUp(ref context, ref baseContext))
        {
            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
            return true;
        }

        return aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }
}