using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PredictedFixedStepTransformsUpdateSystem))]
public class WeaponPredictionUpdateGroup : ComponentSystemGroup
{ }
