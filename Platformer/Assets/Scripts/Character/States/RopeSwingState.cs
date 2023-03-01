using Unity.Entities;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public struct RopeSwingState : IPlatformerCharacterState
{
    public float3 AnchorPoint;

    public void OnStateEnter(CharacterState previousState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        Entity entity = aspect.CharacterAspect.Entity;
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        
        aspect.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
        
        characterProperties.EvaluateGrounding = false;

        // Spawn rope
        Entity ropeInstanceEntity = context.EndFrameECB.Instantiate(context.ChunkIndex, character.RopePrefabEntity);
        context.EndFrameECB.AddComponent(context.ChunkIndex, ropeInstanceEntity, new CharacterRope { OwningCharacterEntity = entity });
    }

    public void OnStateExit(CharacterState nextState, ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        ref KinematicCharacterProperties characterProperties = ref aspect.CharacterAspect.CharacterProperties.ValueRW;
        
        characterProperties.EvaluateGrounding = true;
        // Note: rope despawning is handled by the rope system itself
    }

    public void OnStatePhysicsUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref KinematicCharacterBody characterBody = ref aspect.CharacterAspect.CharacterBody.ValueRW;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        CustomGravity customGravity = aspect.CustomGravity.ValueRO;
        quaternion characterRotation = aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        
        aspect.HandlePhysicsUpdatePhase1(ref context, ref baseContext, false, false);

        // Move
        float3 moveVectorOnPlane = math.normalizesafe(MathUtilities.ProjectOnPlane(characterControl.MoveVector, characterBody.GroundingUp)) * math.length(characterControl.MoveVector);
        float3 acceleration = moveVectorOnPlane * character.RopeSwingAcceleration;
        CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, acceleration, character.RopeSwingMaxSpeed, characterBody.GroundingUp, deltaTime, false);

        // Gravity
        CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, customGravity.Gravity, deltaTime);

        // Drag
        CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, character.RopeSwingDrag);

        // Rope constraint
        RigidTransform characterTransform = new RigidTransform(characterRotation, characterPosition);
        ConstrainToRope(ref characterPosition, ref characterBody.RelativeVelocity, character.RopeLength, AnchorPoint, math.transform(characterTransform, character.LocalRopeAnchorPoint));

        aspect.HandlePhysicsUpdatePhase2(ref context, ref baseContext, false, false, true, false, false);

        DetectTransitions(ref context, ref baseContext, in aspect);
    }

    public void OnStateVariableUpdate(ref PlatformerCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext, in PlatformerCharacterAspect aspect)
    {
        float deltaTime = baseContext.Time.DeltaTime;
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref PlatformerCharacterControl characterControl = ref aspect.CharacterControl.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        ref quaternion characterRotation = ref aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;
        
        if (math.lengthsq(characterControl.MoveVector) > 0f)
        {
            CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation, deltaTime, math.normalizesafe(characterControl.MoveVector), MathUtilities.GetUpFromRotation(characterRotation), character.AirRotationSharpness);
        }
        CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation, deltaTime, math.normalizesafe(AnchorPoint - characterPosition), character.UpOrientationAdaptationSharpness);
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
        
        if (characterControl.JumpPressed || characterControl.DashPressed)
        {
            stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
            return true;
        }
        
        return aspect.DetectGlobalTransitions(ref context, ref baseContext);
    }

    public static bool DetectRopePoints(in PhysicsWorld physicsWorld, in PlatformerCharacterAspect aspect, out float3 point)
    {
        point = default;
        
        ref PlatformerCharacterComponent character = ref aspect.Character.ValueRW;
        ref float3 characterPosition = ref aspect.CharacterAspect.LocalTransform.ValueRW.Position;
        quaternion characterRotation = aspect.CharacterAspect.LocalTransform.ValueRW.Rotation;

        RigidTransform characterTransform = new RigidTransform(characterRotation, characterPosition);
        float3 ropeDetectionPoint = math.transform(characterTransform, character.LocalRopeAnchorPoint);

        CollisionFilter ropeAnchorDetectionFilter = CollisionFilter.Default;
        ropeAnchorDetectionFilter.CollidesWith = character.RopeAnchorCategory.Value;

        PointDistanceInput pointInput = new PointDistanceInput
        {
            Filter = ropeAnchorDetectionFilter,
            MaxDistance = character.RopeLength,
            Position = ropeDetectionPoint,
        };

        if (physicsWorld.CalculateDistance(pointInput, out DistanceHit closestHit))
        {
            point = closestHit.Position;
            return true;
        }

        return false;
    }

    public static void ConstrainToRope(
        ref float3 translation,
        ref float3 velocity,
        float ropeLength,
        float3 ropeAnchorPoint,
        float3 ropeAnchorPointOnCharacter)
    {
        float3 characterToRopeVector = ropeAnchorPoint - ropeAnchorPointOnCharacter;
        float3 ropeNormal = math.normalizesafe(characterToRopeVector);

        if (math.length(characterToRopeVector) >= ropeLength)
        {
            float3 targetAnchorPointOnCharacter = ropeAnchorPoint - MathUtilities.ClampToMaxLength(characterToRopeVector, ropeLength);
            translation += (targetAnchorPointOnCharacter - ropeAnchorPointOnCharacter);

            if (math.dot(velocity, ropeNormal) < 0f)
            {
                velocity = MathUtilities.ProjectOnPlane(velocity, ropeNormal);
            }
        }
    }
}