using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct SpectatorControllerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameResources>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            FPSInputActions.DefaultMapActions defaultActionsMap = GameInput.InputActions.DefaultMap;

            foreach (var (localTransform, spectatorController) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRW<SpectatorController>>())
            {
                float3 moveInput = Vector3.ClampMagnitude(new Vector3(
                        defaultActionsMap.Move.ReadValue<Vector2>().x,
                        defaultActionsMap.SpectatorVertical.ReadValue<float>(),
                        defaultActionsMap.Move.ReadValue<Vector2>().y),
                    1f);

                float2 lookInput = default;
                if (math.lengthsq(defaultActionsMap.LookConst.ReadValue<Vector2>()) >
                    math.lengthsq(defaultActionsMap.LookDelta.ReadValue<Vector2>()))
                {
                    // Gamepad look
                    lookInput = defaultActionsMap.LookConst.ReadValue<Vector2>() * GameSettings.LookSensitivity *
                                deltaTime;
                }
                else
                {
                    // Mouse look
                    lookInput = defaultActionsMap.LookDelta.ReadValue<Vector2>() * GameSettings.LookSensitivity;
                }

                // Velocity
                float3 worldMoveInput = math.mul(localTransform.ValueRW.Rotation, moveInput);
                float3 targetVelocity = worldMoveInput * spectatorController.ValueRW.Params.MoveSpeed;
                spectatorController.ValueRW.Velocity = math.lerp(spectatorController.ValueRW.Velocity, targetVelocity,
                    spectatorController.ValueRW.Params.MoveSharpness * deltaTime);
                localTransform.ValueRW.Position += spectatorController.ValueRW.Velocity * deltaTime;

                // Rotation
                quaternion rotation = localTransform.ValueRW.Rotation;
                quaternion rotationDeltaVertical =
                    quaternion.Euler(math.radians(-lookInput.y) * spectatorController.ValueRW.Params.RotationSpeed, 0f,
                        0f);
                quaternion rotationDeltaHorizontal = quaternion.Euler(0f,
                    math.radians(lookInput.x) * spectatorController.ValueRW.Params.RotationSpeed, 0f);
                rotation = math.mul(rotation, rotationDeltaVertical); // local rotation
                rotation = math.mul(rotationDeltaHorizontal, rotation); // world rotation
                localTransform.ValueRW.Rotation = rotation;
            }
        }
    }
}
