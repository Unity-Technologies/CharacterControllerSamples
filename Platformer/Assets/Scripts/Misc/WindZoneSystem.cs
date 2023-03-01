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
[UpdateAfter(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct WindZoneSystem : ISystem
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
        WindZoneJob job = new WindZoneJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(false),
            CharacterStateMachineLookup = SystemAPI.GetComponentLookup<PlatformerCharacterStateMachine>(true),
            PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
            PhysicsMassLookup = SystemAPI.GetComponentLookup<PhysicsMass>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct WindZoneJob : IJobEntity
    {
        public float DeltaTime;
        public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
        [ReadOnly]
        public ComponentLookup<PlatformerCharacterStateMachine> CharacterStateMachineLookup;
        public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
        [ReadOnly]
        public ComponentLookup<PhysicsMass> PhysicsMassLookup;
        
        void Execute(Entity entity, in WindZone windZone, in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
        {
            for (int i = 0; i < triggerEventsBuffer.Length; i++)
            {
                StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                Entity otherEntity = triggerEvent.GetOtherEntity(entity);
    
                if (triggerEvent.State == StatefulEventState.Stay)
                {
                    // Characters
                    if (CharacterBodyLookup.TryGetComponent(otherEntity, out KinematicCharacterBody characterBody) && 
                        CharacterStateMachineLookup.TryGetComponent(otherEntity, out PlatformerCharacterStateMachine characterStateMachine))
                    {
                        if (PlatformerCharacterAspect.CanBeAffectedByWindZone(characterStateMachine.CurrentState))
                        {
                            characterBody.RelativeVelocity += windZone.WindForce * DeltaTime;
                            CharacterBodyLookup[otherEntity] = characterBody;
                        }
                    }
                    // Dynamic physics bodies
                    if (PhysicsVelocityLookup.TryGetComponent(otherEntity, out PhysicsVelocity physicsVelocity) && 
                        PhysicsMassLookup.TryGetComponent(otherEntity, out PhysicsMass physicsMass))
                    {
                        if (physicsMass.InverseMass > 0f)
                        {
                            physicsVelocity.Linear += windZone.WindForce * DeltaTime;
                            PhysicsVelocityLookup[otherEntity] = physicsVelocity;
                        }
                    }
                }
            }
        }
    }
}