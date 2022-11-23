
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial class MovingPlatformSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float invDeltaTime = 1f / deltaTime;
        float time = (float)World.Time.ElapsedTime;

        foreach(var (movingPlatform, physicsVelocity, physicsMass, localTransform, entity) in SystemAPI.Query<RefRW<MovingPlatform>, RefRW<PhysicsVelocity>, PhysicsMass, LocalTransform>().WithEntityAccess())
        {
            if(!movingPlatform.ValueRW.IsInitialized)
            {
                // Remember initial pos/rot, because our calculations depend on them
                movingPlatform.ValueRW.OriginalPosition = localTransform.Position;
                movingPlatform.ValueRW.OriginalRotation = localTransform.Rotation;
                movingPlatform.ValueRW.IsInitialized = true;
            }

            float3 targetPos = movingPlatform.ValueRW.OriginalPosition + (math.normalizesafe(movingPlatform.ValueRW.TranslationAxis) * math.sin(time * movingPlatform.ValueRW.TranslationSpeed) * movingPlatform.ValueRW.TranslationAmplitude);
            quaternion rotationFromMovement = quaternion.Euler(math.normalizesafe(movingPlatform.ValueRW.RotationAxis) * movingPlatform.ValueRW.RotationSpeed * time);
            quaternion targetRot = math.mul(rotationFromMovement, movingPlatform.ValueRW.OriginalRotation);

            // Move with velocity
            physicsVelocity.ValueRW = PhysicsVelocity.CalculateVelocityToTarget(in physicsMass, localTransform.Position, localTransform.Rotation, new RigidTransform(targetRot, targetPos), invDeltaTime);
        }
    }
}