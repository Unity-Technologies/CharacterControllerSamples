using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class PlatformerUtilities
{
    public static void SetEntityHierarchyEnabled(bool enabled, Entity parent, EntityCommandBuffer commandBuffer, BufferLookup<LinkedEntityGroup> linkedEntityGroupFromEntity)
    {
        if (enabled)
        {
            commandBuffer.RemoveComponent<Disabled>(parent);
        }
        else
        {
            commandBuffer.AddComponent<Disabled>(parent);
        }

        if (linkedEntityGroupFromEntity.HasBuffer(parent))
        {
            DynamicBuffer<LinkedEntityGroup> parentLinkedEntities = linkedEntityGroupFromEntity[parent];
            for (int i = 0; i < parentLinkedEntities.Length; i++)
            {
                if (enabled)
                {
                    commandBuffer.RemoveComponent<Disabled>(parentLinkedEntities[i].Value);
                }
                else
                {
                    commandBuffer.AddComponent<Disabled>(parentLinkedEntities[i].Value);
                }
            }
        }
    }

    public static void SetEntityHierarchyEnabledParallel(bool enabled, Entity parent, EntityCommandBuffer.ParallelWriter ecb, int chunkIndex, BufferLookup<LinkedEntityGroup> linkedEntityGroupFromEntity)
    {
        if (enabled)
        {
            ecb.RemoveComponent<Disabled>(chunkIndex, parent);
        }
        else
        {
            ecb.AddComponent<Disabled>(chunkIndex, parent);
        }

        if (linkedEntityGroupFromEntity.HasBuffer(parent))
        {
            DynamicBuffer<LinkedEntityGroup> parentLinkedEntities = linkedEntityGroupFromEntity[parent];
            for (int i = 0; i < parentLinkedEntities.Length; i++)
            {
                if (enabled)
                {
                    ecb.RemoveComponent<Disabled>(chunkIndex, parentLinkedEntities[i].Value);
                }
                else
                {
                    ecb.AddComponent<Disabled>(chunkIndex, parentLinkedEntities[i].Value);
                }
            }
        }
    }
}