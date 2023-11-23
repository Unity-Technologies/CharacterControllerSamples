
You'll need to make the `ThirdPersonPlayer` a ghost component and synchronize the `ControlledCharacter` and `ControlledCamera` fields. This will ensure that the entity references set up by the server when instantiating a character will be carried over to clients:
```cs
[GhostComponent]
public struct ThirdPersonPlayer : IComponentData
{
    [GhostField]
    public Entity ControlledCharacter;
    [GhostField]
    public Entity ControlledCamera;
}
```

Then, you'll need to make the `OrbitCamera` component a ghost component and synchronize the following fields:
```cs
[GhostComponent]
public struct OrbitCamera : IComponentData
{
    // (....)
    
    [GhostField]
    public float TargetDistance;
    // (....)
    [GhostField]
    public float PitchAngle;
    [GhostField]
    public float3 PlanarForward;
}
```