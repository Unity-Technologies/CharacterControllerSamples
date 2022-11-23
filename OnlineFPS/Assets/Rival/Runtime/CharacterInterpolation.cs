using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Serialization;

namespace Rival
{
    [Serializable]
    [WriteGroup(typeof(LocalToWorld))]
    public struct CharacterInterpolation : IComponentData
    {
        // These fields have to be in this order, because we copy memory to it
        
        /// <summary>
        /// The "previous" transform to interpolate from 
        /// </summary>
        public RigidTransform InterpolationFromTransform;
        /// <summary>
        /// Flags for skipping interpolation
        /// </summary>
        public byte InterpolationSkipping;
        private byte DefaultByte;
        
        /// <summary>
        /// Whether or not to interpolate position
        /// </summary>
        public byte InterpolatePosition;
        /// <summary>
        /// Whether or not to interpolate rotation
        /// </summary>
        public byte InterpolateRotation;

        /// <summary>
        /// Sets interpolation flags to skip position and rotation interpolation
        /// </summary>
        public void SkipNextInterpolation()
        {
            InterpolationSkipping |= 1;
            InterpolationSkipping |= 2;
        }

        /// <summary>
        /// Sets interpolation flags to skip position interpolation
        /// </summary>
        public void SkipNextPositionInterpolation()
        {
            InterpolationSkipping |= 1;
        }

        /// <summary>
        /// Sets interpolation flags to skip rotation interpolation
        /// </summary>
        public void SkipNextRotationInterpolation()
        {
            InterpolationSkipping |= 2;
        }

        /// <summary>
        /// Whether or not the interpolation flags are set to skip position interpolation
        /// </summary>
        /// <returns> Whether or not the interpolation flags are set to skip position interpolation </returns>
        public bool ShouldSkipNextPositionInterpolation()
        {
            return (InterpolationSkipping & 1) == 1;
        }

        /// <summary>
        /// Whether or not the interpolation flags are set to skip rotation interpolation
        /// </summary>
        /// <returns> Whether or not the interpolation flags are set to skip rotation interpolation </returns>
        public bool ShouldSkipNextRotationInterpolation()
        {
            return (InterpolationSkipping & 2) == 2;
        }
    }
}
