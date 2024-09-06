using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PredictedFixedStepSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation)]
    [BurstCompile]
    public partial struct ActiveWeaponSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ActiveWeaponSetupJob setupJob = new ActiveWeaponSetupJob
            {
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                WeaponControlLookup = SystemAPI.GetComponentLookup<WeaponControl>(true),
                FirstPersonCharacterComponentLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterComponent>(true),
                WeaponSimulationShotOriginOverrideLookup =
                    SystemAPI.GetComponentLookup<WeaponShotSimulationOriginOverride>(false),
                LinkedEntityGroupLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(false),
                WeaponShotIgnoredEntityLookup = SystemAPI.GetBufferLookup<WeaponShotIgnoredEntity>(false),
            };
            state.Dependency = setupJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct ActiveWeaponSetupJob : IJobEntity
        {
            public EntityCommandBuffer ECB;
            [ReadOnly] public ComponentLookup<WeaponControl> WeaponControlLookup;
            [ReadOnly] public ComponentLookup<FirstPersonCharacterComponent> FirstPersonCharacterComponentLookup;
            public ComponentLookup<WeaponShotSimulationOriginOverride> WeaponSimulationShotOriginOverrideLookup;
            public BufferLookup<LinkedEntityGroup> LinkedEntityGroupLookup;
            public BufferLookup<WeaponShotIgnoredEntity> WeaponShotIgnoredEntityLookup;

            void Execute(Entity entity, ref ActiveWeapon activeWeapon)
            {
                // Detect changes in active weapon
                if (activeWeapon.Entity != activeWeapon.PreviousEntity)
                {
                    // Setup new weapon
                    if (WeaponControlLookup.HasComponent(activeWeapon.Entity))
                    {
                        // Setup for characters
                        if (FirstPersonCharacterComponentLookup.TryGetComponent(entity,
                                out FirstPersonCharacterComponent character))
                        {
                            // Make character View entity be the weapon's raycast start point
                            if (WeaponSimulationShotOriginOverrideLookup.TryGetComponent(activeWeapon.Entity,
                                    out WeaponShotSimulationOriginOverride shotOriginOverride))
                            {
                                shotOriginOverride.Entity = character.ViewEntity;
                                WeaponSimulationShotOriginOverrideLookup[activeWeapon.Entity] = shotOriginOverride;
                            }

                            // Make weapon be child of character's weapon socket 
                            ECB.AddComponent(activeWeapon.Entity,
                                new Parent { Value = character.WeaponAnimationSocketEntity });

                            // Remember weapon owner
                            ECB.SetComponent(activeWeapon.Entity, new WeaponOwner { Entity = entity });

                            // Make weapon linked to the character
                            DynamicBuffer<LinkedEntityGroup> linkedEntityBuffer = LinkedEntityGroupLookup[entity];
                            linkedEntityBuffer.Add(new LinkedEntityGroup { Value = activeWeapon.Entity });

                            // Add character as ignored shot entities
                            if (WeaponShotIgnoredEntityLookup.TryGetBuffer(activeWeapon.Entity,
                                    out DynamicBuffer<WeaponShotIgnoredEntity> ignoredEntities))
                            {
                                ignoredEntities.Add(new WeaponShotIgnoredEntity { Entity = entity });
                            }
                        }
                    }

                    // TODO: Un-setup previous weapon
                    // if (WeaponControlLookup.HasComponent(activeWeapon.PreviousEntity))
                    // {
                    // Disable weapon update, reset owner, reset data, unparent, etc...
                    // }
                }

                activeWeapon.PreviousEntity = activeWeapon.Entity;
            }
        }
    }
}