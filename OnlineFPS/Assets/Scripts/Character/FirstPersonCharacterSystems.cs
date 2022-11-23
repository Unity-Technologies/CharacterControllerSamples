using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Rival;
using Unity.NetCode;
using UnityEngine;

[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[BurstCompile]
public partial struct BuildCharacterRotationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<LocalTransform, FirstPersonCharacterComponent>().Build());
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        BuildCharacterRotationJob job = new BuildCharacterRotationJob
        { };
        job.Schedule();
    }

    [BurstCompile]
    public partial struct BuildCharacterRotationJob : IJobEntity
    {
        void Execute(ref LocalTransform localTransform, in FirstPersonCharacterComponent characterComponent)
        {
            FirstPersonCharacterUtilities.ComputeRotationFromYAngleAndUp(characterComponent.CharacterYDegrees, math.up(), out quaternion tmpRotation);
            localTransform.Rotation = tmpRotation;
        }
    }
}

[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct FirstPersonCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private FirstPersonCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                FirstPersonCharacterComponent,
                FirstPersonCharacterControl>()
            .Build(ref state);
        
        _context = new FirstPersonCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
        state.RequireForUpdate<NetworkTime>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<NetworkTime>())
            return;
        
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

        FirstPersonCharacterPhysicsUpdateJob job = new FirstPersonCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonCharacterPhysicsUpdateJob : IJobEntity
    {
        public FirstPersonCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref FirstPersonCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
        }
    }
}

[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(PredictedFixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct FirstPersonCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private FirstPersonCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                FirstPersonCharacterComponent,
                FirstPersonCharacterControl>()
            .Build(ref state);
        
        _context = new FirstPersonCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
        FirstPersonCharacterVariableUpdateJob variableUpdateJob = new FirstPersonCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        variableUpdateJob.ScheduleParallel();
        
        FirstPersonCharacterViewJob viewJob = new FirstPersonCharacterViewJob
        {
            FirstPersonCharacterLookup = SystemAPI.GetComponentLookup<FirstPersonCharacterComponent>(true),
        };
        viewJob.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonCharacterVariableUpdateJob : IJobEntity
    {
        public FirstPersonCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref FirstPersonCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.VariableUpdate(ref Context, ref BaseContext);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct FirstPersonCharacterViewJob : IJobEntity
    {
        [ReadOnly] 
        public ComponentLookup<FirstPersonCharacterComponent> FirstPersonCharacterLookup;

        void Execute(ref TransformAspect transformAspect, in FirstPersonCharacterView characterView)
        {
            if (FirstPersonCharacterLookup.TryGetComponent(characterView.CharacterEntity, out FirstPersonCharacterComponent character))
            {
                transformAspect.LocalRotation = character.ViewLocalRotation;
            }
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct CharacterVisualsSystem : ISystem
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
        CharacterClientCleanupJob clientCleanupJob = new CharacterClientCleanupJob
        {
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
        };
        clientCleanupJob.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct CharacterClientCleanupJob : IJobEntity
    {
        [ReadOnly] 
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;

        void Execute(ref CharacterCleanupClient cleanup, in FirstPersonCharacterComponent character)
        {
            if (LocalToWorldLookup.TryGetComponent(character.DeathVFXSpawnPoint, out LocalToWorld deathVFXSpawnLtW))
            {
                cleanup.DeathVFXSpawnWorldPos = deathVFXSpawnLtW.Position;
            }
        }
    }
}