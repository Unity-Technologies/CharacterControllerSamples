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
public partial class PlatformerPlayerInputsSystem : SystemBase
{
    private PlatformerInputActions.GameplayMapActions _defaultActionsMap;
    
    protected override void OnCreate()
    {
        PlatformerInputActions inputActions = new PlatformerInputActions();
        inputActions.Enable();
        inputActions.GameplayMap.Enable();
        _defaultActionsMap = inputActions.GameplayMap;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlatformerPlayer, PlatformerPlayerInputs>().Build());
    }
    
    protected override void OnUpdate()
    {
        uint fixedTick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;
        
        foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<PlatformerPlayerInputs>, PlatformerPlayer>())
        {
            playerInputs.ValueRW.Move = Vector2.ClampMagnitude(_defaultActionsMap.Move.ReadValue<Vector2>(), 1f);
            playerInputs.ValueRW.Look = _defaultActionsMap.LookDelta.ReadValue<Vector2>();
            if (math.lengthsq(_defaultActionsMap.LookConst.ReadValue<Vector2>()) > math.lengthsq(_defaultActionsMap.LookDelta.ReadValue<Vector2>()))
            {
                playerInputs.ValueRW.Look = _defaultActionsMap.LookConst.ReadValue<Vector2>() * SystemAPI.Time.DeltaTime;
            }
            playerInputs.ValueRW.CameraZoom = _defaultActionsMap.CameraZoom.ReadValue<float>();
            playerInputs.ValueRW.SprintHeld = _defaultActionsMap.Sprint.IsPressed();
            playerInputs.ValueRW.RollHeld = _defaultActionsMap.Roll.IsPressed();
            playerInputs.ValueRW.JumpHeld = _defaultActionsMap.Jump.IsPressed();

            if (_defaultActionsMap.Jump.WasPressedThisFrame())
            {
                playerInputs.ValueRW.JumpPressed.Set(fixedTick);
            }
            if (_defaultActionsMap.Dash.WasPressedThisFrame())
            {
                playerInputs.ValueRW.DashPressed.Set(fixedTick);
            }
            if (_defaultActionsMap.Crouch.WasPressedThisFrame())
            {
                playerInputs.ValueRW.CrouchPressed.Set(fixedTick);
            }
            if (_defaultActionsMap.Rope.WasPressedThisFrame())
            {
                playerInputs.ValueRW.RopePressed.Set(fixedTick);
            }
            if (_defaultActionsMap.Climb.WasPressedThisFrame())
            {
                playerInputs.ValueRW.ClimbPressed.Set(fixedTick);
            }
            if (_defaultActionsMap.FlyNoCollisions.WasPressedThisFrame())
            {
                playerInputs.ValueRW.FlyNoCollisionsPressed.Set(fixedTick);
            }
        }
    }
}

/// <summary>
/// Apply inputs that need to be read at a variable rate
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct PlatformerPlayerVariableStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlatformerPlayer, PlatformerPlayerInputs>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerInputs, player) in SystemAPI.Query<PlatformerPlayerInputs, PlatformerPlayer>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<OrbitCameraControl>(player.ControlledCamera))
            {
                OrbitCameraControl cameraControl = SystemAPI.GetComponent<OrbitCameraControl>(player.ControlledCamera);
                
                cameraControl.FollowedCharacterEntity = player.ControlledCharacter;
                cameraControl.Look = playerInputs.Look;
                cameraControl.Zoom = playerInputs.CameraZoom;

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
public partial struct PlatformerPlayerFixedStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlatformerPlayer, PlatformerPlayerInputs>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        uint fixedTick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;
        
        foreach (var (playerInputs, player) in SystemAPI.Query<RefRW<PlatformerPlayerInputs>, PlatformerPlayer>()
                     .WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<PlatformerCharacterControl>(player.ControlledCharacter) && SystemAPI.HasComponent<PlatformerCharacterStateMachine>(player.ControlledCharacter))
            {
                PlatformerCharacterControl characterControl = SystemAPI.GetComponent<PlatformerCharacterControl>(player.ControlledCharacter);
                PlatformerCharacterStateMachine stateMachine = SystemAPI.GetComponent<PlatformerCharacterStateMachine>(player.ControlledCharacter);

                // Get camera rotation data, since our movement is relative to it
                quaternion cameraRotation = quaternion.identity;
                if (SystemAPI.HasComponent<LocalTransform>(player.ControlledCamera))
                {
                    cameraRotation = SystemAPI.GetComponent<LocalTransform>(player.ControlledCamera).Rotation;
                }
                
                stateMachine.GetMoveVectorFromPlayerInput(stateMachine.CurrentState, in playerInputs.ValueRO, cameraRotation, out characterControl.MoveVector);
                
                characterControl.JumpHeld = playerInputs.ValueRW.JumpHeld;
                characterControl.RollHeld = playerInputs.ValueRW.RollHeld;
                characterControl.SprintHeld = playerInputs.ValueRW.SprintHeld;

                characterControl.JumpPressed = playerInputs.ValueRW.JumpPressed.IsSet(fixedTick);
                characterControl.DashPressed = playerInputs.ValueRW.DashPressed.IsSet(fixedTick); 
                characterControl.CrouchPressed = playerInputs.ValueRW.CrouchPressed.IsSet(fixedTick); 
                characterControl.RopePressed = playerInputs.ValueRW.RopePressed.IsSet(fixedTick); 
                characterControl.ClimbPressed = playerInputs.ValueRW.ClimbPressed.IsSet(fixedTick); 
                characterControl.FlyNoCollisionsPressed = playerInputs.ValueRW.FlyNoCollisionsPressed.IsSet(fixedTick); 
                
                SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
            }
        }
    }
}