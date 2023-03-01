using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.CharacterController;

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
    public partial struct OrbitCameraJob : IJobEntity
    {
        public float DeltaTime;
        public PhysicsWorld PhysicsWorld;

        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly] public ComponentLookup<CameraTarget> CameraTargetLookup;
        [ReadOnly] public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup;

        void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref OrbitCamera orbitCamera,
            in OrbitCameraControl cameraControl,
            in DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer)
        {
            // if there is a followed entity, place the camera relatively to it
            if (LocalToWorldLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out LocalToWorld characterLTW))
            {
                // Select the real camera target
                LocalToWorld targetEntityLocalToWorld = default;
                if (CameraTargetLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out CameraTarget cameraTarget) &&
                    LocalToWorldLookup.TryGetComponent(cameraTarget.TargetEntity, out LocalToWorld camTargetLTW))
                {
                    targetEntityLocalToWorld = camTargetLTW;
                }
                else
                {
                    targetEntityLocalToWorld = characterLTW;
                }

                // Rotation
                {
                    transform.Rotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, targetEntityLocalToWorld.Up);

                    // Handle rotating the camera along with character's parent entity (moving platform)
                    if (orbitCamera.RotateWithCharacterParent && KinematicCharacterBodyLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out KinematicCharacterBody characterBody))
                    {
                        KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref transform.Rotation, characterBody.RotationFromParent, DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
                        orbitCamera.PlanarForward = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(transform.Rotation), targetEntityLocalToWorld.Up));
                    }

                    // Yaw
                    float yawAngleChange = cameraControl.Look.x * orbitCamera.RotationSpeed;
                    quaternion yawRotation = quaternion.Euler(targetEntityLocalToWorld.Up * math.radians(yawAngleChange));
                    orbitCamera.PlanarForward = math.rotate(yawRotation, orbitCamera.PlanarForward);

                    // Pitch
                    orbitCamera.PitchAngle += -cameraControl.Look.y * orbitCamera.RotationSpeed;
                    orbitCamera.PitchAngle = math.clamp(orbitCamera.PitchAngle, orbitCamera.MinVAngle, orbitCamera.MaxVAngle);
                    quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(orbitCamera.PitchAngle));

                    // Final rotation
                    transform.Rotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, targetEntityLocalToWorld.Up);
                    transform.Rotation = math.mul(transform.Rotation, pitchRotation);
                }

                float3 cameraForward = MathUtilities.GetForwardFromRotation(transform.Rotation);

                // Distance input
                float desiredDistanceMovementFromInput = cameraControl.Zoom * orbitCamera.DistanceMovementSpeed;
                orbitCamera.TargetDistance = math.clamp(orbitCamera.TargetDistance + desiredDistanceMovementFromInput, orbitCamera.MinDistance, orbitCamera.MaxDistance);
                orbitCamera.CurrentDistanceFromMovement = math.lerp(orbitCamera.CurrentDistanceFromMovement, orbitCamera.TargetDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.DistanceMovementSharpness, DeltaTime));

                // Obstructions
                if (orbitCamera.ObstructionRadius > 0f)
                {
                    float obstructionCheckDistance = orbitCamera.CurrentDistanceFromMovement;

                    CameraObstructionHitsCollector collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                    PhysicsWorld.SphereCastCustom<CameraObstructionHitsCollector>(
                        targetEntityLocalToWorld.Position,
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
                                    targetEntityLocalToWorld.Position,
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
                    if (orbitCamera.CurrentDistanceFromObstruction < newObstructedDistance)
                    {
                        // Move outer
                        orbitCamera.CurrentDistanceFromObstruction = math.lerp(orbitCamera.CurrentDistanceFromObstruction, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionOuterSmoothingSharpness, DeltaTime));
                    }
                    else if (orbitCamera.CurrentDistanceFromObstruction > newObstructedDistance)
                    {
                        // Move inner
                        orbitCamera.CurrentDistanceFromObstruction = math.lerp(orbitCamera.CurrentDistanceFromObstruction, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionInnerSmoothingSharpness, DeltaTime));
                    }
                }
                else
                {
                    orbitCamera.CurrentDistanceFromObstruction = orbitCamera.CurrentDistanceFromMovement;
                }

                // Calculate final camera position from targetposition + rotation + distance
                transform.Position = targetEntityLocalToWorld.Position + (-cameraForward * orbitCamera.CurrentDistanceFromObstruction);

                // Manually calculate the LocalToWorld since this is updating after the Transform systems, and the LtW is what rendering uses
                LocalToWorld cameraLocalToWorld = new LocalToWorld();
                cameraLocalToWorld.Value = new float4x4(transform.Rotation, transform.Position);
                LocalToWorldLookup[entity] = cameraLocalToWorld;
            }
        }
    }
}