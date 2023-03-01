
# Platformer Sample - Crouched

The `CrouchedState` handles regular moving while crouched. It is in many ways similar to the `GroundMoveState`, but it handles resizing the character collider in its `OnStateEnter` and `OnStateExit`.

This state is transitioned to when grounded and when the the crouch input is pressed.

This state handles adapting the character rotation to match any detected "friction surface".