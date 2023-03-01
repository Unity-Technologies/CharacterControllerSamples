
# Platformer Sample - Swimming

The `SwimmingState` allows the character to accelerate and rotate freely towards any direction when it is in water. The state has a `SwimmingState.DetectWaterZones` function to detect when it is in the water.

The state update is split into two main phases: `PreMovementUpdate` and `PostMovementUpdate`. `PreMovementUpdate` uses `SwimmingState.DetectWaterZones` to try to see if it is still in a water zone, and to calculate some details about the direction and distance to the water surface. If it is close to the water surface, the character will rotate upright and prevent moving downward unless the movement input is very perpenticular to the water surface. This helps create a good "swimming at the surface of the water" mechanic. Character rotation control when underwater will simply rotate the character around its capsule geometry center towards the movement direction. After the character movement update is done, `PostMovementUpdate` will call `SwimmingState.DetectWaterZones` one more time. This is because it needs to know if the movement we just did brought us out of the water, and if so, we must snap back to the water surface.

This state is transitioned to when any other state detects that we are in a water zone, by calling `SwimmingState.DetectWaterZones`.