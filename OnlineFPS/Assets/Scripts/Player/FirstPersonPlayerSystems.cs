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
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class FirstPersonPlayerInputsSystem : SystemBase
{
    private FPSInputActions InputActions;
    
    protected override void OnCreate()
    {
        RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
        RequireForUpdate<GameResources>();   
        RequireForUpdate<NetworkTime>();   

        // Create the input user
        InputActions = new FPSInputActions();
        InputActions.Enable();
        InputActions.DefaultMap.Enable();
    }

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
        FPSInputActions.DefaultMapActions defaultActionsMap = InputActions.DefaultMap;

        foreach (var (playerCommands, player, entity) in SystemAPI.Query<RefRW<FirstPersonPlayerCommands>, RefRW<FirstPersonPlayer>>().WithAll<GhostOwnerIsLocal>().WithEntityAccess())
        {
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

            // Look 
            if (math.lengthsq(defaultActionsMap.LookConst.ReadValue<Vector2>()) > math.lengthsq(defaultActionsMap.LookDelta.ReadValue<Vector2>()))
            {
                // Since the look input handling expects a "delta" rather than a constant value, we multiply stick input value by deltaTime
                float2 stickLookInputDelta = defaultActionsMap.LookConst.ReadValue<Vector2>() * deltaTime * GameSettings.LookSensitivity;
                NetworkInputUtilities.AddInputDelta(ref playerCommands.ValueRW.LookInputDelta.x, stickLookInputDelta.x);
                NetworkInputUtilities.AddInputDelta(ref playerCommands.ValueRW.LookInputDelta.y, stickLookInputDelta.y);
            }
            else
            {
                float2 mouseLookInputDelta = defaultActionsMap.LookDelta.ReadValue<Vector2>() * GameSettings.LookSensitivity;
                NetworkInputUtilities.AddInputDelta(ref playerCommands.ValueRW.LookInputDelta.x, mouseLookInputDelta.x);
                NetworkInputUtilities.AddInputDelta(ref playerCommands.ValueRW.LookInputDelta.y, mouseLookInputDelta.y);
            }

            // Jump
            playerCommands.ValueRW.JumpPressed = default;
            if (defaultActionsMap.Jump.WasPressedThisFrame())
            {
                playerCommands.ValueRW.JumpPressed.Set();
            }
            
            // Shoot
            playerCommands.ValueRW.ShootPressed = default;
            if (defaultActionsMap.Shoot.WasPressedThisFrame())
            {
                playerCommands.ValueRW.ShootPressed.Set();
            }
            playerCommands.ValueRW.ShootReleased = default;
            if (defaultActionsMap.Shoot.WasReleasedThisFrame())
            {
                playerCommands.ValueRW.ShootReleased.Set();
            }

            // Aim
            playerCommands.ValueRW.AimHeld = defaultActionsMap.Aim.IsPressed();
        }
    }
}

[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(WeaponPredictionUpdateGroup))]
[UpdateBefore(typeof(FirstPersonCharacterVariableUpdateSystem))]
[UpdateAfter(typeof(BuildCharacterPredictedRotationSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[BurstCompile]
public partial struct FirstPersonPlayerVariableStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkTime>();   
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        NetworkInputUtilities.GetCurrentAndPreviousTick(SystemAPI.GetSingleton<NetworkTime>(), out NetworkTick currentTick, out NetworkTick previousTick);

        FirstPersonPlayerVariableStepControlJob job = new FirstPersonPlayerVariableStepControlJob
        {
            CurrentTick = currentTick,
            PreviousTick = previousTick,
            InterpolationDelayLookup = SystemAPI.GetComponentLookup<InterpolationDelay>(false),
            CharacterControlLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterControl>(false),
            ActiveWeaponlLookup = SystemAPI.GetComponentLookup<ActiveWeapon>(true),
            WeaponControlLookup = SystemAPI.GetComponentLookup<WeaponControl>(false),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonPlayerVariableStepControlJob : IJobEntity
    {
        public NetworkTick CurrentTick;
        public NetworkTick PreviousTick;
        public ComponentLookup<InterpolationDelay> InterpolationDelayLookup;
        public ComponentLookup<FirstPersonCharacterControl> CharacterControlLookup;
        [ReadOnly]
        public ComponentLookup<ActiveWeapon> ActiveWeaponlLookup;
        public ComponentLookup<WeaponControl> WeaponControlLookup;

        void Execute(in DynamicBuffer<InputBufferData<FirstPersonPlayerCommands>> playerCommandsBuffer, in FirstPersonPlayerCommands playerCommands, in FirstPersonPlayer player, in CommandDataInterpolationDelay commandInterpolationDelay)
        {
            NetworkInputUtilities.GetCurrentAndPreviousTickInputs(playerCommandsBuffer, CurrentTick, PreviousTick, out FirstPersonPlayerCommands currentTickInputs, out FirstPersonPlayerCommands previousTickInputs);

            // Character
            if (CharacterControlLookup.HasComponent(player.ControlledCharacter))
            {
                FirstPersonCharacterControl characterControl = CharacterControlLookup[player.ControlledCharacter];
            
                // Look
                characterControl.LookYawPitchDegrees.x = NetworkInputUtilities.GetInputDelta(currentTickInputs.LookInputDelta.x, previousTickInputs.LookInputDelta.x);
                characterControl.LookYawPitchDegrees.y = NetworkInputUtilities.GetInputDelta(currentTickInputs.LookInputDelta.y, previousTickInputs.LookInputDelta.y);
        
                CharacterControlLookup[player.ControlledCharacter] = characterControl;
            }

            // Weapon
            if (ActiveWeaponlLookup.HasComponent(player.ControlledCharacter))
            {
                ActiveWeapon activeWeapon = ActiveWeaponlLookup[player.ControlledCharacter];
                if (WeaponControlLookup.HasComponent(activeWeapon.Entity))
                {
                    WeaponControl weaponControl = WeaponControlLookup[activeWeapon.Entity];
            
                    // Shoot
                    // NOTE: very inportant to get the IsSet from the actual IInputComponentData rather than from the buffer, because the InputEvents in buffer aren't handled by the codegen systems
                    weaponControl.ShootPressed = playerCommands.ShootPressed.IsSet;
                    weaponControl.ShootReleased = playerCommands.ShootReleased.IsSet;
            
                    // Aim
                    weaponControl.AimHeld = playerCommands.AimHeld;
            
                    WeaponControlLookup[activeWeapon.Entity] = weaponControl;
                    InterpolationDelayLookup[activeWeapon.Entity] = new InterpolationDelay { Value = commandInterpolationDelay.Delay };
                }
            }
        }
    }
}

[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[BurstCompile]
public partial struct FirstPersonPlayerFixedStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkTime>();   
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FirstPersonPlayerFixedStepControlJob job = new FirstPersonPlayerFixedStepControlJob
        {
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            CharacterControlLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterControl>(false),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonPlayerFixedStepControlJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        public ComponentLookup<FirstPersonCharacterControl> CharacterControlLookup;

        void Execute(in FirstPersonPlayerCommands playerCommands, in FirstPersonPlayer player, in CommandDataInterpolationDelay commandInterpolationDelay)
        {
            // Character
            if (CharacterControlLookup.HasComponent(player.ControlledCharacter))
            {
                FirstPersonCharacterControl characterControl = CharacterControlLookup[player.ControlledCharacter];

                quaternion characterRotation = LocalTransformLookup[player.ControlledCharacter].Rotation;

                // Move
                float3 characterForward = math.mul(characterRotation, math.forward());
                float3 characterRight = math.mul(characterRotation, math.right());
                characterControl.MoveVector = (playerCommands.MoveInput.y * characterForward) + (playerCommands.MoveInput.x * characterRight);
                characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

                // Jump
                characterControl.Jump = playerCommands.JumpPressed.IsSet;
                
                CharacterControlLookup[player.ControlledCharacter] = characterControl;
            }
        }
    }
}