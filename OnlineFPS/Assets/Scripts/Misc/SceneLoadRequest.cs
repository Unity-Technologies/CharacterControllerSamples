using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

[Serializable]
public struct SceneLoadRequest : IComponentData
{
    public bool IsLoaded;
}

[Serializable]
public struct SceneIdentifier : IBufferElementData
{
    public EntitySceneReference SceneReference;
    public Entity SceneEntity;

    public SceneIdentifier(EntitySceneReference sceneReference)
    {
        SceneReference = sceneReference;
        SceneEntity = default;
    }
}
