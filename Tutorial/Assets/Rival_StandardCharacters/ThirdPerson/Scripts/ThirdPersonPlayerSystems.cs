using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics.Systems;
using Rival;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class ThirdPersonPlayerInputsSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(SystemAPI.QueryBuilder().WithAll<ThirdPersonPlayer, ThirdPersonPlayerInputs>().Build());
    }
    
    protected override void OnUpdate()
    {
        foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<ThirdPersonPlayerInputs>, ThirdPersonPlayer>())
        {
            playerInputs.ValueRW.MoveInput = new float2();
            playerInputs.ValueRW.MoveInput.y += Input.GetKey(KeyCode.W) ? 1f : 0f;
            playerInputs.ValueRW.MoveInput.y += Input.GetKey(KeyCode.S) ? -1f : 0f;
            playerInputs.ValueRW.MoveInput.x += Input.GetKey(KeyCode.D) ? 1f : 0f;
            playerInputs.ValueRW.MoveInput.x += Input.GetKey(KeyCode.A) ? -1f : 0f;
            
            playerInputs.ValueRW.CameraLookInput = new float2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            playerInputs.ValueRW.CameraZoomInput = -Input.mouseScrollDelta.y;
            
            // For button presses that need to be queried during fixed update, use the "FixedInputEvent" helper struct.
            // This is part of a strategy for proper handling of button press events that are consumed during the fixed update group
            if (Input.GetKeyDown(KeyCode.Space))
            {
                playerInputs.ValueRW.JumpPressed.Set(playerInputs.ValueRW.FixedInputsTick);
            }

            playerInputs.ValueRW.SprintHeld = Input.GetKey(KeyCode.LeftShift);
        }
    }
}

/// <summary>
/// Apply inputs that need to be read at a variable rate
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct ThirdPersonPlayerVariableStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<ThirdPersonPlayer, ThirdPersonPlayerInputs>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerInputs, player) in SystemAPI.Query<ThirdPersonPlayerInputs, ThirdPersonPlayer>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<OrbitCameraControl>(player.ControlledCamera))
            {
                OrbitCameraControl cameraControl = SystemAPI.GetComponent<OrbitCameraControl>(player.ControlledCamera);
                
                cameraControl.FollowedCharacterEntity = player.ControlledCharacter;
                cameraControl.Look = playerInputs.CameraLookInput;
                cameraControl.Zoom = playerInputs.CameraZoomInput;

                SystemAPI.SetComponent(player.ControlledCamera, cameraControl);
            }
        }
    }
}

/// <summary>
/// Apply inputs that need to be read at a fixed rate.
/// It is necessary to handle this as part of the fixed step group, in case your framerate is lower than the fixed step rate.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct ThirdPersonPlayerFixedStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<ThirdPersonPlayer, ThirdPersonPlayerInputs>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<ThirdPersonPlayerInputs>, ThirdPersonPlayer>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<ThirdPersonCharacterControl>(player.ControlledCharacter))
            {
                ThirdPersonCharacterControl characterControl = SystemAPI.GetComponent<ThirdPersonCharacterControl>(player.ControlledCharacter);

                float3 characterUp = MathUtilities.GetUpFromRotation(SystemAPI.GetComponent<LocalTransform>(player.ControlledCharacter).Rotation);
                
                // Get camera rotation data, since our movement is relative to it
                quaternion cameraRotation = quaternion.identity;
                if (SystemAPI.HasComponent<LocalTransform>(player.ControlledCamera))
                {
                    cameraRotation = SystemAPI.GetComponent<LocalTransform>(player.ControlledCamera).Rotation;
                }
                float3 cameraForwardOnUpPlane = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(cameraRotation), characterUp));
                float3 cameraRight = MathUtilities.GetRightFromRotation(cameraRotation);

                // Move
                characterControl.MoveVector = (playerInputs.ValueRW.MoveInput.y * cameraForwardOnUpPlane) + (playerInputs.ValueRW.MoveInput.x * cameraRight);
                characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

                // Jump
                // We use the "FixedInputEvent" helper struct here to detect if a jump press event happened since the last time a fixed update was processed.
                // This is part of a strategy for proper handling of button press events that are consumed during the fixed update group.
                characterControl.Jump = playerInputs.ValueRW.JumpPressed.IsSet(playerInputs.ValueRW.FixedInputsTick);
                
                // Sprint
                characterControl.Sprint = playerInputs.ValueRW.SprintHeld;

                SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
            }

            // Increment fixed inputs tick AFTER all input was queried.
            // This is part of a strategy for proper handling of button press events that are consumed during the fixed update group.
            playerInputs.ValueRW.FixedInputsTick++;
        }
    }
}