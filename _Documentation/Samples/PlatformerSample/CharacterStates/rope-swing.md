
# Platformer Sample - Rope Swing

The `RopeSwingState` allows the character to simulate hanging on a rope attached to a point. It does this by handling movement in a very similar manner to typical air movement, but with the addition of calling `RopeSwingState.ConstrainToRope` at the end of the update. This function projects the character velocity to simulate a rope constraint.

This state is transitioned to when the rope input is pressed, and a valid rope attachment point is in range. Rope points are detected with `RopeSwingState.DetectRopePoints`.

The rope entity is spawned in the state's `OnStateEnter`. The `CharacterRopeSystem` handles positioning the rope entity from character to rope attachment point, and destroying the rope when its owning character is not in `RopeSwingState` anymore.