using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Rival
{
    /// <summary>
    /// Contains various math utility functions
    /// </summary>
    public static class MathUtilities
    {
        /// <summary>
        /// Calculates a rotation delta from a certain rotation to another
        /// </summary>
        /// <param name="from"> The source rotation </param>
        /// <param name="to"> The destination rotation </param>
        /// <returns> The rotation delta </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion FromToRotation(quaternion from, quaternion to)
        {
            return math.mul(math.inverse(from), to);
        }

        /// <summary>
        /// Calculates angles in radians between two normalized direction vectors
        /// </summary>
        /// <param name="from"> The source direction </param>
        /// <param name="to"> The destination direction </param>
        /// <returns> Angle in radians </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleRadians(float3 from, float3 to)
        {
            float denominator = (float)math.sqrt(math.lengthsq(from) * math.lengthsq(to));
            if (denominator < math.EPSILON)
                return 0F;

            float dot = math.clamp(math.dot(from, to) / denominator, -1F, 1F);
            return math.acos(dot);
        }

        /// <summary>
        /// Calculates the dot product between two normalized direction vectors that are at a specified angle (in radians) from each other
        /// </summary>
        /// <param name="angleRadians"> The angle in radians separating the two fictional direction vectors </param>
        /// <returns> The dot product result </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AngleRadiansToDotRatio(float angleRadians)
        {
            return math.cos(angleRadians);
        }

        /// <summary>
        /// Calculates the angles in radians that represent the angle difference between two normalized direction vectors that would have a specified dot product result
        /// </summary>
        /// <param name="dotRatio"> The dot product result between the two fictional normalized direction vectors </param>
        /// <returns> The angle in radians </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotRatioToAngleRadians(float dotRatio)
        {
            return math.acos(dotRatio);
        }

        /// <summary>
        /// Projects a vector on a plane
        /// </summary>
        /// <param name="vector"> The vector to project </param>
        /// <param name="onPlaneNormal"> The plane normal to project on </param>
        /// <returns> The projected vector </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ProjectOnPlane(float3 vector, float3 onPlaneNormal)
        {
            return vector - math.projectsafe(vector, onPlaneNormal);
        }

        /// <summary>
        /// Calculates the vector along the specified normalized direction that would have resulted in the specified "projected vector" if projected on the projected vector's normalized direction
        /// </summary>
        /// <param name="projectedVector"> The projected vector that we want to de-project </param>
        /// <param name="onNormalizedVector"> The desired normalized direction of the de-projected vector </param>
        /// <param name="maxLength"> The maximum length of the de-projected vector (de-projection can lead to very large or infinite values for near-perpendicular directions) </param>
        /// <returns> The resulting de-projected vector </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ReverseProjectOnVector(float3 projectedVector, float3 onNormalizedVector, float maxLength)
        {
            float projectionRatio = math.dot(math.normalizesafe(projectedVector), onNormalizedVector);
            if (projectionRatio == 0f)
            {
                return projectedVector;
            }

            float deprojectedLength = math.clamp(math.length(projectedVector) / projectionRatio, 0f, maxLength);
            return onNormalizedVector * deprojectedLength;
        }

        /// <summary>
        /// Clamps a vector to a maximum length
        /// </summary>
        /// <param name="vector"> The vector to clamp </param>
        /// <param name="maxLength"> The max length to clamp to </param>
        /// <returns> The clamped vector </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClampToMaxLength(float3 vector, float maxLength)
        {
            float sqrmag = math.lengthsq(vector);
            if (sqrmag > maxLength * maxLength)
            {
                float mag = math.sqrt(sqrmag);
                float normalized_x = vector.x / mag;
                float normalized_y = vector.y / mag;
                float normalized_z = vector.z / mag;
                return new float3(normalized_x * maxLength,
                    normalized_y * maxLength,
                    normalized_z * maxLength);
            }

            return vector;
        }

        /// <summary>
        /// Gets the up direction of a given quaternion
        /// </summary>
        /// <param name="rot"> The rotation in quaternion </param>
        /// <returns> The up direction </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetUpFromRotation(quaternion rot)
        {
            return math.mul(rot, math.up());
        }

        /// <summary>
        /// Gets the right direction of a given quaternion
        /// </summary>
        /// <param name="rot"> The rotation in quaternion </param>
        /// <returns> The right direction </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetRightFromRotation(quaternion rot)
        {
            return math.mul(rot, math.right());
        }

        /// <summary>
        /// Gets the forward direction of a given quaternion
        /// </summary>
        /// <param name="rot"> The rotation in quaternion </param>
        /// <returns> The forward direction </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetForwardFromRotation(quaternion rot)
        {
            return math.mul(rot, math.forward());
        }

        /// <summary>
        /// Returns an interpolant parameter that represents interpolating with a given sharpness
        /// </summary>
        /// <param name="sharpness"> The desired interpolation sharpness </param>
        /// <param name="dt"> The interpolation time delta </param>
        /// <returns> The resulting interpolant </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSharpnessInterpolant(float sharpness, float dt)
        {
            return math.saturate(1f - math.exp(-sharpness * dt));
        }

        /// <summary>
        /// Reorients a vector on a plane while constraining its direction so that the resulting vector is between the original vector and the specified direction
        /// </summary>
        /// <param name="vector"> The original vector to reorient </param>
        /// <param name="onPlaneNormal"> The plane on which the vector should be reoriented </param>
        /// <param name="alongDirection"> The target direction along which the vector should be reoriented </param>
        /// <returns> The reoriented vector </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ReorientVectorOnPlaneAlongDirection(float3 vector, float3 onPlaneNormal, float3 alongDirection)
        {
            float length = math.length(vector);

            if (length <= math.EPSILON)
                return float3.zero;

            float3 reorientAxis = math.cross(vector, alongDirection);
            float3 reorientedVector = math.normalizesafe(math.cross(onPlaneNormal, reorientAxis)) * length;

            return reorientedVector;
        }

        /// <summary>
        /// Builds a rotation that prioritizes having its up direction aligned with the designated up direction. Then, it orients its forwards towards the designated forward direction as much as is can without breaking the primary up direction constraint
        /// </summary>
        /// <param name="up"> The target up direction </param>
        /// <param name="forward"> The target forward direction </param>
        /// <returns> The resulting rotation </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion CreateRotationWithUpPriority(float3 up, float3 forward)
        {
            if (math.abs(math.dot(forward, up)) == 1f)
            {
                forward = math.forward();
            }
            forward = math.normalizesafe(MathUtilities.ProjectOnPlane(forward, up));

            return quaternion.LookRotationSafe(forward, up);
        }

        /// <summary>
        /// Creates perpendicular right and up directions based on a given forward direction
        /// </summary>
        /// <param name="fwd"> The designated forward direction </param>
        /// <param name="right"> Outputted right direction </param>
        /// <param name="up"> Outputted up direction </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetAxisSystemFromForward(float3 fwd, out float3 right, out float3 up)
        {
            float3 initialVector = math.up();
            if (math.dot(fwd, initialVector) > 0.9f)
            {
                initialVector = math.right();
            }

            right = math.normalizesafe(math.cross(initialVector, fwd));
            up = math.normalizesafe(math.cross(fwd, right));
        }
        
        /// <summary>
        /// Calculates the displacement vector of a worldspace point from one transform pose to the next
        /// </summary>
        /// <param name="pointWorldSpace"> The worldspace point that gets moved (before movement) </param>
        /// <param name="fromTransform"> The original transform pose </param>
        /// <param name="toTransform"> The destination transform pose </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculatePointDisplacement(float3 pointWorldSpace, RigidTransform fromTransform, RigidTransform toTransform)
        {
            float3 pointLocalPositionRelativeToPreviousTransform = math.transform(math.inverse(fromTransform), pointWorldSpace);
            float3 pointNewWorldPosition = math.transform(toTransform, pointLocalPositionRelativeToPreviousTransform);
            return pointNewWorldPosition - pointWorldSpace;
        }

        /// <summary>
        /// Calculates the displacement vector of a worldspace point relatively to a transform that moves with linear/angular velocity
        /// </summary>
        /// <param name="deltaTime"> The time delta of the movement </param>
        /// <param name="bodyRigidTransform"> The transform pose of the moving body </param>
        /// <param name="linearVelocity"> The linear velocity of the moving body </param>
        /// <param name="angularVelocity"> The angular velocity of the moving body </param>
        /// <param name="pointWorldSpace"> The worldspace point to move </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculatePointDisplacementFromVelocity(float deltaTime, RigidTransform bodyRigidTransform, float3 linearVelocity, float3 angularVelocity, float3 pointWorldSpace)
        {
            RigidTransform targetBodyRigidTransform = new RigidTransform();
            targetBodyRigidTransform.pos = bodyRigidTransform.pos + (linearVelocity * deltaTime);
            targetBodyRigidTransform.rot = math.mul(bodyRigidTransform.rot, quaternion.Euler(angularVelocity * deltaTime));

            return CalculatePointDisplacement(pointWorldSpace, bodyRigidTransform, targetBodyRigidTransform);
        }

        /// <summary>
        /// Sets a transform's rotation to a target rotation, but also modifies its position so that the end result is as if the transform has rotated around a given point in order to reach the specified rotation
        /// </summary>
        /// <param name="rotation"> The modified rotation </param>
        /// <param name="position"> The modified position </param>
        /// <param name="aroundPoint"> The point to rotate around </param>
        /// <param name="targetRotation"> The desired rotation </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRotationAroundPoint(ref quaternion rotation, ref float3 position, float3 aroundPoint, quaternion targetRotation)
        {
            float3 localPointToTranslation = math.mul(math.inverse(rotation), position - aroundPoint);
            position = aroundPoint + math.mul(targetRotation, localPointToTranslation);
            rotation = targetRotation;
        }

        /// <summary>
        /// Applies a rotation delta to a transform's rotation, but also modifies its position so that the end result is as if the transform has rotated around a given point in order to reach the specified rotation
        /// </summary>
        /// <param name="rotation"> The modified rotation </param>
        /// <param name="position"> The modified position </param>
        /// <param name="aroundPoint"> The point to rotate around </param>
        /// <param name="addedRotation"> The desired rotation delta </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RotateAroundPoint(ref quaternion rotation, ref float3 position, float3 aroundPoint, quaternion addedRotation)
        {
            float3 localPointToTranslation = math.mul(math.inverse(rotation), position - aroundPoint);
            rotation = math.mul(rotation, addedRotation);
            position = aroundPoint + math.mul(rotation, localPointToTranslation);
        }
    }
}