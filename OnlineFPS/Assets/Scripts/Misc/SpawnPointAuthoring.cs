using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OnlineFPS
{
    public class SpawnPointAuthoring : MonoBehaviour
    {
        public class Baker : Baker<SpawnPointAuthoring>
        {
            public override void Bake(SpawnPointAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SpawnPoint());
            }
        }
    }
}