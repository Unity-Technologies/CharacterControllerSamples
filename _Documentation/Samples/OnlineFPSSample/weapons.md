
# OnlineFPS Sample - Weapons

## Assigning a weapon to a character

`ServerGameSystem` handles spawning a weapon for each character that it spawns, and assigning it as the character's `ActiveWeapon`. This is done in `ServerGameSystem.HandleSpawnCharacter`.

Then, `ActiveWeaponSystem` iterates on all `ActiveWeapon` components (on all characters), and handles doing the weapon setup whenever a change in active weapon is detected. Here we handle parenting the weapon to the FPS character view transform, setting the weapon's shot raycast point to our character view entity, etc... This is a trick most FPS games use to make sure the weapon shot raycasts start from the center of the camera and land exactly in the middle of the screen.


## Weapon shooting

1. The `FirstPersonPlayerVariableStepControlSystem` writes fire press/release inputs to the `WeaponControl` component on the character's active weapon
1. The `WeaponFiringMecanismSystem` reads firing inputs from the `WeaponControl` component, and uses them to determine how many shots should be fired this frame based on the weapon's firing rate and other parameters.
1. The `StandardRaycastWeaponSimulationSystem` and `StandardProjectileWeaponSystem` read the amount of shots to fire determined in the previous step, and for each shot, they either handle the shot raycast or spawn the projectile prefab (depends on the type of weapon)


## Weapon shot VFX

Networked weapon shot VFX for raycast weapons requires some special handling, since the shot is not a ghost. `StandardRaycastWeaponShotVisualsSystem` is responsible for handling this. When creating a weapon prefab, we can choose between two different ways of networking our shot VFX: `Precise` and `Efficient`.

The `Precise` method will add `StandardRaycastWeaponShotVFXRequest` events to a ghosted dynamic buffer on the weapon prefab. Clients will then read events from that buffer, and spawn shot VFX accordingly. Clients must make sure to skip processing events that they've already processed. This approach is somewhat costly for bandwidth, but it allows clients to display the shots exactly as they happened. This approach is used for the RailGun, since a precise visual representation of the shot is important for this weapon.

The `Efficient` method will simply increment a ghosted `ProjectileSpawnCount` field in the weapon component whenever a projectile is fired. Clients can then spawn a shot VFX for each shot count they haven't yet processed (they keep track of the last count they've processed and compare it to the latest count). This is much more bandwidth efficient than the other approach, but will not necessarily represent the exact bullet trajectory that actually happened on the server. We use this approach for the MachineGun and the Shotgun, since they both spawn lots of bullets and the exactitude of the visual representation of bullets isn't critical.

Finally, each projectile type has its own system handling the specific VFX logic, such as:
* `LazerShotVisualsSystem`
* `BulletShotVisualsSystem`


## Weapon animation

`WeaponFiringMecanismVisualsSystem` is responsible for informing the `CharacterWeaponVisualFeedback` component on the character that the active weapon was fired.

`CharacterWeaponVisualFeedbackSystem` is responsible for animating the weapon socket entity and camera FoV based on character velocity and weapon shot information stored in `CharacterWeaponVisualFeedback`. This includes weapon bobbing, weapon recoil, weapon aiming, FoV kick, etc...