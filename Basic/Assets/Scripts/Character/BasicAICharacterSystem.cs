using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct BasicAICharacterSystem : ISystem
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
        float time = (float)SystemAPI.Time.ElapsedTime;

        BasicAICharacterJob job = new BasicAICharacterJob
        {
            Time = time,
        };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct BasicAICharacterJob : IJobEntity
    {
        public float Time;

        void Execute(ref BasicAICharacter aiCharacter, ref BasicCharacterControl characterInputs)
        {
            characterInputs.MoveVector = math.sin(Time * aiCharacter.MovementPeriod) * aiCharacter.MovementDirection;
        }
    }
}