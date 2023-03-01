
# Platformer Sample - Character

The character in this sample is fairly complex. Due to its level of complexity, we've decided to separate its movement logic into various "states".

The implementation of the character logic resembles the third-person standard character at its core, but `PlatformerCharacterAspect.PhysicsUpdate` and `PlatformerCharacterAspect.VariableUpdate` is where it starts to differ. `PlatformerCharacterAspect.PhysicsUpdate` starts by handling logic that is common to all states, then calls the current state's physics update via `stateMachine.OnStatePhysicsUpdate`, and finally does more logic common to all states after the state machine update. Similarly, `PlatformerCharacterAspect.VariableUpdate` calls the current state's variable update via `stateMachine.OnStateVariableUpdate`.

In short; instead of having all character update logic in the `PlatformerCharacterAspect` directly, we've implemented the logic of each state in their own struct. `PlatformerCharacterStateMachine` is responsible for calling the state update methods on the current state struct. For each state method, we pass the `PlatformerCharacterAspect` by reference, along with the character update contexts, so that state updates can have access to all the data they need.

Every character state checks for state transitions, typically at the end of their OnStatePhysicsUpdate. When they determine that they should transition, they call `stateMachine.TransitionToState`.


## States

Here are the various states of the character:

- [Ground Move](CharacterStates/ground-move.md)
- [Air Move](CharacterStates/air-move.md)
- [Crouched](CharacterStates/crouched.md)
- [Wall Run](CharacterStates/wall-run.md)
- [Flying](CharacterStates/flying.md)
- [Dashing](CharacterStates/dashing.md)
- [Rope Swing](CharacterStates/rope-swing.md)
- [Rolling](CharacterStates/rolling.md)
- [Swimming](CharacterStates/swimming.md)
- [Climbing](CharacterStates/climbing.md)
- [Ledge Grab](CharacterStates/ledge-grab.md)


## Misc Features

Here is a quick description of how several other features were implemented:

**Planet Gravity**: The gravity of all physics objects in this sample is handled by a `CustomGravity` component, a `GravityZonesSystem`, and special gravity zones such as `GlobalGravityZone` and `SphericalGravityZone`. The `GravityZonesSystem` will handle calculating a spherical gravity for all `CustomGravity` entities in its sphere trigger zone. Then, it will apply a global gravity to all `CustomGravity` entities that are not in any other kind of gravity zone. Finally, for all dynamic body entities that have a `CustomGravity` and a `PhysicsVelocity`, it will add that calculated gravity to the `PhysicsVelocity.Linear`. The character doesn't get its velocity modified even though it has a `CustomGravity`, because it is not a dynamic body. The character takes care of adding its custom gravity to its `KinematicCharacterBody.RelativeVelocity` in its state updates. And finally, the character orients its rotation so that it always points towards the opposite of the custom gravity's direction.

**Ice Surface**: A `CharacterFrictionModifier` component can be placed on rigidbodies to modify the character's simulated friction when it walks on that surface. In its state movement updates, the character tries to see if the ground hit entity has a `CharacterFrictionModifier` component, and if so, it'll change the way it controls its velocity based on the `CharacterFrictionModifier.Friction`

**Wind Zone**: A `WindZoneSystem` iterates on all entities that have a `WindZone` component and a trigger collider that raises trigger events. For each teleporter, if a trigger event with a `KinematicCharacterBody` was detected, we add a wind acceleration to the `KinematicCharacterBody.RelativeVelocity`

**Jump Pad**: A `JumpPadSystem` iterates on all entities that have a `JumpPad` component and a trigger collider that raises trigger events. For each teleporter, if a trigger event with a character was detected, we unground the character and add a velocity impulse to the `KinematicCharacterBody.RelativeVelocity`

**Teleporter**: A `TeleporterSystem` iterates on all entities that have a `Teleporter` component and a trigger collider that raises trigger events. For each teleporter, if a trigger event with a `KinematicCharacterBody` was detected, we move the character position to the teleportation destination point. Additionally, since we don't want character interpolation to be active for the teleportation, we call `SkipNextInterpolation()` on the `CharacterInterpolation` component of the character entity. This will skip the interpolation for the remainder of the frames until next fixed update.

**Sticky Surfaces (walk on walls)**: `PlatformerCharacterComponent.StickySurfaceTag` allows us to assign a physics tag that represents sticky surfaces that the character can walk on. In the character's state update, it will see if the rigidbody of the ground hit entity has that tag, and if so, it will orient its rotation so that it always points towards the ground normal. Moreover, since the character's `GroundingUp` is always set to be the character rotation's up direction, our grounding reference direction will always be relative to the character orientation, which means the character could consider itself grounded even if it was standing upside down on the ceiling.
