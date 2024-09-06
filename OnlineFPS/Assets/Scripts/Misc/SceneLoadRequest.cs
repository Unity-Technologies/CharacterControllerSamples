using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace OnlineFPS
{
    [Serializable]
    public struct SceneLoadRequest : IComponentData
    {
        public bool IsLoaded;
    }

    [Serializable]
    public struct SceneIdentifier : IBufferElementData
    {
        public Entity SceneEntity;

        public SceneIdentifier(Entity sceneEntity)
        {
            SceneEntity = sceneEntity;
        }
    }
}
