
You'll need to make the `FirstPersonPlayer` a ghost component and synchronize the `ControlledCharacter` field. This will ensure that the entity references set up by the server when instantiating a character will be carried over to clients:
```cs
[GhostComponent]
public struct FirstPersonPlayer : IComponentData
{
    [GhostField]
    public Entity ControlledCharacter;
}
```

Then, make `FirstPersonCharacterComponent` a ghost component and synchronize the `ViewPitchDegrees` field:
```cs
[GhostComponent]
public struct FirstPersonCharacterComponent : IComponentData
{
    // ...

    [GhostField]
    public float ViewPitchDegrees;
}
```