using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Rival
{
    /// <summary>
    /// Component tracking the current and previous poses of a transform at a fixed timestep
    /// </summary>
    [Serializable]
    public struct TrackedTransform : IComponentData
    {
        /// <summary>
        /// Current transform
        /// </summary>
        [HideInInspector]
        public RigidTransform CurrentFixedRateTransform;
        /// <summary>
        /// Previous transform
        /// </summary>
        [HideInInspector]
        public RigidTransform PreviousFixedRateTransform;

        /// <summary>
        /// Calculate a point that results from moving a given point from the previous transform to the current transform
        /// </summary>
        /// <param name="point"> The point to move </param>
        /// <returns> The moved point </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 CalculatePointDisplacement(float3 point)
        {
            float3 characterLocalPositionToPreviousParentTransform = math.transform(math.inverse(PreviousFixedRateTransform), point);
            float3 characterTargetTranslation = math.transform(CurrentFixedRateTransform, characterLocalPositionToPreviousParentTransform);
            return characterTargetTranslation - point;
        }

        /// <summary>
        /// Calculates the linear velocity of a point that gets moved from the previous transform to the current transform over a time delta
        /// </summary>
        /// <param name="point"> The point to move </param>
        /// <param name="deltaTime"> The time delta </param>
        /// <returns> The calculated linear velocity </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 CalculatePointVelocity(float3 point, float deltaTime)
        {
            return CalculatePointDisplacement(point) / deltaTime;
        }
    }
}