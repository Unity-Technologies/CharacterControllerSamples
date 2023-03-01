
# Platformer Sample - Climbing

The `ClimbingState` allows the character to climb on surfaces designated with the correct physics tag. Due to the highly-specialized collision detection required in this state, it turns off the character's collision detection in its `OnStateEnter`. Instead, this state will manage collision detection and velocity projection manually.

The `ClimbingState.ClimbingDetection` function handles detecting overlaps with climbable surfaces, and calculating the average normal of the climbing surface. It also adds every non-climbable hit to the character's `VelocityProjectionHits` buffer. This buffer is used during the state's movement update in order to project the character's velocity against all unclimbable obstructions while climbing, using `PlatformerCharacterAspect.ProjectVelocityOnHits`.

The state's movement update consists of detecting climbing hits, stiching close to the climbing surface, moving towards the input direction projected on the climbing surface normal, projecting velocity against unclimbable hits, and finally orienting self towards the average climbing normal.

It is very important for `ClimbingState.ClimbingDetection` to detect the hits with a perfect sphere shape (the character capsule collider is changed to a sphere shape in the `OnStateEnter`). The reason for that is because when the character is climbing, it must make sure that **adapting its rotation to the climbing normal will not result in changing the hits that are detected**. With a non-sphere shape, the character will keep jittering as it tries to orient itself towards the surface, because it keeps detect different hits as a result of its rotation.

This state is transitioned to when pressing the climb input while being close to a climbable surfaces. Other states call `ClimbingState.CanStartClimbing` in order to detect this.