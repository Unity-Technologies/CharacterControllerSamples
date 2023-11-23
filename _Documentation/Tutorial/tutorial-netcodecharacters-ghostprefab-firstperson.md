
Here is how to setup the ghost components for first-person character prefabs:

Do the following for both prefabs (`FirstPersonCharacter`, `FirstPersonPlayer`):
* Add a `Ghost Authoring Component` and a `Ghost Authoring Inspection Component`.
* Set the `Default Ghost Mode` of the `Ghost Authoring Component` to `Owner Predicted`.
* Set `Has Owner` on the `Ghost Authoring Component` to true.

Do the following for the `FirstPersonCharacter` prefab:
* In the `Ghost Authoring Inspection Component`, use the `DontSerializeVariant` as the variant for `PhysicsVelocity`.

Do the following for the `FirstPersonPlayer` prefab:
* In the `Ghost Authoring Component`, make sure `Support Auto Command Target` is true.
* In the `Ghost Authoring Inspection Component`, use the `DontSerializeVariant` as the variant for `LocalTransform`.