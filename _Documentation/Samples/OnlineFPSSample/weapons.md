
# OnlineFPS Sample - Weapons

## Assigning a weapon to a character

`ServerGameSystem` handles spawning a weapon for each character that it spawns, and assigning it as the character's `ActiveWeapon`. This is done in `ServerGameSystem.HandleSpawnCharacter`.

Then, `ActiveWeaponSystem` iterates on all `ActiveWeapon` components (on all characters), and handles doing the weapon setup whenever a change in active weapon is detected. Here we handle parenting the weapon to the FPS character view transform, setting the weapon's shot raycast point to our character view entity, etc... This is a trick most FPS games use to make sure the weapon shot raycasts start from the center of the camera and land exactly in the middle of the screen.


## Weapon shooting

1. The `FirstPersonPlayerFixedStepControlSystem` writes fire press/release inputs to the `WeaponControl` component on the character's active weapon
1. The `WeaponFiringMecanismSystem` reads firing inputs from the `WeaponControl` component, and uses them to determine how many shots should be fired this frame based on the weapon's firing rate and other parameters.
1. The `StandardRaycastWeaponPredictionSystem` reads the amount of shots to fire determined in the previous step, and for each shot, it`
    * Handles shot raycast & damage (only on the server)
    * Handles shot VFX & recoil requests (only on clients, and on the first time the tick is simulated)


## Weapon shot VFX

Networked weapon shot VFX requires some special handling, because we have some weapons that have a very high firing rate, and we'd like to avoid the bandwidth cost of sending data over network for each projectile that gets shot (whether it's projectile ghosts or RPCs).

Our solution is that every time the server detects a shot to fire on the owner-predicted weapons, it increments a `StandardRaycastWeapon.RemoteShotsCount`, which is a ghost field that gets synchronized on all clients. Then, on all clients, a `StandardRaycastWeaponRemoteShotsJob` job in `StandardRaycastWeaponVisualsSystem` creates shot VFX requests for each new shots in `StandardRaycastWeapon.RemoteShotsCount` since the last time the job was run. Finally, a `StandardRaycastWeaponShotVisualsJob` job in `StandardRaycastWeaponVisualsSystem` processes these shot VFX requests, and spawns the VFX prefabs for each one.

Each projectile type has its own system handling the specific VFX logic, such as:
* `LazerShotVisualsSystem`
* `BulletShotVisualsSystem`


## Weapon animation

`CharacterWeaponVisualFeedbackSystem` is responsible for animating the weapon socket entity and camera FoV based on character velocity and weapon shots. This includes weapon bobbing, weapon recoil, weapon aiming, FoV kick, etc...