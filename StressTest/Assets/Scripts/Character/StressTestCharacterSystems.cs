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
public partial struct StressTestCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private StressTestCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                StressTestCharacterComponent,
                StressTestCharacterControl>()
            .Build(ref state);

        _context = new StressTestCharacterUpdateContext();
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
        if (!SystemAPI.HasSingleton<StressTestManagerSystem.Singleton>())
            return;

        bool multithreaded = SystemAPI.GetSingleton<StressTestManagerSystem.Singleton>().Multithreaded;
        
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
        StressTestCharacterPhysicsUpdateJob job = new StressTestCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
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
    public partial struct StressTestCharacterPhysicsUpdateJob : IJobEntity
    {
        public StressTestCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref StressTestCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
        }
    }
}

[UpdateInGroup(typeof(KinematicCharacterVariableUpdateGroup))]
[BurstCompile]
public partial struct StressTestCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private StressTestCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                StressTestCharacterComponent,
                StressTestCharacterControl>()
            .Build(ref state);

        _context = new StressTestCharacterUpdateContext();
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
        if (!SystemAPI.HasSingleton<StressTestManagerSystem.Singleton>())
            return;

        bool multithreaded = SystemAPI.GetSingleton<StressTestManagerSystem.Singleton>().Multithreaded;
        
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
        StressTestCharacterVariableUpdateJob job = new StressTestCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
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
    public partial struct StressTestCharacterVariableUpdateJob : IJobEntity
    {
        public StressTestCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref StressTestCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.VariableUpdate(ref Context, ref BaseContext);
        }
    }
}
