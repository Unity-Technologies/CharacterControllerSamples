
# Tutorial - Double Jump

We will now add the ability to allow up to X additional jumps while in air. 

We will start by adding an int field in `ThirdPersonCharacterComponent` to determine how many air jumps we are allowed to do, and we will also add another int field to keep track of how many air jumps we've done so far.

```cs
[Serializable]
public struct ThirdPersonCharacterComponent : IComponentData
{
    // (...)

    public int MaxAirJumps;

    [UnityEngine.HideInInspector] // we don't want this field to appear in the inspector
    public int CurrentAirJumps;
}
```

The rest of the implementation will be done in `ThirdPersonCharacterAspect.HandleVelocityControl`. If we are not grounded and a jump is requested, we will check if we have reached our max in-air jumps count, and if not, we will jump. Also, when we are grounded, we always reset our `CurrentAirJumps` to 0.

```cs
public readonly partial struct ThirdPersonCharacterAspect : IAspect, IKinematicCharacterProcessor<ThirdPersonCharacterUpdateContext>
{
    // (...)

    private void HandleVelocityControl(ref ThirdPersonCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
    {
        // (...)

        if (characterBody.IsGrounded)
        {
            // (...)

            // Reset air jumps when grounded
            characterComponent.CurrentAirJumps = 0;
        }
        else
        {
            // Move in air
            // (...)

            // Air Jumps
            if (characterControl.Jump && characterComponent.CurrentAirJumps < characterComponent.MaxAirJumps)
            {
                CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * characterComponent.JumpSpeed, true, characterBody.GroundingUp);
                characterComponent.CurrentAirJumps++;
            }

            // Gravity
            // (...)
        }
    }
}
```

Now if you set `MaxAirJumps` to 1 in your character prefab and press Play, you should be able to double jump. Additional air jumps can be performed with a value of more than 1.