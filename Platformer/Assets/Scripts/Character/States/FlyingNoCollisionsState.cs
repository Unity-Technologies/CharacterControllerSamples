using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public struct FlyingNoCollisionsState : IPlatformerCharacterState
{
    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PhysicsCollider characterCollider = ref aspect.CharacterAspect.PhysicsCollider.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        
        aspect.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
        
        KinematicCharacterUtilities.SetCollisionDetectionActive(false, ref characterProperties, ref characterCollider);
        characterBody.IsGrounded = false;
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PhysicsCollider characterCollider = ref aspect.CharacterAspect.PhysicsCollider.ValueRW;
        
        KinematicCharacterUtilities.SetCollisionDetectionActive(true, ref characterProperties, ref characterCollider);
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        
        aspect.CharacterAspect.Update_Initialize(in aspect, ref context, ref baseContext, ref characterBody, deltaTime);
        
        // Movement
        float3 targetVelocity = characterControl.MoveVector * character.FlyingMaxSpeed;
        CharacterControlUtilities.InterpolateVelocityTowardsTarget(ref characterBody.RelativeVelocity, targetVelocity, deltaTime, character.FlyingMovementSharpness);
        characterPosition += characterBody.RelativeVelocity * deltaTime;

        aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        
        characterRotation = quaternion.identity;
    }

    public void GetCameraParameters(in PlatformerCharacterComponent character, out Entity cameraTarget, out bool calculateUpFromGravity)
    {
        cameraTarget = character.DefaultCameraTargetEntity;
        calculateUpFromGravity = false;
    }

    public void GetMoveVectorFromPlayerInput(in PlatformerPlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
    {
        PlatformerCharacterAspect.GetCommonMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
        float verticalInput = (inputs.JumpHeld ? 1f : 0f) + (inputs.RollHeld ? -1f : 0f);
        moveVector = MathUtilities.ClampToMaxLength(moveVector + (math.mul(cameraRotation, math.up()) * verticalInput), 1f);
    }
}