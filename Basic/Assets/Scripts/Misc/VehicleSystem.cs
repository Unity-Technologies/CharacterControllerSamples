using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using Unity.CharacterController;
using UnityEngine.InputSystem;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateAfter(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct VehicleSystem : ISystem
{
    public struct WheelHitCollector<T> : ICollector<T> where T : struct, IQueryResult
    {
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction => 1f;
        public int NumHits { get; set; }

        public T ClosestHit;

        private Entity _wheelEntity;
        private Entity _vehicleEntity;
        private float _closestHitFraction;

        public void Init(Entity wheelEntity, Entity vehicleEntity)
        {
            _wheelEntity = wheelEntity;
            _vehicleEntity = vehicleEntity;
            _closestHitFraction = float.MaxValue;
        }

        public bool AddHit(T hit)
        {
            if (hit.Entity != _wheelEntity && hit.Entity != _vehicleEntity && hit.Fraction < _closestHitFraction)
            {
                ClosestHit = hit;
                _closestHitFraction = hit.Fraction;

                NumHits = 1;
                return true;
            }

            return false;
        }
    }

    public void OnUpdate(ref SystemState state) 
    {
        float fwdInput = (Keyboard.current.upArrowKey.isPressed ? 1f : 0f) + (Keyboard.current.downArrowKey.isPressed ? -1f : 0f);
        float sideInput = (Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) + (Keyboard.current.leftArrowKey.isPressed ? -1f : 0f);

        VehicleJob job = new VehicleJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            FwdInput = fwdInput,
            SideInput = sideInput,
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            PhysicsColliderLookup = SystemAPI.GetComponentLookup<PhysicsCollider>(true),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct VehicleJob : IJobEntity
    {
        public float DeltaTime;
        public float FwdInput;
        public float SideInput;
        [ReadOnly]
        public CollisionWorld CollisionWorld;
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly]
        public ComponentLookup<PhysicsCollider> PhysicsColliderLookup;

        void Execute(Entity entity, ref PhysicsVelocity physicsVelocity, in Vehicle vehicle, in PhysicsMass physicsMass, in DynamicBuffer<VehicleWheels> vehicleWheelsBuffer)
        {
            LocalTransform localTransform = LocalTransformLookup[entity];
            float3 vehicleUp = MathUtilities.GetUpFromRotation(localTransform.Rotation);
            float3 vehicleForward = MathUtilities.GetForwardFromRotation(localTransform.Rotation);
            float3 vehicleRight = MathUtilities.GetRightFromRotation(localTransform.Rotation);
            float wheelGroundingAmount = 0f;
            float wheelRatio = 1f / (float)vehicleWheelsBuffer.Length;

            // Wheel collision casts
            for (int i = 0; i < vehicleWheelsBuffer.Length; i++)
            {
                VehicleWheels wheel = vehicleWheelsBuffer[i];

                LocalTransform wheelLocalTransform = LocalTransformLookup[wheel.CollisionEntity];

                ColliderCastInput castInput = new ColliderCastInput(PhysicsColliderLookup[wheel.CollisionEntity].Value, wheelLocalTransform.Position, wheelLocalTransform.Position + (-vehicleUp * vehicle.WheelSuspensionDistance), wheelLocalTransform.Rotation);
                WheelHitCollector<ColliderCastHit> collector = default;
                collector.Init(wheel.CollisionEntity, entity);

                float hitDistance = vehicle.WheelSuspensionDistance;
                if (CollisionWorld.CastCollider(castInput, ref collector))
                {
                    hitDistance = collector.ClosestHit.Fraction * vehicle.WheelSuspensionDistance;

                    wheelGroundingAmount += wheelRatio;

                    // Suspension
                    float suspensionCompressedRatio = 1f - (hitDistance / vehicle.WheelSuspensionDistance);

                    // Add suspension force
                    float3 vehicleVelocityAtWheelPoint = physicsVelocity.GetLinearVelocity(physicsMass, localTransform.Position, localTransform.Rotation, wheelLocalTransform.Position);
                    float vehicleVelocityInUpDirection = math.dot(vehicleVelocityAtWheelPoint, vehicleUp);
                    float suspensionImpulseVelocityChangeOnUpDirection = suspensionCompressedRatio * vehicle.WheelSuspensionStrength;

                    suspensionImpulseVelocityChangeOnUpDirection -= vehicleVelocityInUpDirection;
                    if (suspensionImpulseVelocityChangeOnUpDirection > 0f)
                    {
                        physicsVelocity.ApplyImpulse(in physicsMass, in localTransform.Position, in localTransform.Rotation, vehicleUp * suspensionImpulseVelocityChangeOnUpDirection, wheelLocalTransform.Position);
                    }
                }

                // Place wheel mesh at goal position
                LocalTransform wheelMeshTransform = LocalTransformLookup[wheel.MeshEntity];
                wheelMeshTransform.Position = -math.up() * hitDistance;
                LocalTransformLookup[wheel.MeshEntity] = wheelMeshTransform;
            }

            if (wheelGroundingAmount > 0f)
            {
                float chosenAcceleration = wheelGroundingAmount * vehicle.Acceleration;

                // Acceleration
                float3 addedVelocityFromAcceleration = vehicleForward * FwdInput * chosenAcceleration * DeltaTime;
                float3 tmpNewVelocity = physicsVelocity.Linear + addedVelocityFromAcceleration;
                tmpNewVelocity = MathUtilities.ClampToMaxLength(tmpNewVelocity, vehicle.MaxSpeed);
                addedVelocityFromAcceleration = tmpNewVelocity - physicsVelocity.Linear;
                physicsVelocity.Linear += addedVelocityFromAcceleration;

                // Friction & Roll resistance
                float3 upVelocity = math.projectsafe(physicsVelocity.Linear, vehicleUp);
                float3 fwdVelocity = math.projectsafe(physicsVelocity.Linear, vehicleForward);
                float3 lateralVelocity = math.projectsafe(physicsVelocity.Linear, vehicleRight);
                lateralVelocity *= (1f / (1f + (vehicle.WheelFriction * DeltaTime)));

                bool movingInIntendedDirection = math.dot(fwdVelocity, vehicleForward * FwdInput) > 0f;
                if (!movingInIntendedDirection)
                {
                    fwdVelocity *= (1f / (1f + (vehicle.WheelRollResistance * DeltaTime)));
                }

                physicsVelocity.Linear = upVelocity + fwdVelocity + lateralVelocity;

                // Rotation
                physicsVelocity.Angular.y += vehicle.RotationAcceleration * SideInput * DeltaTime;
                physicsVelocity.Angular.y = math.clamp(physicsVelocity.Angular.y, -vehicle.MaxRotationSpeed, vehicle.MaxRotationSpeed);
                physicsVelocity.Angular.y *= (1f / (1f + (vehicle.RotationDamping * DeltaTime)));
            }
        }
    }
}