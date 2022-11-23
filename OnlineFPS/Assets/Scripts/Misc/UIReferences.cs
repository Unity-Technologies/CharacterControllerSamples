using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class UIReferences : MonoBehaviour
{
    public string InitialJoinAddress = "127.0.0.1";
    public UIDocument MenuDocument;
    public UIDocument CrosshairDocument;
    public UIDocument RespawnScreenDocument;
    
    void Start()
    {
        World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<GameUISystem>().SetUIReferences(this);
    }
}
