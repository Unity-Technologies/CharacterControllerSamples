
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.CharacterController;

// Update after events processing system 
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
public partial class JumpPadSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Iterate on all jump pads with trigger event buffers
        foreach (var (jumpPad, triggerEventsBuffer, entity) in SystemAPI.Query<JumpPad, DynamicBuffer<StatefulTriggerEvent>>().WithEntityAccess())
        {
            // Go through each trigger event of the jump pad...
            for (int i = 0; i < triggerEventsBuffer.Length; i++)
            {
                StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                Entity otherEntity = triggerEvent.GetOtherEntity(entity);

                // If a character has entered the trigger...
                if (triggerEvent.State == StatefulEventState.Enter && SystemAPI.HasComponent<KinematicCharacterBody>(otherEntity))
                {
                    KinematicCharacterBody characterBody = SystemAPI.GetComponent<KinematicCharacterBody>(otherEntity);

                    // Cancel out character velocity in the jump force's direction
                    // (this helps make the character jump up even if it is falling down on the jump pad at high speed)
                    characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, math.normalizesafe(jumpPad.JumpForce));

                    // Add the jump pad force to the character
                    characterBody.RelativeVelocity += jumpPad.JumpForce;

                    // Unground the character
                    // (without this, the character would snap right back to the ground on the next frame)
                    characterBody.IsGrounded = false;

                    // Don't forget to write back to the component
                    SystemAPI.SetComponent(otherEntity, characterBody);
                }
            }
        }
    }
}