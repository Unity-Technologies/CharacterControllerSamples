
Here is how to setup the ghost components for third-person character prefabs:

Do the following for all 3 prefabs (`ThirdPersonCharacter`, `ThirdPersonPlayer`, `OrbitCamera`):
* Add a `Ghost Authoring Component` and a `Ghost Authoring Inspection Component`.
* Set the `Default Ghost Mode` of the `Ghost Authoring Component` to `Owner Predicted`.
* Set `Has Owner` on the `Ghost Authoring Component` to true.

Do the following for the `ThirdPersonCharacter` prefab:
* In the `Ghost Authoring Inspection Component`, use the `DontSerializeVariant` as the variant for `PhysicsVelocity`.

Do the following for the `ThirdPersonPlayer` prefab:
* In the `Ghost Authoring Component`, make sure `Support Auto Command Target` is true.
* In the `Ghost Authoring Inspection Component`, use the `DontSerializeVariant` as the variant for `LocalTransform`.

Do the following for the `OrbitCamera` prefab:
* In the `Ghost Authoring Inspection Component`, use the `DontSerializeVariant` as the variant for `LocalTransform`.