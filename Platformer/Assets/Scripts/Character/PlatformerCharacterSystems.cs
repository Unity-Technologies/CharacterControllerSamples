using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[RequireMatchingQueriesForUpdate]
[BurstCompile]
public partial struct PlatformerCharacterInitializationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    { }

    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
        BufferLookup<LinkedEntityGroup> linkedEntitiesLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

        foreach (var (character, stateMachine, entity) in SystemAPI.Query<RefRW<PlatformerCharacterComponent>, RefRW<PlatformerCharacterStateMachine>>().WithNone<PlatformerCharacterInitialized>().WithEntityAccess())
        {
            // Make sure the transform system has done a pass on it first
            if (linkedEntitiesLookup.HasBuffer(entity))
            {
                // Disable alternative meshes
                PlatformerUtilities.SetEntityHierarchyEnabled(false, character.ValueRO.RollballMeshEntity, ecb, linkedEntitiesLookup);
                ecb.AddComponent<PlatformerCharacterInitialized>(entity);
            }
        }
    }
}

[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct PlatformerCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private PlatformerCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                PlatformerCharacterComponent,
                PlatformerCharacterControl,
                PlatformerCharacterStateMachine>()
            .Build(ref state);

        _context = new PlatformerCharacterUpdateContext();
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
        _context.OnSystemUpdate(ref state, SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged));
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
        PlatformerCharacterPhysicsUpdateJob job = new PlatformerCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlatformerCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public PlatformerCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute([ChunkIndexInQuery] int chunkIndex, PlatformerCharacterAspect characterAspect)
        {
            Context.SetChunkIndex(chunkIndex);
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

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct PlatformerCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private PlatformerCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<
                PlatformerCharacterComponent,
                PlatformerCharacterControl>()
            .Build(ref state);

        _context = new PlatformerCharacterUpdateContext();
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
        _context.OnSystemUpdate(ref state, SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged));
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
        PlatformerCharacterVariableUpdateJob job = new PlatformerCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlatformerCharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public PlatformerCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;
    
        void Execute([ChunkIndexInQuery] int chunkIndex, PlatformerCharacterAspect characterAspect)
        {
            Context.SetChunkIndex(chunkIndex);
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
