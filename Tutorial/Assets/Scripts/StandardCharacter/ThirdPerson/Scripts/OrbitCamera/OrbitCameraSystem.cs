using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ThirdPersonPlayerVariableStepControlSystem))]
[UpdateAfter(typeof(ThirdPersonCharacterVariableUpdateSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct OrbitCameraSimulationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<OrbitCamera, OrbitCameraControl>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        OrbitCameraSimulationJob job = new OrbitCameraSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
            CameraTargetLookup = SystemAPI.GetComponentLookup<CameraTarget>(true),
            KinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct OrbitCameraSimulationJob : IJobEntity
    {
        public float DeltaTime;

        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public ComponentLookup<Parent> ParentLookup;
        [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        [ReadOnly] public ComponentLookup<CameraTarget> CameraTargetLookup;
        [ReadOnly] public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup;

        void Execute(Entity entity, ref OrbitCamera orbitCamera, in OrbitCameraControl cameraControl)
        {
            if (OrbitCameraUtilities.TryGetCameraTargetSimulationWorldTransform(
                    cameraControl.FollowedCharacterEntity,
                    ref LocalTransformLookup,
                    ref ParentLookup,
                    ref PostTransformMatrixLookup,
                    ref CameraTargetLookup, 
                    out float4x4 targetWorldTransform))
            {
                float3 targetUp = targetWorldTransform.Up();
                float3 targetPosition = targetWorldTransform.Translation();

                // Update planar forward based on target up direction and rotation from parent
                {
                    quaternion tmpPlanarRotation = MathUtilities.CreateRotationWithUpPriority(targetUp, orbitCamera.PlanarForward);
                    
                    // Rotation from character parent 
                    if (orbitCamera.RotateWithCharacterParent &&
                        KinematicCharacterBodyLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out KinematicCharacterBody characterBody))
                    {
                        // Only consider rotation around the character up, since the camera is already adjusting itself to character up
                        quaternion planarRotationFromParent = characterBody.RotationFromParent;
                        KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref tmpPlanarRotation, planarRotationFromParent, DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
                    }
                    
                    orbitCamera.PlanarForward = MathUtilities.GetForwardFromRotation(tmpPlanarRotation);
                }

                // Yaw
                float yawAngleChange = cameraControl.LookDegreesDelta.x * orbitCamera.RotationSpeed;
                quaternion yawRotation = quaternion.Euler(targetUp * math.radians(yawAngleChange));
                orbitCamera.PlanarForward = math.rotate(yawRotation, orbitCamera.PlanarForward);
                
                // Pitch
                orbitCamera.PitchAngle += -cameraControl.LookDegreesDelta.y * orbitCamera.RotationSpeed;
                orbitCamera.PitchAngle = math.clamp(orbitCamera.PitchAngle, orbitCamera.MinVAngle, orbitCamera.MaxVAngle);

                // Calculate final rotation
                quaternion cameraRotation = OrbitCameraUtilities.CalculateCameraRotation(targetUp, orbitCamera.PlanarForward, orbitCamera.PitchAngle);

                // Distance input
                float desiredDistanceMovementFromInput = cameraControl.ZoomDelta * orbitCamera.DistanceMovementSpeed;
                orbitCamera.TargetDistance = math.clamp(orbitCamera.TargetDistance + desiredDistanceMovementFromInput, orbitCamera.MinDistance, orbitCamera.MaxDistance);

                // Calculate camera position (no smoothing or obstructions yet; these are done in the camera late update)
                float3 cameraPosition = OrbitCameraUtilities.CalculateCameraPosition(targetPosition, cameraRotation, orbitCamera.TargetDistance);

                // Write back to component
                LocalTransformLookup[entity] = LocalTransform.FromPositionRotation(cameraPosition, cameraRotation);
            }
        }
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct OrbitCameraLateUpdateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<OrbitCamera, OrbitCameraControl>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        OrbitCameraLateUpdateJob job = new OrbitCameraLateUpdateJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
            CameraTargetLookup = SystemAPI.GetComponentLookup<CameraTarget>(true),
            KinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct OrbitCameraLateUpdateJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly]
        public PhysicsWorld PhysicsWorld;
        
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly] 
        public ComponentLookup<CameraTarget> CameraTargetLookup;
        [ReadOnly] 
        public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup;

        void Execute(
            Entity entity,
            ref OrbitCamera orbitCamera,
            in OrbitCameraControl cameraControl,
            in DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer)
        {
            if (OrbitCameraUtilities.TryGetCameraTargetInterpolatedWorldTransform(
                    cameraControl.FollowedCharacterEntity,
                    ref LocalToWorldLookup,
                    ref CameraTargetLookup,
                    out LocalToWorld targetWorldTransform))
            {
                quaternion cameraRotation = OrbitCameraUtilities.CalculateCameraRotation(targetWorldTransform.Up, orbitCamera.PlanarForward, orbitCamera.PitchAngle);
                
                float3 cameraForward = math.mul(cameraRotation, math.forward());
                float3 targetPosition = targetWorldTransform.Position;
                
                // Distance smoothing
                orbitCamera.SmoothedTargetDistance = math.lerp(orbitCamera.SmoothedTargetDistance, orbitCamera.TargetDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.DistanceMovementSharpness, DeltaTime));
                
                // Obstruction handling
                // Obstruction detection is handled here, because we have to adjust the obstruction distance
                // to match the interpolated physics body transform (as opposed to the "simulation" transform). Otherwise, a
                // camera getting obstructed by a moving physics body would have visible jitter. 
                if (orbitCamera.ObstructionRadius > 0f)
                {
                    float obstructionCheckDistance = orbitCamera.SmoothedTargetDistance;

                    CameraObstructionHitsCollector collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                    PhysicsWorld.SphereCastCustom(
                        targetPosition,
                        orbitCamera.ObstructionRadius,
                        -cameraForward,
                        obstructionCheckDistance,
                        ref collector,
                        CollisionFilter.Default,
                        QueryInteraction.IgnoreTriggers);

                    float newObstructedDistance = obstructionCheckDistance;
                    if (collector.NumHits > 0)
                    {
                        newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;

                        // Redo cast with the interpolated body transform to prevent FixedUpdate jitter in obstruction detection
                        if (orbitCamera.PreventFixedUpdateJitter)
                        {
                            RigidBody hitBody = PhysicsWorld.Bodies[collector.ClosestHit.RigidBodyIndex];
                            if (LocalToWorldLookup.TryGetComponent(hitBody.Entity, out LocalToWorld hitBodyLocalToWorld))
                            {
                                // Adjust the rigidbody transform for interpolation, so we can raycast it in that state
                                hitBody.WorldFromBody = new RigidTransform(quaternion.LookRotationSafe(hitBodyLocalToWorld.Forward, hitBodyLocalToWorld.Up), hitBodyLocalToWorld.Position);

                                collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                                hitBody.SphereCastCustom(
                                    targetPosition,
                                    orbitCamera.ObstructionRadius,
                                    -cameraForward,
                                    obstructionCheckDistance,
                                    ref collector,
                                    CollisionFilter.Default,
                                    QueryInteraction.IgnoreTriggers);

                                if (collector.NumHits > 0)
                                {
                                    newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;
                                }
                            }
                        }
                    }

                    // Update current distance based on obstructed distance
                    if (orbitCamera.ObstructedDistance < newObstructedDistance)
                    {
                        // Move outer
                        orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionOuterSmoothingSharpness, DeltaTime));
                    }
                    else if (orbitCamera.ObstructedDistance > newObstructedDistance)
                    {
                        // Move inner
                        orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionInnerSmoothingSharpness, DeltaTime));
                    }
                }
                else
                {
                    orbitCamera.ObstructedDistance = orbitCamera.SmoothedTargetDistance;
                }
                
                // Place camera at the final distance (includes smoothing and obstructions)
                float3 cameraPosition = OrbitCameraUtilities.CalculateCameraPosition(targetPosition, cameraRotation, orbitCamera.ObstructedDistance);
                
                // Write to LtW
                LocalToWorldLookup[entity] = new LocalToWorld { Value = new float4x4(cameraRotation, cameraPosition) };
            }
        }
    }
}

public static class OrbitCameraUtilities
{
    public static bool TryGetCameraTargetSimulationWorldTransform(
        Entity targetCharacterEntity, 
        ref ComponentLookup<LocalTransform> localTransformLookup,
        ref ComponentLookup<Parent> parentLookup,
        ref ComponentLookup<PostTransformMatrix> postTransformMatrixLookup,
        ref ComponentLookup<CameraTarget> cameraTargetLookup,
        out float4x4 worldTransform)
    {
        bool foundValidCameraTarget = false;
        worldTransform = float4x4.identity;

        // Camera target is either defined by the CameraTarget component, or if not, the transform of the followed character
        if (cameraTargetLookup.TryGetComponent(targetCharacterEntity, out CameraTarget cameraTarget) &&
            localTransformLookup.HasComponent(cameraTarget.TargetEntity))
        {
            TransformHelpers.ComputeWorldTransformMatrix(
                cameraTarget.TargetEntity,
                out worldTransform,
                ref localTransformLookup,
                ref parentLookup,
                ref postTransformMatrixLookup);
            foundValidCameraTarget = true;
        }
        else if (localTransformLookup.TryGetComponent(targetCharacterEntity, out LocalTransform characterLocalTransform))
        {
            worldTransform = float4x4.TRS(characterLocalTransform.Position, characterLocalTransform.Rotation, 1f);
            foundValidCameraTarget = true;
        }

        return foundValidCameraTarget;
    }
    
    public static bool TryGetCameraTargetInterpolatedWorldTransform(
        Entity targetCharacterEntity, 
        ref ComponentLookup<LocalToWorld> localToWorldLookup,
        ref ComponentLookup<CameraTarget> cameraTargetLookup,
        out LocalToWorld worldTransform)
    {
        bool foundValidCameraTarget = false;
        worldTransform = default;

        // Get the interpolated transform of the target
        if (cameraTargetLookup.TryGetComponent(targetCharacterEntity, out CameraTarget cameraTarget) &&
            localToWorldLookup.TryGetComponent(cameraTarget.TargetEntity, out worldTransform))
        {
            foundValidCameraTarget = true;
        }
        else if (localToWorldLookup.TryGetComponent(targetCharacterEntity, out worldTransform))
        {
            foundValidCameraTarget = true;
        }

        return foundValidCameraTarget;
    }
    
    public static quaternion CalculateCameraRotation(float3 targetUp, float3 planarForward, float pitchAngle)
    {
        quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(pitchAngle));
        quaternion cameraRotation = MathUtilities.CreateRotationWithUpPriority(targetUp, planarForward);
        cameraRotation = math.mul(cameraRotation, pitchRotation);
        return cameraRotation;
    }
    
    public static float3 CalculateCameraPosition(float3 targetPosition, quaternion cameraRotation, float distance)
    {
        return targetPosition + (-MathUtilities.GetForwardFromRotation(cameraRotation) * distance);
    }
}

public struct CameraObstructionHitsCollector : ICollector<ColliderCastHit>
{
    public bool EarlyOutOnFirstHit => false;
    public float MaxFraction => 1f;
    public int NumHits { get; private set; }

    public ColliderCastHit ClosestHit;

    private float _closestHitFraction;
    private float3 _cameraDirection;
    private Entity _followedCharacter;
    private DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> _ignoredEntitiesBuffer;

    public CameraObstructionHitsCollector(Entity followedCharacter, DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer, float3 cameraDirection)
    {
        NumHits = 0;
        ClosestHit = default;

        _closestHitFraction = float.MaxValue;
        _cameraDirection = cameraDirection;
        _followedCharacter = followedCharacter;
        _ignoredEntitiesBuffer = ignoredEntitiesBuffer;
    }

    public bool AddHit(ColliderCastHit hit)
    {
        if (_followedCharacter == hit.Entity)
        {
            return false;
        }
        
        if (math.dot(hit.SurfaceNormal, _cameraDirection) < 0f || !PhysicsUtilities.IsCollidable(hit.Material))
        {
            return false;
        }

        for (int i = 0; i < _ignoredEntitiesBuffer.Length; i++)
        {
            if (_ignoredEntitiesBuffer[i].Entity == hit.Entity)
            {
                return false;
            }
        }

        // Process valid hit
        if (hit.Fraction < _closestHitFraction)
        {
            _closestHitFraction = hit.Fraction;
            ClosestHit = hit;
        }
        NumHits++;

        return true;
    }
}