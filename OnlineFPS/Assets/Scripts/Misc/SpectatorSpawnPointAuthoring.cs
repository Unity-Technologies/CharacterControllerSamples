using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OnlineFPS
{
    public class SpectatorSpawnPointAuthoring : MonoBehaviour
    {
        public class Baker : Baker<SpectatorSpawnPointAuthoring>
        {
            public override void Bake(SpectatorSpawnPointAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SpectatorSpawnPoint());
            }
        }
    }
}