using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public struct WorldTransformsHelper
{
    public ComponentLookup<LocalTransform> LocalTransformLookup;
    [ReadOnly]
    public ComponentLookup<Parent> ParentLookup;

    public WorldTransformsHelper(ref SystemState state)
    {
        LocalTransformLookup = state.GetComponentLookup<LocalTransform>(false);
        ParentLookup = state.GetComponentLookup<Parent>(true);
    }

    public bool HasLocalTransform(Entity entity)
    {
        return LocalTransformLookup.HasComponent(entity);
    }

    public bool HasParent(Entity entity)
    {
        return ParentLookup.HasComponent(entity);
    }
    
    public bool TrySetToWorldTransformUnscaled(Entity entity, RigidTransform targetWorldTransform)
    {
        Entity queriedEntity = entity;
        RigidTransform hierarchyTransform = RigidTransform.identity;

        if (LocalTransformLookup.HasComponent(entity))
        {
            while (ParentLookup.TryGetComponent(queriedEntity, out Parent parent))
            {
                queriedEntity = parent.Value;
                if (LocalTransformLookup.TryGetComponent(queriedEntity, out LocalTransform parentLocalTransform))
                {
                    hierarchyTransform = math.mul(parentLocalTransform.ToRigidTransform(), hierarchyTransform);
                }
            }

            LocalTransformLookup[entity] = math.mul(math.inverse(hierarchyTransform), targetWorldTransform).ToLocalTransform();
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool TryGetWorldTransformUnscaled(Entity entity, out RigidTransform result)
    {
        Entity queriedEntity = entity;
        result = RigidTransform.identity;
        
        if (LocalTransformLookup.TryGetComponent(entity, out LocalTransform localTransform))
        {
            result = localTransform.ToRigidTransform();

            while (ParentLookup.TryGetComponent(queriedEntity, out Parent parent))
            {
                queriedEntity = parent.Value;
                if (LocalTransformLookup.TryGetComponent(queriedEntity, out LocalTransform parentLocalTransform))
                {
                    result = math.mul(parentLocalTransform.ToRigidTransform(), result);
                }
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    public void LookAt(Entity lookingEntity, Entity lookAtEntity, float3 upDirection, out float3 worldPositionDelta)
    {
        worldPositionDelta = default;
        
        if (TryGetWorldTransformUnscaled(lookingEntity, out RigidTransform lookingEntityWorldTransform) && TryGetWorldTransformUnscaled(lookAtEntity, out RigidTransform lookAtEntityWorldTransform))
        {
            worldPositionDelta = lookAtEntityWorldTransform.pos - lookingEntityWorldTransform.pos;
            lookingEntityWorldTransform.rot = quaternion.LookRotationSafe(math.normalizesafe(worldPositionDelta), upDirection);
            TrySetToWorldTransformUnscaled(lookingEntity, lookingEntityWorldTransform);
        }
    }

    public void LookAt(Entity lookingEntity, float3 lookAtPos, float3 upDirection, out float3 worldPositionDelta)
    {
        worldPositionDelta = default;

        if (TryGetWorldTransformUnscaled(lookingEntity, out RigidTransform lookingEntityWorldTransform))
        {
            worldPositionDelta = lookAtPos - lookingEntityWorldTransform.pos;
            lookingEntityWorldTransform.rot = quaternion.LookRotationSafe(math.normalizesafe(worldPositionDelta), upDirection);
            TrySetToWorldTransformUnscaled(lookingEntity, lookingEntityWorldTransform);
        }
    }
}

[BurstCompile]
public struct WorldTransformsHelperReadOnly
{
    [ReadOnly]
    public ComponentLookup<LocalTransform> LocalTransformLookup;
    [ReadOnly]
    public ComponentLookup<Parent> ParentLookup;

    public WorldTransformsHelperReadOnly(ref SystemState state)
    {
        LocalTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        ParentLookup = state.GetComponentLookup<Parent>(true);
    }

    public bool HasLocalTransform(Entity entity)
    {
        return LocalTransformLookup.HasComponent(entity);
    }

    public bool HasParent(Entity entity)
    {
        return ParentLookup.HasComponent(entity);
    }

    public bool TryGetWorldTransformUnscaled(Entity entity, out RigidTransform result)
    {
        Entity queriedEntity = entity;
        result = RigidTransform.identity;
        
        if (LocalTransformLookup.TryGetComponent(entity, out LocalTransform localTransform))
        {
            result = localTransform.ToRigidTransform();

            while (ParentLookup.TryGetComponent(queriedEntity, out Parent parent))
            {
                queriedEntity = parent.Value;
                if (LocalTransformLookup.TryGetComponent(queriedEntity, out LocalTransform parentLocalTransform))
                {
                    result = math.mul(parentLocalTransform.ToRigidTransform(), result);
                }
            }
        }
        else
        {
            return false;
        }

        return true;
    }
}

public static class TransformUtilities
{
    public static LocalTransform ToLocalTransform(this RigidTransform rigidTransform, float scale = 1f)
    {
        return new LocalTransform { Position = rigidTransform.pos, Rotation = rigidTransform.rot, Scale = scale };
    }
    
    public static RigidTransform ToRigidTransform(this LocalTransform localTransform)
    {
        return new RigidTransform(localTransform.Rotation, localTransform.Position);
    }
    
    public static float3 Forward(this RigidTransform rigidTransform)
    {
        return math.mul(rigidTransform.rot, math.forward());
    }
    
    public static float3 Right(this RigidTransform rigidTransform)
    {
        return math.mul(rigidTransform.rot, math.right());
    }
    
    public static float3 Up(this RigidTransform rigidTransform)
    {
        return math.mul(rigidTransform.rot, math.up());
    }
}
