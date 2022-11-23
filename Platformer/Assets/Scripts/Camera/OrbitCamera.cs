using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Serializable]
public struct OrbitCamera : IComponentData
{
    [Header("Rotation")]
    public float RotationSpeed;
    public float MaxVAngle;
    public float MinVAngle;
    public bool RotateWithCharacterParent;

    [Header("Zooming")]
    public float TargetDistance;
    public float MinDistance;
    public float MaxDistance;
    public float DistanceMovementSpeed;
    public float DistanceMovementSharpness;

    [Header("Obstructions")]
    public float ObstructionRadius;
    public float ObstructionInnerSmoothingSharpness;
    public float ObstructionOuterSmoothingSharpness;
    public bool PreventFixedUpdateJitter;
    
    [Header("Misc")]
    public float CameraTargetTransitionTime;

    // Data in calculations
    [HideInInspector]
    public float CurrentDistanceFromMovement;
    [HideInInspector]
    public float CurrentDistanceFromObstruction;
    [HideInInspector]
    public float PitchAngle;
    [HideInInspector]
    public float3 PlanarForward;
    
    [HideInInspector]
    public Entity ActiveCameraTarget;
    [HideInInspector]
    public Entity PreviousCameraTarget;
    [HideInInspector]
    public float CameraTargetTransitionStartTime;
    [HideInInspector]
    public RigidTransform CameraTargetTransform;
    [HideInInspector]
    public RigidTransform CameraTargetTransitionFromTransform;
    [HideInInspector]
    public bool PreviousCalculateUpFromGravity;

    public static OrbitCamera GetDefault()
    {
        OrbitCamera c = new OrbitCamera
        {
            RotationSpeed = 150f,
            MaxVAngle = 89f,
            MinVAngle = -89f,

            TargetDistance = 5f,
            MinDistance = 0f,
            MaxDistance = 10f,
            DistanceMovementSpeed = 50f,
            DistanceMovementSharpness = 20f,

            ObstructionRadius = 0.1f,
            ObstructionInnerSmoothingSharpness = float.MaxValue,
            ObstructionOuterSmoothingSharpness = 5f,
            PreventFixedUpdateJitter = true,
            
            CameraTargetTransitionTime = 0.4f,
            
            CurrentDistanceFromMovement = 5f,
            CurrentDistanceFromObstruction = 5f,
        };
        return c;
    }
}

[Serializable]
public struct OrbitCameraControl : IComponentData
{
    public Entity FollowedCharacterEntity;
    public float2 Look;
    public float Zoom;
}

[Serializable]
public struct OrbitCameraIgnoredEntityBufferElement : IBufferElementData
{
    public Entity Entity;
}