using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace OnlineFPS
{
    public class SpectatorControllerAuthoring : MonoBehaviour
    {
        public SpectatorController.Parameters Parameters;

        public class Baker : Baker<SpectatorControllerAuthoring>
        {
            public override void Bake(SpectatorControllerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SpectatorController { Params = authoring.Parameters });
            }
        }
    }
}