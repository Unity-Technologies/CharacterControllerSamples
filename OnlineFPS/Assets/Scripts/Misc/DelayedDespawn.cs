using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace OnlineFPS
{
    [GhostComponent]
    [GhostEnabledBit]
    public struct DelayedDespawn : IComponentData, IEnableableComponent
    {
        public uint Ticks;
        public byte HasHandledPreDespawn;
    }
}