using System;
using System.Windows.Input;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine.Serialization;

namespace OnlineFPS
{
    [Serializable]
    [GhostComponent()]
    public struct FirstPersonPlayer : IComponentData
    {
        [GhostField()] public FixedString128Bytes Name;
        [GhostField()] public Entity ControlledCharacter;

        public bool IsAutoMoving;
    }

    [Serializable]
    [GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct FirstPersonPlayerNetworkInput : IComponentData
    {
        [GhostField()] public float2 LastProcessedLookYawPitchDegrees;
    }

    [Serializable]
    public struct FirstPersonPlayerCommands : IInputComponentData
    {
        public float2 MoveInput;
        public float2 LookYawPitchDegrees;
        public InputEvent JumpPressed;
        public InputEvent ShootPressed;
        public InputEvent ShootReleased;
        public bool AimHeld;
    }
}