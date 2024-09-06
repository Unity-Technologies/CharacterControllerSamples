using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.CharacterController;

namespace OnlineFPS
{
    public static class FirstPersonCharacterUtilities
    {
        public static quaternion GetCurrentWorldViewRotation(quaternion characterRotation,
            quaternion localCharacterViewRotation)
        {
            return math.mul(characterRotation, localCharacterViewRotation);
        }

        public static void GetCurrentWorldViewDirectionAndRotation(
            quaternion characterRotation,
            quaternion localCharacterViewRotation,
            out float3 worldCharacterViewDirection,
            out quaternion worldCharacterViewRotation)
        {
            worldCharacterViewRotation = GetCurrentWorldViewRotation(characterRotation, localCharacterViewRotation);
            worldCharacterViewDirection = math.mul(worldCharacterViewRotation, math.forward());
        }

        public static void ComputeFinalRotationsFromTargetLookDirection(
            ref quaternion characterRotation,
            ref quaternion localCharacterViewRotation,
            ref float3 targetLookDirection,
            ref float viewPitchDegrees,
            float viewRollDegrees,
            float minViewPitchDegrees,
            float maxViewPitchDegrees,
            float3 characterUp)
        {
            // rotate character root to point at look direction on character up plane
            float3 newCharacterForward =
                math.normalizesafe(MathUtilities.ProjectOnPlane(targetLookDirection, characterUp));
            characterRotation = quaternion.LookRotationSafe(newCharacterForward, characterUp);

            // calculate view pitch angles to look at target direction
            viewPitchDegrees = math.degrees(MathUtilities.AngleRadians(newCharacterForward, targetLookDirection));
            if (math.dot(characterUp, targetLookDirection) < 0f)
            {
                viewPitchDegrees *= -1f;
            }

            viewPitchDegrees = math.clamp(viewPitchDegrees, minViewPitchDegrees, maxViewPitchDegrees);

            localCharacterViewRotation = CalculateLocalViewRotation(viewPitchDegrees, viewRollDegrees);
        }

        public static void ComputeFinalRotationsFromRotationDelta(
            ref quaternion characterRotation,
            ref float viewPitchDegrees,
            float2 yawPitchDeltaDegrees,
            float viewRollDegrees,
            float minPitchDegrees,
            float maxPitchDegrees,
            out float canceledPitchDegrees,
            out quaternion viewLocalRotation)
        {
            // Yaw
            quaternion yawRotation = quaternion.Euler(math.up() * math.radians(yawPitchDeltaDegrees.x));
            characterRotation = math.mul(characterRotation, yawRotation);

            // Pitch
            viewPitchDegrees += yawPitchDeltaDegrees.y;
            float viewPitchAngleDegreesBeforeClamp = viewPitchDegrees;
            viewPitchDegrees = math.clamp(viewPitchDegrees, minPitchDegrees, maxPitchDegrees);
            canceledPitchDegrees = yawPitchDeltaDegrees.y - (viewPitchAngleDegreesBeforeClamp - viewPitchDegrees);

            viewLocalRotation = CalculateLocalViewRotation(viewPitchDegrees, viewRollDegrees);
        }

        public static void ComputeFinalRotationsFromRotationDelta(
            ref float viewPitchDegrees,
            ref float characterRotationYDegrees,
            float3 characterTransformUp,
            float2 yawPitchDeltaDegrees,
            float viewRollDegrees,
            float minPitchDegrees,
            float maxPitchDegrees,
            out quaternion characterRotation,
            out float canceledPitchDegrees,
            out quaternion viewLocalRotation)
        {
            // Yaw
            characterRotationYDegrees += yawPitchDeltaDegrees.x;
            ComputeRotationFromYAngleAndUp(characterRotationYDegrees, characterTransformUp, out characterRotation);

            // Pitch
            viewPitchDegrees += yawPitchDeltaDegrees.y;
            float viewPitchAngleDegreesBeforeClamp = viewPitchDegrees;
            viewPitchDegrees = math.clamp(viewPitchDegrees, minPitchDegrees, maxPitchDegrees);
            canceledPitchDegrees = yawPitchDeltaDegrees.y - (viewPitchAngleDegreesBeforeClamp - viewPitchDegrees);

            viewLocalRotation = CalculateLocalViewRotation(viewPitchDegrees, viewRollDegrees);
        }

        public static void ComputeRotationFromYAngleAndUp(
            float characterRotationYDegrees,
            float3 characterTransformUp,
            out quaternion characterRotation)
        {
            characterRotation =
                math.mul(MathUtilities.CreateRotationWithUpPriority(characterTransformUp, math.forward()),
                    quaternion.Euler(0f, math.radians(characterRotationYDegrees), 0f));
        }

        public static quaternion CalculateLocalViewRotation(float viewPitchDegrees, float viewRollDegrees)
        {
            // Pitch
            quaternion viewLocalRotation = quaternion.AxisAngle(-math.right(), math.radians(viewPitchDegrees));

            // Roll
            viewLocalRotation = math.mul(viewLocalRotation,
                quaternion.AxisAngle(math.forward(), math.radians(viewRollDegrees)));

            return viewLocalRotation;
        }

        public static float3 ComputeTargetLookDirectionFromRotationAngles(
            ref float viewPitchDegrees,
            float minViewPitchDegrees,
            float maxViewPitchDegrees,
            float2 pitchYawDegrees,
            quaternion characterRotation)
        {
            // Yaw
            quaternion yawRotation = quaternion.Euler(math.up() * math.radians(pitchYawDegrees.x));
            quaternion targetRotation = math.mul(characterRotation, yawRotation);

            // Pitch
            float tmpViewPitchAngleDegrees = viewPitchDegrees + pitchYawDegrees.y;
            tmpViewPitchAngleDegrees = math.clamp(tmpViewPitchAngleDegrees, minViewPitchDegrees, maxViewPitchDegrees);
            quaternion pitchRotation = quaternion.Euler(-math.right() * math.radians(tmpViewPitchAngleDegrees));
            targetRotation = math.mul(targetRotation, pitchRotation);

            return math.mul(targetRotation, math.forward());
        }
    }
}
