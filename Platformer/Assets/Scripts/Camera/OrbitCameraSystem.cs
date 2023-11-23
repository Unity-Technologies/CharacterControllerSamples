using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.CharacterController;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct OrbitCameraSystem : ISystem
{
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
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<OrbitCamera, OrbitCameraControl>().Build());
    }

    public void OnDestroy(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        OrbitCameraJob job = new OrbitCameraJob
        {
            TimeData = SystemAPI.Time,
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
            KinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
            PlatformerCharacterComponentLookup = SystemAPI.GetComponentLookup<PlatformerCharacterComponent>(true),
            PlatformerCharacterStateMachineLookup = SystemAPI.GetComponentLookup<PlatformerCharacterStateMachine>(true),
            CustomGravityLookup = SystemAPI.GetComponentLookup<CustomGravity>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct OrbitCameraJob : IJobEntity
    {
        public TimeData TimeData;
        public PhysicsWorld PhysicsWorld;

        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly] public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup;
        [ReadOnly] public ComponentLookup<PlatformerCharacterComponent> PlatformerCharacterComponentLookup;
        [ReadOnly] public ComponentLookup<PlatformerCharacterStateMachine> PlatformerCharacterStateMachineLookup;
        [ReadOnly] public ComponentLookup<CustomGravity> CustomGravityLookup;

        void Execute(
            Entity entity,
            ref LocalTransform localTransform,
            ref OrbitCamera orbitCamera,
            in OrbitCameraControl cameraControl,
            in DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer)
        {
            float elapsedTime = (float)TimeData.ElapsedTime;
            
            if (LocalToWorldLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out LocalToWorld characterLTW) &&
                CustomGravityLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out CustomGravity characterCustomGravity) &&
                PlatformerCharacterComponentLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out PlatformerCharacterComponent characterComponent) &&
                PlatformerCharacterStateMachineLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out PlatformerCharacterStateMachine characterStateMachine))
            {
                // Camera target handling
                {
                    characterStateMachine.GetCameraParameters(characterStateMachine.CurrentState, in characterComponent, out Entity selectedCameraTarget, out bool calculateUpFromGravity);

                    RigidTransform selectedCameraTargetTransform = default;
                    if (LocalToWorldLookup.TryGetComponent(selectedCameraTarget, out LocalToWorld camTargetLTW))
                    {
                        selectedCameraTargetTransform = new RigidTransform(camTargetLTW.Rotation, camTargetLTW.Position);
                    }
                    else
                    {
                        selectedCameraTargetTransform = new RigidTransform(characterLTW.Rotation, characterLTW.Position);
                    }
                    if (calculateUpFromGravity)
                    {
                        selectedCameraTargetTransform.rot = MathUtilities.CreateRotationWithUpPriority(math.normalizesafe(-characterCustomGravity.Gravity), math.mul(selectedCameraTargetTransform.rot, math.forward()));
                    }

                    // Detect transition
                    if (orbitCamera.ActiveCameraTarget != selectedCameraTarget ||
                        orbitCamera.PreviousCalculateUpFromGravity != calculateUpFromGravity)
                    {
                        orbitCamera.CameraTargetTransitionStartTime = elapsedTime;
                        orbitCamera.CameraTargetTransitionFromTransform = orbitCamera.CameraTargetTransform;
                        orbitCamera.ActiveCameraTarget = selectedCameraTarget;
                        orbitCamera.PreviousCalculateUpFromGravity = calculateUpFromGravity;
                    }

                    // Update transitions
                    if (elapsedTime < orbitCamera.CameraTargetTransitionStartTime + orbitCamera.CameraTargetTransitionTime)
                    {
                        float3 previousCameraTargetPosition = default;
                        if (LocalToWorldLookup.TryGetComponent(orbitCamera.PreviousCameraTarget, out LocalToWorld previousCamTargetLTW))
                        {
                            previousCameraTargetPosition = previousCamTargetLTW.Position;
                        }
                        else
                        {
                            previousCameraTargetPosition = characterLTW.Position;
                        }
                        
                        float transitionRatio = math.saturate((elapsedTime - orbitCamera.CameraTargetTransitionStartTime) / orbitCamera.CameraTargetTransitionTime);
                        orbitCamera.CameraTargetTransform.pos = math.lerp(previousCameraTargetPosition, selectedCameraTargetTransform.pos, transitionRatio);
                        orbitCamera.CameraTargetTransform.rot = math.slerp(orbitCamera.CameraTargetTransitionFromTransform.rot, selectedCameraTargetTransform.rot, transitionRatio);
                    }
                    else
                    {
                        orbitCamera.CameraTargetTransform = selectedCameraTargetTransform;
                        orbitCamera.PreviousCameraTarget = orbitCamera.ActiveCameraTarget;
                    }
                }

                float3 cameraTargetUp = math.mul(orbitCamera.CameraTargetTransform.rot, math.up());
                
                // Rotation
                {
                    localTransform.Rotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, cameraTargetUp);

                    // Handle rotating the camera along with character's parent entity (moving platform)
                    if (orbitCamera.RotateWithCharacterParent && KinematicCharacterBodyLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out KinematicCharacterBody characterBody))
                    {
                        KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref localTransform.Rotation, characterBody.RotationFromParent, TimeData.DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
                        orbitCamera.PlanarForward = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(localTransform.Rotation), cameraTargetUp));
                    }

                    // Yaw
                    float yawAngleChange = cameraControl.LookDegreesDelta.x * orbitCamera.RotationSpeed;
                    quaternion yawRotation = quaternion.Euler(cameraTargetUp * math.radians(yawAngleChange));
                    orbitCamera.PlanarForward = math.rotate(yawRotation, orbitCamera.PlanarForward);

                    // Pitch
                    orbitCamera.PitchAngle += -cameraControl.LookDegreesDelta.y * orbitCamera.RotationSpeed;
                    orbitCamera.PitchAngle = math.clamp(orbitCamera.PitchAngle, orbitCamera.MinVAngle, orbitCamera.MaxVAngle);
                    quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(orbitCamera.PitchAngle));

                    // Final rotation
                    localTransform.Rotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, cameraTargetUp);
                    localTransform.Rotation = math.mul(localTransform.Rotation, pitchRotation);
                }

                float3 cameraForward = MathUtilities.GetForwardFromRotation(localTransform.Rotation);

                // Distance input
                float desiredDistanceMovementFromInput = cameraControl.ZoomDelta * orbitCamera.DistanceMovementSpeed;
                orbitCamera.TargetDistance = math.clamp(orbitCamera.TargetDistance + desiredDistanceMovementFromInput, orbitCamera.MinDistance, orbitCamera.MaxDistance);
                orbitCamera.SmoothedTargetDistance = math.lerp(orbitCamera.SmoothedTargetDistance, orbitCamera.TargetDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.DistanceMovementSharpness, TimeData.DeltaTime));

                // Obstructions
                if (orbitCamera.ObstructionRadius > 0f)
                {
                    float obstructionCheckDistance = orbitCamera.SmoothedTargetDistance;

                    CameraObstructionHitsCollector collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                    PhysicsWorld.SphereCastCustom<CameraObstructionHitsCollector>(
                        orbitCamera.CameraTargetTransform.pos,
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
                                hitBody.WorldFromBody = new RigidTransform(quaternion.LookRotationSafe(hitBodyLocalToWorld.Forward, hitBodyLocalToWorld.Up), hitBodyLocalToWorld.Position);

                                collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                                hitBody.SphereCastCustom<CameraObstructionHitsCollector>(
                                    orbitCamera.CameraTargetTransform.pos,
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
                        orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionOuterSmoothingSharpness, TimeData.DeltaTime));
                    }
                    else if (orbitCamera.ObstructedDistance > newObstructedDistance)
                    {
                        // Move inner
                        orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionInnerSmoothingSharpness, TimeData.DeltaTime));
                    }
                }
                else
                {
                    orbitCamera.ObstructedDistance = orbitCamera.SmoothedTargetDistance;
                }

                // Calculate final camera position from targetposition + rotation + distance
                localTransform.Position = orbitCamera.CameraTargetTransform.pos + (-cameraForward * orbitCamera.ObstructedDistance);

                // Manually calculate the LocalToWorld since this is updating after the Transform systems, and the LtW is what rendering uses
                LocalToWorld cameraLocalToWorld = new LocalToWorld();
                cameraLocalToWorld.Value = new float4x4(localTransform.Rotation, localTransform.Position);
                LocalToWorldLookup[entity] = cameraLocalToWorld;
            }
        }
    }
}