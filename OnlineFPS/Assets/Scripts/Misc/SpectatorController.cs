using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OnlineFPS
{
    [Serializable]
    public struct SpectatorController : IComponentData
    {
        [Serializable]
        public struct Parameters
        {
            public float MoveSpeed;
            public float MoveSharpness;
            public float RotationSpeed;
        }

        public Parameters Params;
        public float3 Velocity;
    }
}