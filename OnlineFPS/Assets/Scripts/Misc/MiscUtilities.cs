using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.InputSystem;

namespace OnlineFPS
{
    public struct SwitchBool
    {
        private bool Current;
        private bool Previous;

        public void Init(bool val)
        {
            Current = val;
            Previous = val;
        }

        public void Set(bool val)
        {
            Previous = Current;
            Current = val;
        }

        public bool HasChanged()
        {
            return Current != Previous;
        }

        public void ConsumeChange()
        {
            Previous = Current;
        }

        public bool Get()
        {
            return Current;
        }
    }

    public static class MiscUtilities
    {
        public static void SetShadowModeInHierarchy(EntityManager entityManager, EntityCommandBuffer ecb,
            Entity onEntity, ref BufferLookup<Child> childBufferFromEntity,
            UnityEngine.Rendering.ShadowCastingMode mode)
        {
            if (entityManager.HasComponent<RenderFilterSettings>(onEntity))
            {
                RenderFilterSettings renderFilterSettings =
                    entityManager.GetSharedComponent<RenderFilterSettings>(onEntity);
                renderFilterSettings.ShadowCastingMode = mode;
                ecb.SetSharedComponent(onEntity, renderFilterSettings);
            }

            if (childBufferFromEntity.HasBuffer(onEntity))
            {
                DynamicBuffer<Child> childBuffer = childBufferFromEntity[onEntity];
                for (int i = 0; i < childBuffer.Length; i++)
                {
                    SetShadowModeInHierarchy(entityManager, ecb, childBuffer[i].Value, ref childBufferFromEntity, mode);
                }
            }
        }

        public static void DisableRenderingInHierarchy(EntityCommandBuffer ecb, Entity onEntity,
            ref BufferLookup<Child> childBufferFromEntity)
        {
            ecb.RemoveComponent<MaterialMeshInfo>(onEntity);

            if (childBufferFromEntity.HasBuffer(onEntity))
            {
                DynamicBuffer<Child> childBuffer = childBufferFromEntity[onEntity];
                for (int i = 0; i < childBuffer.Length; i++)
                {
                    DisableRenderingInHierarchy(ecb, childBuffer[i].Value, ref childBufferFromEntity);
                }
            }
        }

        public static bool HasSingleton<T>(ref SystemState state) where T : unmanaged, IComponentData
        {
            return new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(ref state).HasSingleton<T>();
        }

        public static T GetSingleton<T>(ref SystemState state) where T : unmanaged, IComponentData
        {
            return new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(ref state).GetSingleton<T>();
        }

        public static Entity GetSingletonEntity<T>(ref SystemState state) where T : unmanaged, IComponentData
        {
            return new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(ref state).GetSingletonEntity();
        }
    }
}
