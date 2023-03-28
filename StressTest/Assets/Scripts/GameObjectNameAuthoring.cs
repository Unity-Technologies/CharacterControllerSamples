using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct GameObjectName : IComponentData
{
    public FixedString128Bytes Name;
}

public class GameObjectNameAuthoring : MonoBehaviour
{ 
    public class Baker : Baker<GameObjectNameAuthoring>
    {
        public override void Bake(GameObjectNameAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new GameObjectName
            {
                Name = new FixedString128Bytes(authoring.gameObject.name),
            });
        }
    }
}