
# Platformer Sample - Flying

The `FlyingNoCollisionsState` allows the character to fly without any collisions. It handles turning off character collisions in its `OnStateEnter`, and turning them back on in its `OnStateExit`. The movement is handled by simply moving the character transform position directly.

This state is transitioned to by pressing the flying input.