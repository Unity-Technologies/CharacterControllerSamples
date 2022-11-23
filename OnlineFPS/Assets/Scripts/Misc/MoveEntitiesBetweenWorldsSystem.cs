using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct MoveToClientWorld : IComponentData
{ }

public struct MoveToServerWorld : IComponentData
{ }

public struct MoveToLocalWorld : IComponentData
{ }

public struct EntityPendingMove : IComponentData
{ }

public static class WorldUtilities
{
    public static bool IsValidAndCreated(World world)
    {
        return world != null && world.IsCreated;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
public partial class MoveLocalEntitiesToClientServerSystem : SystemBase
{
    private List<World> ClientWorlds;
    private World ServerWorld;

    protected override void OnCreate()
    {
        base.OnCreate();

        ClientWorlds = new List<World>();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(World.Unmanaged);
        
        // Find client worlds
        World.NoAllocReadOnlyCollection<World> worlds = World.All;
        for (int i = 0; i < worlds.Count; i++)
        {
            World tmpWorld = worlds[i];
            if (WorldUtilities.IsValidAndCreated(tmpWorld) && 
                (tmpWorld.IsClient() || tmpWorld.IsThinClient()))
            {
                if (!ClientWorlds.Contains(tmpWorld))
                {
                    ClientWorlds.Add(tmpWorld);
                }
            }
        }
        
        // Find server world
        if (!WorldUtilities.IsValidAndCreated(ServerWorld))
        {
            for (int i = 0; i < worlds.Count; i++)
            {
                World tmpWorld = worlds[i];
                if (WorldUtilities.IsValidAndCreated(tmpWorld) && tmpWorld.IsServer())
                {
                    ServerWorld = tmpWorld;
                    break;
                }
            }
        }
        
        // Move entities to clients
        for (int i = ClientWorlds.Count - 1; i >= 0; i--)
        {
            World clientWorld = ClientWorlds[i];
            if (WorldUtilities.IsValidAndCreated(clientWorld))
            {
                EntityQuery pendingMoveQuery = SystemAPI.QueryBuilder().WithAll<MoveToClientWorld, EntityPendingMove>().Build();
                NativeArray<Entity> moveEntities = SystemAPI.QueryBuilder().WithAll<MoveToClientWorld>().Build().ToEntityArray(Allocator.Temp);
                for (int j = 0; j < moveEntities.Length; j++)
                {
                    Entity original = moveEntities[j];
                    Entity copy = EntityManager.Instantiate(original);
                    EntityManager.AddComponentData(copy, new EntityPendingMove());
                
                    ecb.DestroyEntity(original);
                }
                moveEntities.Dispose();
                
                clientWorld.EntityManager.MoveEntitiesFrom(EntityManager, pendingMoveQuery);
                EntityManager.DestroyEntity(pendingMoveQuery);
            }
            else
            {
                ClientWorlds.RemoveAt(i);
            }
        }
        
        // Move entities to server
        if (WorldUtilities.IsValidAndCreated(ServerWorld))
        {
            EntityQuery pendingMoveQuery = SystemAPI.QueryBuilder().WithAll<MoveToServerWorld, EntityPendingMove>().Build();
            NativeArray<Entity> moveEntities = SystemAPI.QueryBuilder().WithAll<MoveToServerWorld>().Build().ToEntityArray(Allocator.Temp);
            for (int i = 0; i < moveEntities.Length; i++)
            {
                Entity original = moveEntities[i];
                Entity copy = EntityManager.Instantiate(original);
                EntityManager.AddComponentData(copy, new EntityPendingMove());
                
                ecb.DestroyEntity(original);
            }
            moveEntities.Dispose();
                
            ServerWorld.EntityManager.MoveEntitiesFrom(EntityManager, pendingMoveQuery);
            EntityManager.DestroyEntity(pendingMoveQuery);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial class MoveClientServerEntitiesToLocalSystem : SystemBase
{
    private World LocalWorld;
    
    protected override void OnUpdate()
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(World.Unmanaged);
        
        if (!WorldUtilities.IsValidAndCreated(LocalWorld))
        {
            World.NoAllocReadOnlyCollection<World> worlds = World.All;
            for (int i = 0; i < worlds.Count; i++)
            {
                World tmpWorld = worlds[i];
                if (WorldUtilities.IsValidAndCreated(tmpWorld) && 
                     !(tmpWorld.IsClient() || tmpWorld.IsThinClient()) &&
                     !tmpWorld.IsServer() &&
                     tmpWorld.GetExistingSystemManaged<GameManagementSystem>() != null)
                {
                    LocalWorld = tmpWorld;
                    break;
                }
            }
        }
        
        // Move entities
        if (WorldUtilities.IsValidAndCreated(LocalWorld))
        {
            EntityQuery pendingMoveQuery = SystemAPI.QueryBuilder().WithAll<MoveToLocalWorld, EntityPendingMove>().Build();
            NativeArray<Entity> moveEntities = SystemAPI.QueryBuilder().WithAll<MoveToLocalWorld>().Build().ToEntityArray(Allocator.Temp);
            for (int i = 0; i < moveEntities.Length; i++)
            {
                Entity original = moveEntities[i];
                Entity copy = EntityManager.Instantiate(original);
                EntityManager.AddComponentData(copy, new EntityPendingMove());
                
                ecb.DestroyEntity(original);
            }
            moveEntities.Dispose();
                
            LocalWorld.EntityManager.MoveEntitiesFrom(EntityManager, pendingMoveQuery);
            EntityManager.DestroyEntity(pendingMoveQuery);
        }
    }
}