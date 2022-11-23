using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Rival
{
    /// <summary>
    /// Handles tracking previous and current transforms of an entity at a fixed timestep
    /// </summary>
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct TrackedTransformFixedSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrackedTransform>(); 
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            TrackedTransformFixedSimulationJob job = new TrackedTransformFixedSimulationJob();
            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct TrackedTransformFixedSimulationJob : IJobEntity
    {
        void Execute(ref TrackedTransform trackedTransform, in LocalTransform transform)
        {
            trackedTransform.PreviousFixedRateTransform = trackedTransform.CurrentFixedRateTransform;
            trackedTransform.CurrentFixedRateTransform = new RigidTransform(transform.Rotation, transform.Position);
        }
    }
}