using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Rival;

[Serializable]
public struct StressTestCharacterComponent : IComponentData
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

    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

    public bool UseStatefulHits;
    public bool UseSaveRestoreState;

    public static StressTestCharacterComponent GetDefault()
    {
        return new StressTestCharacterComponent
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

            StepAndSlopeHandling = BasicStepAndSlopeHandlingParameters.GetDefault(),
        };
    }
}

[Serializable]
public struct StressTestCharacterControl : IComponentData
{
    public float3 MoveVector;
    public bool Jump;
}
