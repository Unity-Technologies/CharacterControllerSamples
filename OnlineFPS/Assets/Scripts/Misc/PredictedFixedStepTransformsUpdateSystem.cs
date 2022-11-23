using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct PredictedFixedStepTransformsUpdateSystem : ISystem
{
    private SystemHandle _transformsGroupHandle;
    
    public void OnCreate(ref SystemState state)
    {
        _transformsGroupHandle = state.World.GetExistingSystem<TransformSystemGroup>();
    }

    public void OnDestroy(ref SystemState state)
    { }

    public void OnUpdate(ref SystemState state)
    {
        _transformsGroupHandle.Update(state.WorldUnmanaged);
    }
}
