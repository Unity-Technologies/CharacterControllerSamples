
# Platformer Sample - Ground Move

The `GroundMoveState` handles regular grounded movement, jumping, sprinting, detecting "sticky surfaces" (walk on walls), and detecting "friction surfaces" (like ice). It also deals with jumping "grace times": allowing a jump input to be pressed slightly before landing on the ground, but still allowing the jump to happen. This is controlled by the `JumpBeforeGroundedGraceTime`.

Typically, this state is transitioned to when the character is grounded and not doing any other special action such as crouching.

This state handles adapting the character rotation to match any detected "friction surface".