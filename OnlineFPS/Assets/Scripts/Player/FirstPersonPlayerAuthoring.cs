using UnityEngine;
using Unity.Entities;

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
            AddComponent<FirstPersonPlayerCommands>(entity);
        }
    }
}