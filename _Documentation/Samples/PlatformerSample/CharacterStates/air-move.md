
# Platformer Sample - Air Move

The `AirMoveState` handles regular air movement, air jumping, and detecting ungrounded walls (required for wall-running transitions). It also deals with jumping "grace times": allowing a jump input to be pressed slightly after becoming ungrounded, but still allowing the jump to happen as though we were grounded. This is controlled by the `JumpAfterUngroundedGraceTime`.

Typically, this state is transitioned to when the character is not grounded and not doing any other special action such as wall running.