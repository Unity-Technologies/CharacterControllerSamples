
# OnlineFPS Sample - Weapons

## Assigning a weapon to a character

`ServerGameSystem` handles spawning a weapon for each character that it spawns, and assigning it as the character's `ActiveWeapon`. This is done in `ServerGameSystem.HandleCharacters`.

Then, `ActiveWeaponSystem` iterates on all `ActiveWeapon` components (on all characters), and handles doing the weapon setup whenever a change in active weapon is detected. Here we handle parenting the weapon to the FPS character view transform, setting the weapon's shot raycast point to our character view entity, etc... This is a trick most FPS games use to make sure the weapon shot raycasts start from the center of the camera and land exactly in the middle of the screen.


## Base weapon logic

Every weapon in the game shares these common steps in order to fire:
1. The `FirstPersonPlayerVariableStepControlSystem` writes fire press/release inputs to the `WeaponControl` component on the character's active weapon.
1. The `WeaponsSimulationSystem` schedules a `BaseWeaponSimulationJob` that:
    1. Reads inputs from the `WeaponControl` component
    1. Calculates how many shots should be fired on this frame. Depending on the firing rate, there could be 0, 1, or multiple shot events in the same frame (a weapon shooting 120 projectiles per second might have to shoot on average 2 shots per frame at 60 fps). 
    1. For each shot to fire, determine how many projectiles to fire. A weapons can define multiple projectiles per shots, such as for shotguns that must shoot multiple bullets in a single shot
    1. For each projectile, add a new element to the `DynamicBuffer<WeaponProjectileEvent>` buffer on the weapon entity. 
    1. The total shots count and total projectiles count are both stored in the `BaseWeapon`. This will be used later for weapon visual feedback over network.

Weapon shot visual feedback that is synchronized over network, such as recoil, is handled by the `BaseWeaponShotVisualsSystem`. It relies on the `BaseWeapon.TotalShotsCount` ghost field and the `BaseWeapon.LastVisualTotalShotsCount` local-only field. By subtracting `LastVisualTotalShotsCount` to `TotalShotsCount`, we can determine how many shot events have happened since the last time we processed shot events. This allows us to handle visual recoil animations over network at very little bandwidth cost.

`CharacterWeaponVisualFeedbackSystem` is responsible for animating the weapon socket entity and camera FoV based on character velocity and weapon shot information stored in `CharacterWeaponVisualFeedback`. This includes weapon bobbing, weapon recoil, weapon aiming, FoV kick, etc...


## Raycast projectile weapons

Weapons such as the MachineGun, RailGun, and Shotgun all use an instantaneous raycast to detect hits for their projectiles. The prefabs that are spawned for projectiles are purely visual.

The `WeaponsSimulationSystem` schedules a `RaycastWeaponSimulationJob` that iterates all weapon entities that have the `RaycastWeapon` and `DynamicBuffer<RaycastWeaponVisualProjectileEvent>` components. For each projectile to shoot in the `DynamicBuffer<WeaponProjectileEvent>` buffer (filled by the `BaseWeaponSimulationJob`), this job will:
1. Do a raycast to determine what the projectile would hit
1. Add an element to the `DynamicBuffer<RaycastWeaponVisualProjectileEvent>` buffer, representing an event to spawn the visuals of a projectile (more on this later)
1. Apply damage to the hit entity, if possible 

The weapon system offers two different ways to sync raycast projectile visuals over network. Both of these are handled in the `RaycastWeaponProjectileVisualsJob`:
1. The `Precise` mode maintains a buffer of `RaycastWeaponVisualProjectileEvent` on the weapon entity, holding data about where exactly each projectile started and ended, and at what tick did it happen. This buffer is synchronized over network. Using this buffer and the tick of each projectile, remote clients are able to recreate the projectile shot visual events exactly as they happened. We always skip processing the events that belong to a tick we've already processed. The events are cleared by the server past a certain tick age.
1. The `BandwidthEfficient` mode sacrifices precision for efficiency. This approach skips the `RaycastWeaponVisualProjectileEvent` buffer entirely, and instead relies mostly on the `BaseWeapon.TotalProjectilesCount` ghost field and the `BaseWeapon.LastVisualTotalProjectilesCount` local-only field. By knowing how many projectiles a weapon should've shot so far, and how many projectile visuals we've actually spawned so far, we can always spawn as many projectiles as the difference between these two values, using the latest information available to us. The random spread of a projectile always uses a random value that is seeded from the current projectile index, and so it can be deterministically reconstructed. We do lose precision however, because we rely on the latest interpolated transform of the weapon in order to determine where the shot should start from. But with this approach, whether the weapon has to shoot 1 or 10000 projectiles, it'll always just have the bandwidth cost of one `uint TotalProjectilesCount`.


## Prefab projectile weapons

Weapons such as the RocketLauncher and PlasmaGun spawn projectiles as ghost prefabs, and these prefabs are in charge of moving and performing collision checks.

The `WeaponsSimulationSystem` schedules a `PrefabWeaponSimulationJob` that iterates all weapon entities that have the `PrefabWeapon`. For each projectile to shoot in the `DynamicBuffer<WeaponProjectileEvent>` buffer (filled by the `BaseWeaponSimulationJob`), this job will spawn a projectile prefab both on the server and on predicted clients, as a predicted prefab. `ProjectileClassificationSystem` is responsible for resolving predicted-spawned projectile ghosts on clients with the real server ghosts once they become available.

The `ProjectileSimulationsJob` is the base job that handles projectile movement and collision detection, but more specific jobs such as `ProjectileBulletSimulationJob` and `RocketSimulationJob` handle the logic of plasma bullet and rockets more specifically, after the `ProjectileSimulationsJob` has done most of the work.