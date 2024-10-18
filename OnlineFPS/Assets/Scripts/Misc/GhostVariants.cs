using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Unity.CharacterController;

namespace OnlineFPS
{
    [CreateBefore(typeof(Unity.NetCode.TransformDefaultVariantSystem))]
    public partial class DefaultVariantSystem : DefaultVariantSystemBase
    {
        protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
        {
            defaultVariants.Add(typeof(LocalTransform), Rule.ForAll(typeof(DontSerializeVariant)));
            defaultVariants.Add(typeof(KinematicCharacterBody),
                Rule.ForAll(typeof(KinematicCharacterBody_GhostVariant)));
        }
    }

    [GhostComponentVariation(typeof(KinematicCharacterBody))]
    [GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct KinematicCharacterBody_GhostVariant
    {
        [GhostField(Quantization = 1000)] public float3 RelativeVelocity;
        [GhostField()] public bool IsGrounded;
    }

    // Character interpolation must be Client-only, because it would prevent proper LocalToWorld updates on server otherwise
    [GhostComponentVariation(typeof(CharacterInterpolation))]
    [GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
    public struct CharacterInterpolation_GhostVariant
    {
    }
}