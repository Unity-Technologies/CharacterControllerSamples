using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
[BurstCompile]
public partial struct FixedTickSystem : ISystem
{
    public struct Singleton : IComponentData
    {
        public uint Tick;
    }

    public void OnCreate(ref SystemState state)
    { }

    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<Singleton>())
        { 
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton());
        }

        ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
        singleton.Tick++;
    } 
}
