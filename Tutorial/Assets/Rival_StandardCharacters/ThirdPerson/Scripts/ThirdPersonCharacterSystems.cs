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
public partial struct ThirdPersonCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private ThirdPersonCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                ThirdPersonCharacterComponent,
                ThirdPersonCharacterControl>()
            .Build(ref state);

        _context = new ThirdPersonCharacterUpdateContext();
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
        
        ThirdPersonCharacterPhysicsUpdateJob job = new ThirdPersonCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ThirdPersonCharacterPhysicsUpdateJob : IJobEntity
    {
        public ThirdPersonCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref ThirdPersonCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
        }
    }
}

[UpdateInGroup(typeof(KinematicCharacterVariableUpdateGroup))]
[BurstCompile]
public partial struct ThirdPersonCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private ThirdPersonCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                ThirdPersonCharacterComponent,
                ThirdPersonCharacterControl>()
            .Build(ref state);

        _context = new ThirdPersonCharacterUpdateContext();
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
        
        ThirdPersonCharacterVariableUpdateJob job = new ThirdPersonCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ThirdPersonCharacterVariableUpdateJob : IJobEntity
    {
        public ThirdPersonCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute(ref ThirdPersonCharacterAspect characterAspect)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            characterAspect.VariableUpdate(ref Context, ref BaseContext);
        }
    }
}
