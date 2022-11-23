using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Rival;

[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct BasicCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private BasicCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                BasicCharacterComponent,
                BasicCharacterControl>()
            .Build(ref state);

        _context = new BasicCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
        BasicCharacterPhysicsUpdateJob job = new BasicCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct BasicCharacterPhysicsUpdateJob : IJobEntity
    {
        public BasicCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref BasicCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
        }
    }
}

[UpdateInGroup(typeof(KinematicCharacterVariableUpdateGroup))]
[BurstCompile]
public partial struct BasicCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private BasicCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                BasicCharacterComponent,
                BasicCharacterControl>()
            .Build(ref state);

        _context = new BasicCharacterUpdateContext();
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
        
        BasicCharacterVariableUpdateJob job = new BasicCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct BasicCharacterVariableUpdateJob : IJobEntity
    {
        public BasicCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref BasicCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.VariableUpdate(ref Context, ref BaseContext);
        }
    }
}
