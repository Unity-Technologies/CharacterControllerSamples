using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct TestMovingPlatform : IComponentData
{
    [Serializable]
    public struct AuthoringData
    {
        public float3 TranslationAxis;
        public float TranslationAmplitude;
        public float TranslationSpeed;
        public float3 RotationAxis;
        public float RotationSpeed;
        public float3 OscillationAxis;
        public float OscillationAmplitude;
        public float OscillationSpeed;
    }

    public AuthoringData Data;
    public float3 OriginalPosition;
    public quaternion OriginalRotation;
}