using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct MachineGunVisualsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    { }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        MachineGunVisualsJob job = new MachineGunVisualsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct MachineGunVisualsJob : IJobEntity
    {
        public float DeltaTime;
        public ComponentLookup<LocalTransform> LocalTransformLookup;

        void Execute(Entity entity, ref MachineGunVisuals visuals, in StandardWeaponFiringMecanism firingMecanism)
        {
            if (LocalTransformLookup.TryGetComponent(visuals.BarrelEntity, out LocalTransform localTransform))
            {
                if (firingMecanism.ShotsToFire > 0)
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
