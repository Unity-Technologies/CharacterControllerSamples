
# Platformer Sample - Dashing

The `DashingState` allows the character to perform a quick dash in a given direction. It works by remembering a `_dashStartTime` and `_dashDirection` when it enters the state, and then it moves in that `_dashDirection` until its `DashDuration` has expired.

This state is transitioned to with the dash input.