using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.CharacterController;
using Unity.Transforms;
using UnityEngine;

[AlwaysSynchronizeSystem]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
public partial class PlatformerCharacterHybridSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(World.Unmanaged); 
        
        // Create
        foreach (var (characterAnimation, hybridData, entity) in SystemAPI.Query<RefRW<PlatformerCharacterAnimation>, PlatformerCharacterHybridData>()
                     .WithNone<PlatformerCharacterHybridLink>()
                     .WithEntityAccess())
        {
            GameObject tmpObject = GameObject.Instantiate(hybridData.MeshPrefab);
            Animator animator = tmpObject.GetComponent<Animator>();

            ecb.AddComponent(entity, new PlatformerCharacterHybridLink
            {
                Object = tmpObject,
                Animator = animator,
            });

            // Find the clipIndex param
            for (int i = 0; i < animator.parameters.Length; i++)
            {
                if (animator.parameters[i].name == "ClipIndex")
                {
                    characterAnimation.ValueRW.ClipIndexParameterHash = animator.parameters[i].nameHash;
                    break;
                }
            }
        }
        
        // Update
        foreach (var (characterAnimation, characterBody, characterTransform, characterComponent, characterStateMachine, characterControl, hybridLink, entity) in SystemAPI.Query<
            RefRW<PlatformerCharacterAnimation>, 
            KinematicCharacterBody,
            LocalTransform,
            PlatformerCharacterComponent,
            PlatformerCharacterStateMachine,
            PlatformerCharacterControl,
            PlatformerCharacterHybridLink>()
            .WithEntityAccess())
        {
            if (hybridLink.Object)
            {
                // Transform
                LocalToWorld meshRootLTW = SystemAPI.GetComponent<LocalToWorld>(characterComponent.MeshRootEntity);
                hybridLink.Object.transform.position = meshRootLTW.Position;
                hybridLink.Object.transform.rotation = meshRootLTW.Rotation;

                // Animation
                if (hybridLink.Animator)
                {
                    PlatformerCharacterAnimationHandler.UpdateAnimation(
                        hybridLink.Animator,
                        ref characterAnimation.ValueRW,
                        in characterBody,
                        in characterComponent,
                        in characterStateMachine,
                        in characterControl,
                        in characterTransform);
                }

                // Mesh enabling
                if (characterStateMachine.CurrentState == CharacterState.Rolling)
                {
                    if (hybridLink.Object.activeSelf)
                    {
                        hybridLink.Object.SetActive(false);
                    }
                }
                else
                {
                    if (!hybridLink.Object.activeSelf)
                    {
                        hybridLink.Object.SetActive(true);
                    }
                }
            }
        }
        
        // Destroy
        foreach (var (hybridLink, entity) in SystemAPI.Query<PlatformerCharacterHybridLink>()
                     .WithNone<PlatformerCharacterHybridData>()
                     .WithEntityAccess())
        {
            GameObject.Destroy(hybridLink.Object);
            ecb.RemoveComponent<PlatformerCharacterHybridLink>(entity);
        }
    }
}