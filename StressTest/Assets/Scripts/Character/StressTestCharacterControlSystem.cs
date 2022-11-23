using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public partial struct StressTestCharacterControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<StressTestManagerSystem.Singleton>())
            return;

        bool multithreaded = SystemAPI.GetSingleton<StressTestManagerSystem.Singleton>().Multithreaded;
        
        float3 worldMoveVector = math.mul(quaternion.Euler(math.up() * (float)SystemAPI.Time.ElapsedTime), math.forward());
        StressTestCharacterControlJob job = new StressTestCharacterControlJob
        {
            WorldMoveVector = worldMoveVector,
        };
        if (multithreaded)
        {
            job.ScheduleParallel();
        }
        else
        {
            job.Schedule();
        }
    }

    [BurstCompile]
    public partial struct StressTestCharacterControlJob : IJobEntity
    {
        public float3 WorldMoveVector;
        
        void Execute(ref StressTestCharacterControl characterControl)
        {
            characterControl.MoveVector = WorldMoveVector;
        }
    }
}
