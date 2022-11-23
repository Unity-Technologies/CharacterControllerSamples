using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Rival
{
    public static class CharacterControlUtilities
    {
        /// <summary>
        // Calculates the signed slope angle (radians) in a given movement direction.
        // The resulting angle will be positive if the slope goes up, and negative if the slope goes down.
        /// </summary>
        /// <param name="useDegrees"> Whether to use degrees or radians for the returned result </param>
        /// <param name="moveDirection"> The direction the character is moving in </param>
        /// <param name="slopeNormal"> The normal of the evaluated slope </param>
        /// <param name="groundingUp"> The character grounding up direction </param>
        /// <returns> The effective signed slope angle </returns>
        public static float GetSlopeAngleTowardsDirection(bool useDegrees, float3 moveDirection, float3 slopeNormal, float3 groundingUp)
        {
            float3 moveDirectionOnSlopePlane = math.normalizesafe(MathUtilities.ProjectOnPlane(moveDirection, slopeNormal));
            float angleRadiansWithUp = MathUtilities.AngleRadians(moveDirectionOnSlopePlane, groundingUp);

            if (useDegrees)
            {
                return 90f - math.degrees(angleRadiansWithUp);
            }
            else
            {
                return (math.PI * 0.5f) - angleRadiansWithUp;
            }
        }

        /// <summary>
        /// Handles updating character velocity for standard interpolated ground movement
        /// </summary>
        /// <param name="velocity"> Current character velocity </param>
        /// <param name="targetVelocity"> Desired character velocity </param>
        /// <param name="sharpness"> The sharpness of the velocity change (how quickly it changes) </param>
        /// <param name="deltaTime"> The character update time delta </param>
        /// <param name="groundingUp"> The character's grounding up direction </param>
        /// <param name="groundedHitNormal"> The character ground hit normal </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StandardGroundMove_Interpolated(ref float3 velocity, float3 targetVelocity, float sharpness, float deltaTime, float3 groundingUp, float3 groundedHitNormal)
        {
            velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, groundedHitNormal, groundingUp);
            targetVelocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(targetVelocity, groundedHitNormal, groundingUp);
            InterpolateVelocityTowardsTarget(ref velocity, targetVelocity, deltaTime, sharpness);
        }

        /// <summary>
        /// Handles updating character velocity for standard accelerated ground movement
        /// </summary>
        /// <param name="velocity"> Current character velocity </param>
        /// <param name="acceleration"> Acceleration strength </param>
        /// <param name="maxSpeed"> Maximum speed that can be reached </param>
        /// <param name="deltaTime"> The character update time delta </param>
        /// <param name="movementPlaneUp"> The up direction of the horizontal reference plane that the character moves on </param>
        /// <param name="groundedHitNormal"> The character ground hit normal </param>
        /// <param name="forceNoMaxSpeedExcess"> Whether or not to trim character velocity to an absolute maximum (prevents velocity exploits, but can also break preservation of momentum) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StandardGroundMove_Accelerated(ref float3 velocity, float3 acceleration, float maxSpeed, float deltaTime, float3 movementPlaneUp, float3 groundedHitNormal, bool forceNoMaxSpeedExcess)
        {
            float3 addedVelocityFromAcceleration = float3.zero;
            AccelerateVelocity(ref addedVelocityFromAcceleration, acceleration, deltaTime);

            velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, groundedHitNormal, movementPlaneUp);
            addedVelocityFromAcceleration = MathUtilities.ReorientVectorOnPlaneAlongDirection(addedVelocityFromAcceleration, groundedHitNormal, movementPlaneUp);
            ClampAdditiveVelocityToMaxSpeedOnPlane(ref addedVelocityFromAcceleration, velocity, maxSpeed, groundedHitNormal, forceNoMaxSpeedExcess);
            velocity += addedVelocityFromAcceleration;
        }

        /// <summary>
        /// Handles updating character velocity for standard accelerated air movement
        /// </summary>
        /// <param name="velocity"> Current character velocity </param>
        /// <param name="acceleration"> Acceleration strength </param>
        /// <param name="maxSpeed"> Maximum speed that can be reached </param>
        /// <param name="movementPlaneUp"> The up direction of the horizontal reference plane that the character moves on </param>
        /// <param name="deltaTime"> The character update time delta </param>
        /// <param name="forceNoMaxSpeedExcess"> Whether or not to trim character velocity to an absolute maximum (prevents velocity exploits, but can also break preservation of momentum) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StandardAirMove(ref float3 velocity, float3 acceleration, float maxSpeed, float3 movementPlaneUp, float deltaTime, bool forceNoMaxSpeedExcess)
        {
            float3 addedVelocityFromAcceleration = float3.zero;
            AccelerateVelocity(ref addedVelocityFromAcceleration, acceleration, deltaTime);
            ClampAdditiveVelocityToMaxSpeedOnPlane(ref addedVelocityFromAcceleration, velocity, maxSpeed, movementPlaneUp, forceNoMaxSpeedExcess);
            velocity += addedVelocityFromAcceleration;
        }

        /// <summary>
        /// Interpolates a velocity towards a target velocity, with a given sharpness
        /// </summary>
        /// <param name="velocity"> The modified velocity </param>
        /// <param name="targetVelocity"> THe target velocity </param>
        /// <param name="deltaTime"> The character update time delta </param>
        /// <param name="interpolationSharpness"> The sharpness of the velocity change (how quickly it changes) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InterpolateVelocityTowardsTarget(ref float3 velocity, float3 targetVelocity, float deltaTime, float interpolationSharpness)
        {
            velocity = math.lerp(velocity, targetVelocity, MathUtilities.GetSharpnessInterpolant(interpolationSharpness, deltaTime));
        }

        /// <summary>
        /// Accelerates a velocity
        /// </summary>
        /// <param name="velocity"> The modified velocity </param>
        /// <param name="acceleration"> The acceleration strength </param>
        /// <param name="deltaTime"> The character update time delta </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccelerateVelocity(ref float3 velocity, float3 acceleration, float deltaTime)
        {
            velocity += acceleration * deltaTime;
        }

        /// <summary>
        /// Add a velocity vector to another velocity, and clamp the resulting total velocity, but only on a given plane (the velocity along the plane's up axis remains unclamped)
        /// </summary>
        /// <param name="additiveVelocity"> Added velocity </param>
        /// <param name="originalVelocity"> Original velocity </param>
        /// <param name="maxSpeed"> Maximum allowed speed on the clamping plane </param>
        /// <param name="movementPlaneUp"> Up direction of the clamping plane </param>
        /// <param name="forceNoMaxSpeedExcess"> Whether or not to trim character velocity to an absolute maximum (prevents velocity exploits, but can also break preservation of momentum) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClampAdditiveVelocityToMaxSpeedOnPlane(ref float3 additiveVelocity, float3 originalVelocity, float maxSpeed, float3 movementPlaneUp, bool forceNoMaxSpeedExcess)
        {
            if (forceNoMaxSpeedExcess)
            {
                float3 totalVelocity = originalVelocity + additiveVelocity;
                float3 velocityUp = math.projectsafe(totalVelocity, movementPlaneUp);
                float3 velocityHorizontal = MathUtilities.ProjectOnPlane(totalVelocity, movementPlaneUp);
                velocityHorizontal = MathUtilities.ClampToMaxLength(velocityHorizontal, maxSpeed);
                additiveVelocity = (velocityHorizontal + velocityUp) - originalVelocity;
            }
            else
            {
                float maxSpeedSq = maxSpeed * maxSpeed;

                float3 additiveVelocityOnPlaneUp = math.projectsafe(additiveVelocity, movementPlaneUp);
                float3 additiveVelocityOnPlane = additiveVelocity - additiveVelocityOnPlaneUp;

                float3 originalVelocityOnPlaneUp = math.projectsafe(originalVelocity, movementPlaneUp);
                float3 originalVelocityOnPlane = originalVelocity - originalVelocityOnPlaneUp;

                float3 totalVelocityOnPlane = originalVelocityOnPlane + additiveVelocityOnPlane;

                if (math.lengthsq(totalVelocityOnPlane) > maxSpeedSq)
                {
                    float3 originalVelocityForwardOnPlane = math.normalizesafe(originalVelocityOnPlane);
                    float3 totalVelocityDirectionOnPlane = math.normalizesafe(totalVelocityOnPlane);

                    float3 totalClampedVelocityOnPlane = float3.zero;
                    if (math.dot(totalVelocityDirectionOnPlane, originalVelocityForwardOnPlane) > 0f)
                    {
                        float3 originalVelocityRightOnPlane = math.normalizesafe(math.cross(originalVelocityForwardOnPlane, movementPlaneUp));

                        // trim additive velocity excess in original velocity direction
                        float3 trimmedTotalVelocityForwardComponent = MathUtilities.ClampToMaxLength(math.projectsafe(totalVelocityOnPlane, originalVelocityForwardOnPlane), math.max(maxSpeed, math.length(originalVelocityOnPlane)));
                        float3 trimmedTotalVelocityRightComponent = MathUtilities.ClampToMaxLength(math.projectsafe(totalVelocityOnPlane, originalVelocityRightOnPlane), maxSpeed);
                        totalClampedVelocityOnPlane = trimmedTotalVelocityForwardComponent + trimmedTotalVelocityRightComponent;
                    }
                    else
                    {
                        // clamp totalvelocity to circle
                        totalClampedVelocityOnPlane = MathUtilities.ClampToMaxLength(totalVelocityOnPlane, maxSpeed);
                    }

                    float3 clampedAdditiveVelocityOnPlane = totalClampedVelocityOnPlane - originalVelocityOnPlane;
                    additiveVelocity = clampedAdditiveVelocityOnPlane + additiveVelocityOnPlaneUp;
                }
            }
        }

        /// <summary>
        /// Handles standard jumping logic for a character
        /// </summary>
        /// <param name="characterBody"> The character's character body component </param>
        /// <param name="jumpVelocity"> The velocity of the jump </param>
        /// <param name="cancelVelocityBeforeJump"> Whether or not to cancel-out any velocity in the velocity-canceling up direction before applying the jump velocity </param>
        /// <param name="velocityCancelingUpDirection"> The velocity-canceling up direction </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StandardJump(ref KinematicCharacterBody characterBody, float3 jumpVelocity, bool cancelVelocityBeforeJump, float3 velocityCancelingUpDirection)
        {
            // Without this, the ground snapping mecanism would prevent you from jumping
            characterBody.IsGrounded = false;
            characterBody.GroundHit = default;

            if (cancelVelocityBeforeJump)
            {
                characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, velocityCancelingUpDirection);
            }

            characterBody.RelativeVelocity += jumpVelocity;
        }

        /// <summary>
        /// Applies drag to a velocity
        /// </summary>
        /// <param name="velocity">  </param>
        /// <param name="deltaTime"></param>
        /// <param name="drag"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyDragToVelocity(ref float3 velocity, float deltaTime, float drag)
        {
            velocity *= (1f / (1f + (drag * deltaTime)));
        }

        /// <summary>
        /// Calculates the velocity required to move by a given position delta over the next time delta
        /// </summary>
        /// <param name="deltaTime"> The time delta </param>
        /// <param name="positionDelta"> The position delta </param>
        /// <returns> The required velocity for the move </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetLinearVelocityForMovePosition(float deltaTime, float3 positionDelta)
        {
            if (deltaTime > 0f)
            {
                return positionDelta / deltaTime;
            }

            return default;
        }

        /// <summary>
        /// Interpolates a rotation to make it face a direction
        /// </summary>
        /// <param name="rotation"> The modified rotation </param>
        /// <param name="deltaTime"> The time delta </param>
        /// <param name="direction"> The faced direction </param>
        /// <param name="orientationSharpness"> The sharpness of the rotation (how fast it interpolates) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SlerpRotationTowardsDirection(ref quaternion rotation, float deltaTime, float3 direction, float orientationSharpness)
        {
            if (math.lengthsq(direction) > 0f)
            {
                rotation = math.slerp(rotation, quaternion.LookRotationSafe(math.normalizesafe(direction), math.up()), MathUtilities.GetSharpnessInterpolant(orientationSharpness, deltaTime));
            }
        }

        /// <summary>
        /// Interpolates a rotation to make it face a direction, but constrains rotation to make it pivot around a designated up axis
        /// </summary>
        /// <param name="rotation"> The modified rotation </param>
        /// <param name="deltaTime"> The time delta </param>
        /// <param name="direction"> The direction to face </param>
        /// <param name="upDirection"> The rotation constraint up axis </param>
        /// <param name="orientationSharpness"> The sharpness of the rotation (how fast it interpolates) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SlerpRotationTowardsDirectionAroundUp(ref quaternion rotation, float deltaTime, float3 direction, float3 upDirection, float orientationSharpness)
        {
            if (math.lengthsq(direction) > 0f)
            {
                rotation = math.slerp(rotation, MathUtilities.CreateRotationWithUpPriority(upDirection, direction), MathUtilities.GetSharpnessInterpolant(orientationSharpness, deltaTime));
            }
        }

        /// <summary>
        /// Interpolates a rotation to make its up direction point to the designated up direction
        /// </summary>
        /// <param name="rotation"> The modified rotation </param>
        /// <param name="deltaTime"> The time delta </param>
        /// <param name="direction"> The up direction to face </param>
        /// <param name="orientationSharpness"> The sharpness of the rotation (how fast it interpolates) </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SlerpCharacterUpTowardsDirection(ref quaternion rotation, float deltaTime, float3 direction, float orientationSharpness)
        {
            quaternion targetRotation = MathUtilities.CreateRotationWithUpPriority(direction, MathUtilities.GetForwardFromRotation(rotation));
            rotation = math.slerp(rotation, targetRotation, MathUtilities.GetSharpnessInterpolant(orientationSharpness, deltaTime));
        }
    }
}
