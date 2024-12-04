using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics.Systems;
using Unity.CharacterController;
using Unity.NetCode;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct FirstPersonPlayerInputsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>()
                .Build());
            state.RequireForUpdate<GameResources>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            FPSInputActions.DefaultMapActions defaultActionsMap = GameInput.InputActions.DefaultMap;

            foreach (var (playerCommands, player, entity) in SystemAPI
                         .Query<RefRW<FirstPersonPlayerCommands>, RefRW<FirstPersonPlayer>>()
                         .WithAll<GhostOwnerIsLocal>().WithEntityAccess())
            {
                // Toggle auto-movement (used for testing purposes)
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
                {
                    player.ValueRW.IsAutoMoving = !player.ValueRW.IsAutoMoving;
                }

                // Move
                if (player.ValueRW.IsAutoMoving)
                {
                    playerCommands.ValueRW.MoveInput =
                        UnityEngine.Vector2.ClampMagnitude(new float2(math.sin(elapsedTime), math.sin(elapsedTime)),
                            1f);
                }
                else
                {
                    playerCommands.ValueRW.MoveInput =
                        UnityEngine.Vector2.ClampMagnitude(defaultActionsMap.Move.ReadValue<UnityEngine.Vector2>(), 1f);
                }

                // Look 
                {
                    // Gamepad stick (constant) input
                    if (math.lengthsq(defaultActionsMap.LookConst.ReadValue<UnityEngine.Vector2>()) >
                        math.lengthsq(defaultActionsMap.LookDelta.ReadValue<UnityEngine.Vector2>()))
                    {
                        // Since the look input handling expects a "delta" rather than a constant value, we multiply stick input value by deltaTime
                        InputDeltaUtilities.AddInputDelta(ref playerCommands.ValueRW.LookYawPitchDegrees,
                            (float2)defaultActionsMap.LookConst.ReadValue<UnityEngine.Vector2>() * deltaTime *
                            GameSettings.LookSensitivity);
                    }
                    // Mouse (delta) input
                    else
                    {
                        InputDeltaUtilities.AddInputDelta(ref playerCommands.ValueRW.LookYawPitchDegrees,
                            (float2)defaultActionsMap.LookDelta.ReadValue<UnityEngine.Vector2>() *
                            GameSettings.LookSensitivity);
                    }
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
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct FirstPersonPlayerVariableStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>()
                .Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            FirstPersonPlayerVariableStepControlJob job = new FirstPersonPlayerVariableStepControlJob
            {
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
            public ComponentLookup<FirstPersonCharacterControl> CharacterControlLookup;
            [ReadOnly] public ComponentLookup<ActiveWeapon> ActiveWeaponlLookup;
            public ComponentLookup<WeaponControl> WeaponControlLookup;

            void Execute(ref FirstPersonPlayerCommands playerCommands,
                ref FirstPersonPlayerNetworkInput playerNetworkInput, in FirstPersonPlayer player,
                in CommandDataInterpolationDelay interpolationDelay)
            {
                // Compute a rotation delta from inputs, compared to last known value
                float2 lookYawPitchDegreesDelta = InputDeltaUtilities.GetInputDelta(
                    playerCommands.LookYawPitchDegrees,
                    playerNetworkInput.LastProcessedLookYawPitchDegrees);
                playerNetworkInput.LastProcessedLookYawPitchDegrees = playerCommands.LookYawPitchDegrees;

                // Character
                if (CharacterControlLookup.HasComponent(player.ControlledCharacter))
                {
                    FirstPersonCharacterControl characterControl = CharacterControlLookup[player.ControlledCharacter];

                    // Look 
                    characterControl.LookYawPitchDegreesDelta = lookYawPitchDegreesDelta;

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
                        weaponControl.ShootPressed = playerCommands.ShootPressed.IsSet;
                        weaponControl.ShootReleased = playerCommands.ShootReleased.IsSet;

                        // Aim
                        weaponControl.AimHeld = playerCommands.AimHeld;

                        weaponControl.InterpolationDelay = interpolationDelay.Delay;

                        WeaponControlLookup[activeWeapon.Entity] = weaponControl;
                    }
                }
            }
        }
    }

    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct FirstPersonPlayerFixedStepControlSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<FirstPersonPlayer, FirstPersonPlayerCommands>()
                .Build());
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
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            public ComponentLookup<FirstPersonCharacterControl> CharacterControlLookup;

            void Execute(in FirstPersonPlayerCommands playerCommands, in FirstPersonPlayer player)
            {
                // Character
                if (CharacterControlLookup.HasComponent(player.ControlledCharacter))
                {
                    FirstPersonCharacterControl characterControl = CharacterControlLookup[player.ControlledCharacter];

                    quaternion characterRotation = LocalTransformLookup[player.ControlledCharacter].Rotation;

                    // Move
                    float3 characterForward = math.mul(characterRotation, math.forward());
                    float3 characterRight = math.mul(characterRotation, math.right());
                    characterControl.MoveVector = (playerCommands.MoveInput.y * characterForward) +
                                                  (playerCommands.MoveInput.x * characterRight);
                    characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

                    // Jump
                    characterControl.Jump = playerCommands.JumpPressed.IsSet;

                    CharacterControlLookup[player.ControlledCharacter] = characterControl;
                }
            }
        }
    }
}
