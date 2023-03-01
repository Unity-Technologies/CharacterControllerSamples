
# Tutorial - Sprint

We will now add a Sprint functionality to the character. When pressing the LeftShift key, we want to apply a multiplier to our character velocity.

First, we will modify the `ThirdPersonPlayerInputs` component to add a field for sprint input. This component is meant to hold the raw input associated with a human player

```cs
[Serializable]
public struct ThirdPersonPlayerInputs : IComponentData
{
    // (...)
    public bool SprintHeld;
}
```

Next, we will modify the `ThirdPersonPlayerInputsSystem` so that it queries that input from the input system, and stores it in the component

```cs
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class ThirdPersonPlayerInputsSystem : SystemBase
{
    // (...)
    
    protected override void OnUpdate()
    {
        foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<ThirdPersonPlayerInputs>, ThirdPersonPlayer>())
        {
            // (...)

            playerInputs.ValueRW.SprintHeld = Input.GetKey(KeyCode.LeftShift);
        }
    }
}
```

Next, we will modify the `ThirdPersonCharacterControl` component to add a field for whether the character should be sprinting or not

```cs
[Serializable]
public struct ThirdPersonCharacterControl : IComponentData
{
    // (...)
    public bool Sprint;
}
```

Next, we will modify `ThirdPersonPlayerFixedStepControlSystem` so that it takes the sprint input stored in `ThirdPersonPlayerInputs` on the player, and writes it to the `ThirdPersonCharacterControl` component on the character. Make sure this happens *before* we write back to the component using `SystemAPI.SetComponent`

```cs
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct ThirdPersonPlayerFixedStepControlSystem : ISystem
{
    // (...)

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<ThirdPersonPlayerInputs>, ThirdPersonPlayer>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<ThirdPersonCharacterControl>(player.ControlledCharacter))
            {
                // (...)

                // Sprint
                characterControl.Sprint = playerInputs.ValueRW.SprintHeld;

                SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
            }

            // (...)
        }
    }
}
```

To summarize what we've done at this point: we've stored raw inputs for sprinting in a component on the entity that represents the human player controlling the character (the "Player" prefab), and we've modified its input systems in order to pass on that sprint input to a component that tells the character what to do (the `ThirdPersonCharacterControl` component on the "Character" prefab).

We've taken care of the sprint inputs, but now we must take care of the sprint implementation.

We will add a field that represents the multiplier to apply to our velocity when we're sprinting. This field will be in `ThirdPersonCharacterComponent` (this component is meant to represent the data that's specific to our character implementation)

```cs
[Serializable]
public struct ThirdPersonCharacterComponent : IComponentData
{
    // (...)
    public float SprintSpeedMultiplier; 
}
```

Then, we will use that `ThirdPersonCharacterComponent.SprintSpeedMultiplier` field to modify our character velocity in the character update, but only when `ThirdPersonCharacterControl.Sprint` is true, and only when the character is grounded. This will be done in `ThirdPersonCharacterAspect.HandleVelocityControl`

```cs
public readonly partial struct ThirdPersonCharacterAspect : IAspect, IKinematicCharacterProcessor<ThirdPersonCharacterUpdateContext>
{
    // (...)

    private void HandleVelocityControl(ref ThirdPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        // (...)

        if (characterBody.IsGrounded)
        {
            // Move on ground
            float3 targetVelocity = characterControl.MoveVector * characterComponent.GroundMaxSpeed;
            
            // Sprint
            if (characterControl.Sprint)
            {
                targetVelocity *= characterComponent.SprintSpeedMultiplier;
            }
            
            CharacterControlUtilities.StandardGroundMove_Interpolated(ref characterBody.RelativeVelocity, targetVelocity, characterComponent.GroundedMovementSharpness, deltaTime, characterBody.GroundingUp, characterBody.GroundHit.Normal);
            
            // (...)
        }
        else
        {
            // (...)
        }
    }
}
```

At this point, we can assign a value to the `SprintSpeedMultiplier` on the character prefab (a value of `2` will do, which means the spring speed will be 2x the regular speed). You can do this in the `ThirdPersonCharacterAuthoring` component on your prefab, under the `Character` section.

Now if you press play, and if you hold the LeftShift key, your character should be moving faster.
