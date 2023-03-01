

# OnlineFPS Sample - Character and Camera

The character in this sample started out with the first-person standard character, and was modified to add netcode compatibility. You can find a summary of the necessary changes in the [Networking](https://docs.unity3d.com/Packages/com.unity.charactercontroller@latest/index.html?subfolder=/manual/networking.html) article in the documentation.

The Character and Player ghost prefabs are located under: `Assets/Prefabs/Ghost`.

The `GhostVariants` class defines ghost variants for `KinematicCharacterBody`, `CharacterInterpolation`, and `LocalTransform` for character ghosts.


## Character & camera rotation synchronization

In this sample game, we use a special strategy for synchronizing character & camera rotation. Due to the fact that we have a character that only ever rotates around its Y axis, it would be a waste of bandwidth to synchronize the full rotation quaternion of the character. Instead, we can simply synchronize the Y euler angle of our rotation, and reconstruct our character rotation on clients/server based on that angle. Similarly, for camera rotation, we only synchronize a pitch angle instead of the full rotation quaternion. These ghost fields are defined in `FirstPersonCharacterComponent.CharacterYDegrees` and `FirstPersonCharacterComponent.ViewPitchDegrees`.

In our `FirstPersonCharacterAspect.VariableUpdate`, when we update our character rotation, we do so by working directly with `ViewPitchDegrees` and `CharacterYDegrees` (look for their usage in the parameters of the `FirstPersonCharacterUtilities.ComputeFinalRotationsFromRotationDelta` call). This function handles modifying those angles, and then computing final character & view rotations from them. 

However, since this rotation update is only handled for simulated characters on owning client & server, we also need a way for non-owning clients to synchronize character rotations as well. This is done in `BuildCharacterRotationSystem`. This system handles rebuilding the rotation of all characters based on the `CharacterYDegrees` at the beginning the prediction update. With this, all character rotations will be synchronized on all clients, and all character rotations will also be accurately reconstructed on all rollback & resimulation updates


## Interpolation

Character interpolation is a special case in netcode characters. Because netcode manages its own interpolation of interpolated ghosts, we need the built-in character interpolation to not interfere with it. We can do this with the `CharacterInterpolation_GhostVariant`, which specifies that the `CharacterInterpolation` component should only exist on predicted client ghosts.

