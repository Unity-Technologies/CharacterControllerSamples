using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.CharacterController;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct CharacterRopeSystem : ISystem
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
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
        
        foreach (var (characterRope, entity) in SystemAPI.Query<CharacterRope>().WithEntityAccess())
        {
            if (characterRope.OwningCharacterEntity == Entity.Null)
            {
                return;
            }

            if (SystemAPI.HasComponent<PlatformerCharacterComponent>(characterRope.OwningCharacterEntity) && SystemAPI.HasComponent<PlatformerCharacterStateMachine>(characterRope.OwningCharacterEntity))
            {
                PlatformerCharacterComponent platformerCharacter = SystemAPI.GetComponent<PlatformerCharacterComponent>(characterRope.OwningCharacterEntity);
                PlatformerCharacterStateMachine characterStateMachine = SystemAPI.GetComponent<PlatformerCharacterStateMachine>(characterRope.OwningCharacterEntity);
                LocalToWorld characterLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(characterRope.OwningCharacterEntity);

                // Handle rope positioning
                {
                    RigidTransform characterTransform = new RigidTransform(characterLocalToWorld.Rotation, characterLocalToWorld.Position);
                    float3 anchorPointOnCharacter = math.transform(characterTransform, platformerCharacter.LocalRopeAnchorPoint);
                    float3 ropeVector = characterStateMachine.RopeSwingState.AnchorPoint - anchorPointOnCharacter;
                    float ropeLength = math.length(ropeVector);
                    float3 ropeMidPoint = anchorPointOnCharacter + (ropeVector * 0.5f);

                    SystemAPI.SetComponent(entity, new LocalToWorld { Value = math.mul(new float4x4(MathUtilities.CreateRotationWithUpPriority(math.normalizesafe(ropeVector), math.forward()), ropeMidPoint), float4x4.Scale(new float3(0.04f, ropeLength * 0.5f, 0.04f))) });
                }

                // Destroy self when not in rope swing state anymore
                if (characterStateMachine.CurrentState != CharacterState.RopeSwing)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}