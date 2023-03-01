using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.CharacterController;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct JumpPadSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    { }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        JumpPadJob job = new JumpPadJob
        {
            KinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(false),
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct JumpPadJob : IJobEntity
    {
        public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup; 
        
        void Execute(Entity entity, in LocalTransform localTransform, in JumpPad jumpPad, in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
        {
            for (int i = 0; i < triggerEventsBuffer.Length; i++)
            {
                StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                Entity otherEntity = triggerEvent.GetOtherEntity(entity);

                // If a character has entered the trigger, add jumppad power to it
                if (triggerEvent.State == StatefulEventState.Enter && KinematicCharacterBodyLookup.TryGetComponent(otherEntity, out KinematicCharacterBody characterBody))
                {
                    float3 jumpVelocity = MathUtilities.GetForwardFromRotation(localTransform.Rotation) * jumpPad.JumpPower;
                    characterBody.RelativeVelocity = jumpVelocity;

                    // Unground the character
                    if (characterBody.IsGrounded && math.dot(math.normalizesafe(jumpVelocity), characterBody.GroundHit.Normal) > jumpPad.UngroundingDotThreshold)
                    {
                        characterBody.IsGrounded = false;
                    }

                    KinematicCharacterBodyLookup[otherEntity] = characterBody;
                }
            }
        }
    }
}