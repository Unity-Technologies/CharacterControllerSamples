
# Platformer Sample - Ledge Grab

The `LedgeGrabState` allows the character to "snap" to a ledge and move along that ledge. Due to the highly-specialized collision detection required in this state, it turns off the character's collision detection in its `OnStateEnter`. Instead, this state will manage collision detection manually.

`LedgeGrabState.LedgeDetection` is what handles detecting valid ledges & information related to them. It casts the character collider forward to detect the ledge wall, and then raycasts downward at the ledge detection point to try to find a valid surface at the top of the ledge. Once it has found a valid surface, it will do a distance check with the character collider at the top of the ledge to see if our character would have space to get up there. Finally, it evaluates if we would be grounded on that surface hit.

The character's movement update in this state first calls `LedgeGrabState.LedgeDetection` to get up-to-date information about the current ledge. Then, if we have still found a valid ledge, we adjust the character position & rotation to make it stick to the ledge wall. Then, we calculate a movement that is parallel to the ledge wall, and determine the final position where this movement would take us. We do a `LedgeGrabState.LedgeDetection` at that final calculated position to see if there would still be a valid ledge over there. If not, we cancel the movement.

This state is transitioned to when other states (such as `AirMoveState`) call `LedgeGrabState.CanGrabLedge`, and a ledge is successfully detected.