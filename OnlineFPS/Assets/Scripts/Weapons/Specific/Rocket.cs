using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using UnityEngine;

namespace OnlineFPS
{
    public struct Rocket : IComponentData, IEnableableComponent
    {
        public float DirectHitDamage;
        public float MaxRadiusDamage;
        public float DamageRadius;
        public float ExplosionSize;

        public byte HasProcessedHitSimulation;
        public byte HasProcessedHitVFX;
    }
}