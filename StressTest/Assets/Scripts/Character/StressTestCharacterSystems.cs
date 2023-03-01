using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;

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
    public partial struct StressTestCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public StressTestCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref StressTestCharacterAspect characterAspect)
        {
            characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        { }
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
    public partial struct StressTestCharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public StressTestCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref StressTestCharacterAspect characterAspect)
        {
            characterAspect.VariableUpdate(ref Context, ref BaseContext);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        { }
    }
}
