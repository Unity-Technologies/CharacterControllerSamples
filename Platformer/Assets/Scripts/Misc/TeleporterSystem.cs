using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.CharacterController;
using Unity.Physics.Stateful;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[BurstCompile]
public partial struct TeleporterSystem : ISystem
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
        TeleporterJob job = new TeleporterJob
        {
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
            CharacterInterpolationLookup = SystemAPI.GetComponentLookup<CharacterInterpolation>(false),
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct TeleporterJob : IJobEntity
    {
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
        public ComponentLookup<CharacterInterpolation> CharacterInterpolationLookup;

        void Execute(Entity entity, in Teleporter teleporter, in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
        {
            // Only teleport if there is a destination
            if (teleporter.DestinationEntity != Entity.Null)
            {
                for (int i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                    Entity otherEntity = triggerEvent.GetOtherEntity(entity);

                    // If a character has entered the trigger, move its translation to the destination
                    if (triggerEvent.State == StatefulEventState.Enter && CharacterBodyLookup.HasComponent(otherEntity))
                    {
                        LocalTransform t = LocalTransformLookup[otherEntity];
                        t.Position = LocalTransformLookup[teleporter.DestinationEntity].Position;
                        t.Rotation = LocalTransformLookup[teleporter.DestinationEntity].Rotation;
                        LocalTransformLookup[otherEntity] = t;

                        // Bypass interpolation
                        if (CharacterInterpolationLookup.HasComponent(otherEntity))
                        {
                            CharacterInterpolation interpolation = CharacterInterpolationLookup[otherEntity];
                            interpolation.SkipNextInterpolation();
                            CharacterInterpolationLookup[otherEntity] = interpolation;
                        }
                    }
                }
            }
        }
    }
}