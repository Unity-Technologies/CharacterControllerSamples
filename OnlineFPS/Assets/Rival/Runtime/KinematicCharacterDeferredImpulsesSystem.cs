using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Rival
{
    /// <summary>
    /// Handles applying impulses that were detected during the character update
    /// </summary>
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct KinematicCharacterDeferredImpulsesSystem : ISystem
    {
        private EntityQuery _characterQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder().Build(ref state);
            state.RequireForUpdate(_characterQuery);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            KinematicCharacterDeferredImpulsesJob job = new KinematicCharacterDeferredImpulsesJob
            {
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
                CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(false),
                CharacterPropertiesLookup = SystemAPI.GetComponentLookup<KinematicCharacterProperties>(true),
            };
            job.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct KinematicCharacterDeferredImpulsesJob : IJobEntity
        {
            public ComponentLookup<LocalTransform> TransformLookup;
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
            [ReadOnly]
            public ComponentLookup<KinematicCharacterProperties> CharacterPropertiesLookup;

            void Execute(in DynamicBuffer<KinematicCharacterDeferredImpulse> characterDeferredImpulsesBuffer)
            {
                for (int deferredImpulseIndex = 0; deferredImpulseIndex < characterDeferredImpulsesBuffer.Length; deferredImpulseIndex++)
                {
                    KinematicCharacterDeferredImpulse deferredImpulse = characterDeferredImpulsesBuffer[deferredImpulseIndex];

                    // Impulse
                    bool isImpulseOnCharacter = CharacterPropertiesLookup.HasComponent(deferredImpulse.OnEntity);
                    if (isImpulseOnCharacter)
                    {
                        KinematicCharacterProperties hitCharacterProperties = CharacterPropertiesLookup[deferredImpulse.OnEntity];
                        if (hitCharacterProperties.SimulateDynamicBody)
                        {
                            KinematicCharacterBody hitCharacterBody = CharacterBodyLookup[deferredImpulse.OnEntity];
                            hitCharacterBody.RelativeVelocity += deferredImpulse.LinearVelocityChange;
                            CharacterBodyLookup[deferredImpulse.OnEntity] = hitCharacterBody;
                        }
                    }
                    else
                    {
                        PhysicsVelocity bodyPhysicsVelocity = PhysicsVelocityLookup[deferredImpulse.OnEntity];

                        bodyPhysicsVelocity.Linear += deferredImpulse.LinearVelocityChange;
                        bodyPhysicsVelocity.Angular += deferredImpulse.AngularVelocityChange;

                        PhysicsVelocityLookup[deferredImpulse.OnEntity] = bodyPhysicsVelocity;
                    }

                    // Displacement
                    if (math.lengthsq(deferredImpulse.Displacement) > 0f)
                    {
                        LocalTransform bodyTransform = TransformLookup[deferredImpulse.OnEntity];
                        bodyTransform.Position += deferredImpulse.Displacement;
                        TransformLookup[deferredImpulse.OnEntity] = bodyTransform;
                    }
                }
            }
        }
    }
}