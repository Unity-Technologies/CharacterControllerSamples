using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Rival;
using Unity.Physics.Authoring;

[Serializable]
public struct BasicCharacterComponent : IComponentData
{
    [Header("Movement")]
    public float RotationSharpness;
    public float GroundMaxSpeed;
    public float GroundedMovementSharpness;
    public float AirAcceleration;
    public float AirMaxSpeed;
    public float AirDrag;
    public float JumpSpeed;
    public float3 Gravity;
    public bool PreventAirAccelerationAgainstUngroundedHits;
    public int MaxJumpsInAir;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;
    
    [Header("Tags")]
    public CustomPhysicsBodyTags IgnoreCollisionsTag;
    public CustomPhysicsBodyTags IgnoreGroundingTag;
    public CustomPhysicsBodyTags ZeroMassAgainstCharacterTag;
    public CustomPhysicsBodyTags InfiniteMassAgainstCharacterTag;
    public CustomPhysicsBodyTags IgnoreStepHandlingTag;
    
    [NonSerialized]
    public int CurrentJumpsInAir;

    public static BasicCharacterComponent GetDefault()
    {
        return new BasicCharacterComponent
        {
            RotationSharpness = 25f,
            GroundMaxSpeed = 10f,
            GroundedMovementSharpness = 15f,
            AirAcceleration = 50f,
            AirMaxSpeed = 10f,
            AirDrag = 0f,
            JumpSpeed = 10f,
            Gravity = math.up() * -30f,
            PreventAirAccelerationAgainstUngroundedHits = true,
            MaxJumpsInAir = 0,

            StepAndSlopeHandling = BasicStepAndSlopeHandlingParameters.GetDefault(),
        };
    }
}

[Serializable]
public struct BasicCharacterControl : IComponentData
{
    public float3 MoveVector;
    public bool Jump;
}
