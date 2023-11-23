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

public static class WorldUtilities
{
    public static bool IsValidAndCreated(World world)
    {
        return world != null && world.IsCreated;
    }

    public static void CopyEntitiesToWorld(EntityManager srcEntityManager, EntityManager dstEntityManager, EntityQuery entityQuery)
    {
        NativeArray<Entity> entitiesToCopy = entityQuery.ToEntityArray(Allocator.Temp);
        dstEntityManager.CopyEntitiesFrom(srcEntityManager, entitiesToCopy);
        entitiesToCopy.Dispose();
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
public partial class MoveLocalEntitiesToClientServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Move entities to clients
        EntityQuery pendingMoveToClientQuery = SystemAPI.QueryBuilder().WithAll<MoveToClientWorld>().Build();
        if (pendingMoveToClientQuery.CalculateEntityCount() > 0)
        {
            // For each client world...
            World.NoAllocReadOnlyCollection<World> worlds =   World.All;
            for (int i = 0; i < worlds.Count; i++)
            {
                World tmpWorld = worlds[i];
                if (WorldUtilities.IsValidAndCreated(tmpWorld) && (tmpWorld.IsClient() || tmpWorld.IsThinClient()))
                {
                    WorldUtilities.CopyEntitiesToWorld(EntityManager, tmpWorld.EntityManager, pendingMoveToClientQuery);
                }
            }
            
            // Destroy entities in this world after copying them to all target worlds
            EntityManager.DestroyEntity(pendingMoveToClientQuery);
        }

        // Move entities to server
        EntityQuery pendingMoveToServerQuery = SystemAPI.QueryBuilder().WithAll<MoveToServerWorld>().Build();
        if (pendingMoveToServerQuery.CalculateEntityCount() > 0)
        {
            // For each server world...
            World.NoAllocReadOnlyCollection<World> worlds = World.All;
            for (int i = 0; i < worlds.Count; i++)
            {
                World tmpWorld = worlds[i];
                if (WorldUtilities.IsValidAndCreated(tmpWorld) && tmpWorld.IsServer())
                {
                    WorldUtilities.CopyEntitiesToWorld(EntityManager, tmpWorld.EntityManager, pendingMoveToServerQuery);
                }
            }
            
            // Destroy entities in this world after copying them to all target worlds
            EntityManager.DestroyEntity(pendingMoveToServerQuery);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial class MoveClientServerEntitiesToLocalSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityQuery pendingMoveToLocalQuery = SystemAPI.QueryBuilder().WithAll<MoveToLocalWorld>().Build();
        if (pendingMoveToLocalQuery.CalculateEntityCount() > 0)
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
                    WorldUtilities.CopyEntitiesToWorld(EntityManager, tmpWorld.EntityManager, pendingMoveToLocalQuery);
                }
            
                // Destroy entities in this world after copying them to all target worlds
                EntityManager.DestroyEntity(pendingMoveToLocalQuery);
            }
        }
    }
}