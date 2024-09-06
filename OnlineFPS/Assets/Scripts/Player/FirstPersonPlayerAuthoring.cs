using UnityEngine;
using Unity.Entities;

namespace OnlineFPS
{
    [DisallowMultipleComponent]
    public class FirstPersonPlayerAuthoring : MonoBehaviour
    {
        public GameObject ControlledCharacter;

        public class Baker : Baker<FirstPersonPlayerAuthoring>
        {
            public override void Bake(FirstPersonPlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new FirstPersonPlayer
                {
                    ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
                });
                AddComponent(entity, new FirstPersonPlayerNetworkInput());
                AddComponent<FirstPersonPlayerCommands>(entity);
            }
        }
    }
}