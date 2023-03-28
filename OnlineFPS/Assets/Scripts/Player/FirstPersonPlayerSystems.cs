using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics.Systems;
using Unity.CharacterController;
using Unity.NetCode;

[UpdateInGroup(typeof(GhostInputSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial class FirstPersonPlayerInputsSystem : SystemBase
{
    private FPSInputActions InputActions;
    
    protected override void OnCreate()
    {
        RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
        RequireForUpdate<GameResources>();   
        RequireForUpdate<NetworkTime>();   
        RequireForUpdate<NetworkId>();   

        // Create the input user
        InputActions = new FPSInputActions();
        InputActions.Enable();
        InputActions.DefaultMap.Enable();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        NetworkTick tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
        FPSInputActions.DefaultMapActions defaultActionsMap = InputActions.DefaultMap;

        foreach (var (playerCommands, player, ghostOwner, entity) in SystemAPI.Query<RefRW<FirstPersonPlayerCommands>, RefRW<FirstPersonPlayer>, GhostOwner>().WithAll<GhostOwnerIsLocal>().WithEntityAccess())
        {
            // Remember if new tick because some inputs need to be reset when we just started a new tick
            bool isOnNewTick = !player.ValueRW.LastKnownCommandsTick.IsValid || tick.IsNewerThan(player.ValueRW.LastKnownCommandsTick);

            playerCommands.ValueRW = default;

            // Toggle auto-movement (used for testing purposes)
            if (Input.GetKeyDown(KeyCode.F8))
            {
                player.ValueRW.IsAutoMoving = !player.ValueRW.IsAutoMoving;
            }
            
            // Move
            if (player.ValueRW.IsAutoMoving)
            {
                playerCommands.ValueRW.MoveInput =Vector2.ClampMagnitude(new float2(math.sin(elapsedTime), math.sin(elapsedTime)), 1f);
            }
            else
            {
                playerCommands.ValueRW.MoveInput = Vector2.ClampMagnitude(defaultActionsMap.Move.ReadValue<Vector2>(), 1f);
            }

            // Look input must be accumulated on each update belonging to the same tick, because it is a delta and will be processed at a variable update
            if (!isOnNewTick)
            {
                playerCommands.ValueRW.LookInputDelta = player.ValueRW.LastKnownCommands.LookInputDelta;
            }
            if (math.lengthsq(defaultActionsMap.LookConst.ReadValue<Vector2>()) > math.lengthsq(defaultActionsMap.LookDelta.ReadValue<Vector2>()))
            {
                // Gamepad look with a constant stick value
                playerCommands.ValueRW.LookInputDelta += (float2)(defaultActionsMap.LookConst.ReadValue<Vector2>() * GameSettings.LookSensitivity * deltaTime);
            }
            else
            {
                // Mouse look with a mouse move delta value
                playerCommands.ValueRW.LookInputDelta += (float2)(defaultActionsMap.LookDelta.ReadValue<Vector2>() * GameSettings.LookSensitivity);
            }

            // Jump
            if (defaultActionsMap.Jump.WasPressedThisFrame())
            {
                playerCommands.ValueRW.JumpPressed.Set();
            }
            
            // Shoot
            if (defaultActionsMap.Shoot.WasPressedThisFrame())
            {
                playerCommands.ValueRW.ShootPressed.Set();
            }
            if (defaultActionsMap.Shoot.WasReleasedThisFrame())
            {
                playerCommands.ValueRW.ShootReleased.Set();
            }

            // Aim
            playerCommands.ValueRW.AimHeld = defaultActionsMap.Aim.IsPressed();
            
            player.ValueRW.LastKnownCommandsTick = tick;
            player.ValueRW.LastKnownCommands = playerCommands.ValueRW;
        }
    }
}

[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(FirstPersonCharacterVariableUpdateSystem))]
[UpdateAfter(typeof(BuildCharacterRotationSystem))]
[BurstCompile]
public partial struct FirstPersonPlayerVariableStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerCommands, player) in SystemAPI.Query<FirstPersonPlayerCommands, FirstPersonPlayer>().WithAll<Simulate>())
        {
            if (SystemAPI.HasComponent<FirstPersonCharacterControl>(player.ControlledCharacter))
            {
                FirstPersonCharacterControl characterControl = SystemAPI.GetComponent<FirstPersonCharacterControl>(player.ControlledCharacter);
            
                // Look
                characterControl.LookYawPitchDegrees = playerCommands.LookInputDelta;
        
                SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
            }
        }
    }
}

[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct FirstPersonPlayerFixedStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerCommands, player, commandInterpolationDelay, entity) in SystemAPI.Query<FirstPersonPlayerCommands, FirstPersonPlayer, CommandDataInterpolationDelay>().WithAll<Simulate>().WithEntityAccess())
        {
            // Character
            if (SystemAPI.HasComponent<FirstPersonCharacterControl>(player.ControlledCharacter))
            {
                FirstPersonCharacterControl characterControl = SystemAPI.GetComponent<FirstPersonCharacterControl>(player.ControlledCharacter);

                quaternion characterRotation = SystemAPI.GetComponent<LocalTransform>(player.ControlledCharacter).Rotation;

                // Move
                float3 characterForward = math.mul(characterRotation, math.forward());
                float3 characterRight = math.mul(characterRotation, math.right());
                characterControl.MoveVector = (playerCommands.MoveInput.y * characterForward) + (playerCommands.MoveInput.x * characterRight);
                characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

                // Jump
                characterControl.Jump = playerCommands.JumpPressed.IsSet;

                SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
            }

            // Weapon
            if (SystemAPI.HasComponent<ActiveWeapon>(player.ControlledCharacter))
            {
                ActiveWeapon activeWeapon = SystemAPI.GetComponent<ActiveWeapon>(player.ControlledCharacter);
                if (SystemAPI.HasComponent<WeaponControl>(activeWeapon.Entity))
                {
                    WeaponControl weaponControl = SystemAPI.GetComponent<WeaponControl>(activeWeapon.Entity);
                    InterpolationDelay interpolationDelay = SystemAPI.GetComponent<InterpolationDelay>(activeWeapon.Entity);

                    // Shoot
                    weaponControl.FirePressed = playerCommands.ShootPressed.IsSet;
                    weaponControl.FireReleased = playerCommands.ShootReleased.IsSet;

                    // Aim
                    weaponControl.AimHeld = playerCommands.AimHeld;

                    // Interp delay
                    interpolationDelay.Value = commandInterpolationDelay.Delay;

                    SystemAPI.SetComponent(activeWeapon.Entity, weaponControl);
                    SystemAPI.SetComponent(activeWeapon.Entity, interpolationDelay);
                }
            }
        }
    }
}