using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))] // Updates after transforms, because shots must spawn at the interpolated weapon position
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial class WeaponShotVisualsGroup : ComponentSystemGroup
{
}

[UpdateInGroup(typeof(WeaponShotVisualsGroup))]
public partial class WeaponShotVisualsSpawnECBSystem : EntityCommandBufferSystem
{
    public unsafe struct Singleton : IComponentData, IECBSingleton
    {
        internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
        internal Allocator allocator;

        public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
        {
            return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
        }

        public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
        {
            pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
        }

        public void SetAllocator(Allocator allocatorIn)
        {
            allocator = allocatorIn;
        }
    }
    
    protected override unsafe void OnCreate()
    {
        base.OnCreate();

        ref UnsafeList<EntityCommandBuffer> pendingBuffers = ref *m_PendingBuffers;
        this.RegisterSingleton<Singleton>(ref pendingBuffers, World.Unmanaged);
    }
}