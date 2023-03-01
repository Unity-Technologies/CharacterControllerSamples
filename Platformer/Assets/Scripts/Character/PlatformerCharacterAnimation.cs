using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.CharacterController;

[Serializable]
public struct PlatformerCharacterAnimation : IComponentData
{
    [HideInInspector] public int ClipIndexParameterHash;

    [HideInInspector] public int IdleClip;
    [HideInInspector] public int RunClip;
    [HideInInspector] public int SprintClip;
    [HideInInspector] public int InAirClip;
    [HideInInspector] public int LedgeGrabMoveClip;
    [HideInInspector] public int LedgeStandUpClip;
    [HideInInspector] public int WallRunLeftClip;
    [HideInInspector] public int WallRunRightClip;
    [HideInInspector] public int CrouchIdleClip;
    [HideInInspector] public int CrouchMoveClip;
    [HideInInspector] public int ClimbingMoveClip;
    [HideInInspector] public int SwimmingIdleClip;
    [HideInInspector] public int SwimmingMoveClip;
    [HideInInspector] public int DashClip;
    [HideInInspector] public int RopeHangClip;
    [HideInInspector] public int SlidingClip;

    [HideInInspector] public CharacterState LastAnimationCharacterState;
}

public static class PlatformerCharacterAnimationHandler
{
    public static void UpdateAnimation(
        Animator animator,
        ref PlatformerCharacterAnimation characterAnimation,
        in KinematicCharacterBody characterBody,
        in PlatformerCharacterComponent characterComponent,
        in PlatformerCharacterStateMachine characterStateMachine,
        in PlatformerCharacterControl characterControl,
        in LocalTransform localTransform)
    {
        float velocityMagnitude = math.length(characterBody.RelativeVelocity);
        switch (characterStateMachine.CurrentState)
        {
            case CharacterState.GroundMove:
            {
                if (math.length(characterControl.MoveVector) < 0.01f)
                {
                    animator.speed = 1f;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.IdleClip);
                }
                else
                {
                    if (characterComponent.IsSprinting)
                    {
                        float velocityRatio = velocityMagnitude / characterComponent.GroundSprintMaxSpeed;
                        animator.speed = velocityRatio;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SprintClip);
                    }
                    else
                    {
                        float velocityRatio = velocityMagnitude / characterComponent.GroundRunMaxSpeed;
                        animator.speed = velocityRatio;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.RunClip);
                    }
                }
            }
                break;
            case CharacterState.Crouched:
            {
                if (math.length(characterControl.MoveVector) < 0.01f)
                {
                    animator.speed = 1f;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.CrouchIdleClip);
                }
                else
                {
                    float velocityRatio = velocityMagnitude / characterComponent.CrouchedMaxSpeed;
                    animator.speed = velocityRatio;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.CrouchMoveClip);
                }
            }
                break;
            case CharacterState.AirMove:
            {
                animator.speed = 1f;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.InAirClip);
            }
                break;
            case CharacterState.Dashing:
            {
                animator.speed = 1f;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.DashClip);
            }
                break;
            case CharacterState.WallRun:
            {
                bool wallIsOnTheLeft = math.dot(MathUtilities.GetRightFromRotation(localTransform.Rotation), characterComponent.LastKnownWallNormal) > 0f;
                animator.speed = 1f;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, wallIsOnTheLeft ? characterAnimation.WallRunLeftClip : characterAnimation.WallRunRightClip);
            }
                break;
            case CharacterState.RopeSwing:
            {
                animator.speed = 1f;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.RopeHangClip);
            }
                break;
            case CharacterState.Climbing:
            {
                float velocityRatio = velocityMagnitude / characterComponent.ClimbingSpeed;
                animator.speed = velocityRatio;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.ClimbingMoveClip);
            }
                break;
            case CharacterState.LedgeGrab:
            {
                float velocityRatio = velocityMagnitude / characterComponent.LedgeMoveSpeed;
                animator.speed = velocityRatio;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.LedgeGrabMoveClip);
            }
                break;
            case CharacterState.LedgeStandingUp:
            {
                animator.speed = 1f;
                //animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.LedgeStandUpClip);
            }
                break;
            case CharacterState.Swimming:
            {
                float velocityRatio = velocityMagnitude / characterComponent.SwimmingMaxSpeed;
                if (velocityRatio < 0.1f)
                {
                    animator.speed = 1f;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SwimmingIdleClip);
                }
                else
                {
                    animator.speed = velocityRatio;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SwimmingMoveClip);
                }
            }
                break;
            case CharacterState.Rolling:
            case CharacterState.FlyingNoCollisions:
            {
                animator.speed = 1f;
                animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.IdleClip);
            }
                break;
        }

        characterAnimation.LastAnimationCharacterState = characterStateMachine.CurrentState;
    }
}