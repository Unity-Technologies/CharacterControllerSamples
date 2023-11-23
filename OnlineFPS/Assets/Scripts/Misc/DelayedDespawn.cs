using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct DelayedDespawn : IComponentData
{
    public float Timer;
    public byte HasDisabledRendering;
}
