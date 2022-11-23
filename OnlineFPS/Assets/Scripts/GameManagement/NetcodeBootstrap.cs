using System.Collections;
using System.Collections.Generic;
using Unity.NetCode;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class NetCodeBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        CreateLocalWorld(defaultWorldName);
        return true;
    }
}