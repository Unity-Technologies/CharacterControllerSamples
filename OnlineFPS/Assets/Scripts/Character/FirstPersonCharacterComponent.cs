using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.CharacterController;
using Unity.NetCode;

[Serializable]
[GhostComponent()]
public struct FirstPersonCharacterComponent : IComponentData
{
    public float BaseFoV;
    public float GroundMaxSpeed;
    public float GroundedMovementSharpness;
    public float AirAcceleration;
    public float AirMaxSpeed;
    public float AirDrag;
    public float JumpSpeed;
    public float3 Gravity;
    public bool PreventAirAccelerationAgainstUngroundedHits;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

    public Entity NameTagSocketEntity;
    public Entity WeaponSocketEntity;
    public Entity WeaponAnimationSocketEntity;
    public Entity ViewEntity;
    public Entity DeathVFX;
    public Entity DeathVFXSpawnPoint;
    public float MinViewAngle;
    public float MaxViewAngle;
    public float ViewRollAmount;
    public float ViewRollSharpness;
    
    [HideInInspector]
    [GhostField(Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
    public float CharacterYDegrees;
    [HideInInspector]
    [GhostField(Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
    public float ViewPitchDegrees;
    [HideInInspector]
    public quaternion ViewLocalRotation;
    [HideInInspector]
    public float ViewRollDegrees;

    public static FirstPersonCharacterComponent GetDefault()
    {
        return new FirstPersonCharacterComponent
        {
            BaseFoV = 75f,
            GroundMaxSpeed = 10f,
            GroundedMovementSharpness = 15f,
            AirAcceleration = 50f,
            AirMaxSpeed = 10f,
            AirDrag = 0f,
            JumpSpeed = 10f,
            Gravity = math.up() * -30f,
            PreventAirAccelerationAgainstUngroundedHits = true,

            StepAndSlopeHandling = BasicStepAndSlopeHandlingParameters.GetDefault(),

            MinViewAngle = -90f,
            MaxViewAngle = 90f,
        };
    }
}

[Serializable]
public struct FirstPersonCharacterControl : IComponentData
{
    public float3 MoveVector;
    public float2 LookYawPitchDegrees;
    public bool Jump;
}

[Serializable]
public struct FirstPersonCharacterView : IComponentData
{
    public Entity CharacterEntity;
}

[Serializable]
public struct CharacterClientCleanup : ICleanupComponentData
{
    public Entity DeathVFX;
    public float3 DeathVFXSpawnWorldPosition;
}

[Serializable]
[GhostComponent()]
public struct OwningPlayer : IComponentData
{
    [GhostField()]
    public Entity Entity;
}