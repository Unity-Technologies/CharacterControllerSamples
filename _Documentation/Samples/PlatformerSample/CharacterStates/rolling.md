
# Platformer Sample - Rolling

The `RollingState` allows the character to turn into a rolling ball that can accelerate and has no notion of grounding. In `OnStateEnter`, the character's grounding evaluation is disabled, the character collider geometry is changed to a sphere, and the character mesh is swapped for a ball mesh. The opposite effect is done in `OnStateExit`. Velocity is handled simply by accelerating the velocity in the desired direction.

This state is transitioned to by pressing the roll input.