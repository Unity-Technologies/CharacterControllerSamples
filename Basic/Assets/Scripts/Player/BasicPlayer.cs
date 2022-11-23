using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct BasicPlayer : IComponentData
{
    public Entity ControlledCharacter;
    public Entity ControlledCamera;
}

[Serializable]
public struct BasicPlayerInputs : IComponentData
{
    public float2 MoveInput;
    public float2 CameraLookInput;
    public float CameraZoomInput;
    public FixedInputEvent JumpPressed;
}
