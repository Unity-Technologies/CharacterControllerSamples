using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Physics.Extensions;
using Unity.Transforms;
using Unity.CharacterController;

[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
[BurstCompile]
public partial struct TestMovingPlatformSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        if (deltaTime <= 0f)
            return;

        TestMovingPlatformJob job = new TestMovingPlatformJob
        {
            Time = (float)SystemAPI.Time.ElapsedTime,
            InvDeltaTime = 1f / deltaTime,
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct TestMovingPlatformJob : IJobEntity
    {
        public float Time;
        public float InvDeltaTime;

        void Execute(Entity entity, ref PhysicsVelocity physicsVelocity, in PhysicsMass physicsMass, in LocalTransform localTransform, in TestMovingPlatform movingPlatform)
        {
            float3 targetPos = movingPlatform.OriginalPosition + (math.normalizesafe(movingPlatform.Data.TranslationAxis) * math.sin(Time * movingPlatform.Data.TranslationSpeed) * movingPlatform.Data.TranslationAmplitude);

            quaternion rotationFromRotation = quaternion.Euler(math.normalizesafe(movingPlatform.Data.RotationAxis) * movingPlatform.Data.RotationSpeed * Time);
            quaternion rotationFromOscillation = quaternion.Euler(math.normalizesafe(movingPlatform.Data.OscillationAxis) * (math.sin(Time * movingPlatform.Data.OscillationSpeed) * movingPlatform.Data.OscillationAmplitude));
            quaternion totalRotation = math.mul(rotationFromRotation, rotationFromOscillation);
            quaternion targetRot = math.mul(totalRotation, movingPlatform.OriginalRotation);

            RigidTransform targetTransform = new RigidTransform(targetRot, targetPos);

            physicsVelocity = PhysicsVelocity.CalculateVelocityToTarget(in physicsMass, localTransform.Position, localTransform.Rotation, in targetTransform, InvDeltaTime);
        }
    }
}