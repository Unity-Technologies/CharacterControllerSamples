
# Platformer Sample - Wall Run

The `WallRunState` first detects if it is still moving against an ungrounded surface (a wall). If not, it transitions out to another state. But if it is, it will restrict move the move input vector so that it is tangent to the wall, and it will move the character in that direction. It also handles jumping against the wall (the jump direction is calculated based on the wall normal).

Reducing the `PlatformerCharacterComponent.WallRunGravityFactor` will increase the efficiency of wall-running. This basically reduces the effect of gravity while in that state, and therefore makes you stay in air longer than if you were just free-falling.

This state is transitioned to when in `AirMoveState`, and the sprint input is true, and `HasDetectedMoveAgainstWall` is true.