using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct PlatformerPlayer : IComponentData
{
    public Entity ControlledCharacter;
    public Entity ControlledCamera;
}

[Serializable]
public struct PlatformerPlayerInputs : IComponentData
{
    public float2 Move;
    public float2 Look;
    public float CameraZoom;
    
    public bool SprintHeld;
    public bool RollHeld;
    public bool JumpHeld;
    
    public FixedInputEvent JumpPressed;
    public FixedInputEvent DashPressed;
    public FixedInputEvent CrouchPressed;
    public FixedInputEvent RopePressed;
    public FixedInputEvent ClimbPressed;
    public FixedInputEvent FlyNoCollisionsPressed;
}
