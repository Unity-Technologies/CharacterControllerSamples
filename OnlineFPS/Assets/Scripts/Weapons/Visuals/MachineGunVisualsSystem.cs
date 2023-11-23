using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(WeaponShotVisualsGroup))]
[UpdateBefore(typeof(WeaponFiringMecanismVisualsSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[BurstCompile]
public partial struct MachineGunVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        MachineGunVisualsJob job = new MachineGunVisualsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct MachineGunVisualsJob : IJobEntity
    {
        public float DeltaTime;
        public ComponentLookup<LocalTransform> LocalTransformLookup;

        void Execute(Entity entity, ref MachineGunVisuals visuals, in WeaponShotVisuals weaponShotVisuals)
        {
            if (LocalTransformLookup.TryGetComponent(visuals.BarrelEntity, out LocalTransform localTransform))
            {
                if (weaponShotVisuals.LastTotalShotsCount < weaponShotVisuals.TotalShotsCount)
                {
                    visuals.CurrentSpinVelocity = visuals.SpinVelocity;
                }
                else
                {
                    visuals.CurrentSpinVelocity -= visuals.SpinVelocityDecay * DeltaTime;
                    visuals.CurrentSpinVelocity = math.clamp(visuals.CurrentSpinVelocity, 0f, float.MaxValue);
                }

                localTransform.Rotation = math.mul(localTransform.Rotation, quaternion.Euler(0f, 0f, visuals.CurrentSpinVelocity * DeltaTime));
                LocalTransformLookup[visuals.BarrelEntity] = localTransform;
            }
        }
    }
}
