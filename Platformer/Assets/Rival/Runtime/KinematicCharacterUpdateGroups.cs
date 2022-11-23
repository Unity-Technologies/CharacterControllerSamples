using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Rival
{
    /// <summary>
    /// System group for the default character physics update
    /// </summary>
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public class KinematicCharacterPhysicsUpdateGroup : ComponentSystemGroup
    {
    }
    
    /// <summary>
    /// System group for the default character rotation update
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public class KinematicCharacterVariableUpdateGroup : ComponentSystemGroup
    {
    }
}