using UnityEngine;
using Unity.Entities;

[DisallowMultipleComponent]
public class PlatformerPlayerAuthoring : MonoBehaviour
{
    public GameObject ControlledCharacter;
    public GameObject ControlledCamera;

    public class Baker : Baker<PlatformerPlayerAuthoring>
    {
        public override void Bake(PlatformerPlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new PlatformerPlayer
            {
                ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
                ControlledCamera = GetEntity(authoring.ControlledCamera, TransformUsageFlags.Dynamic),
            });
            AddComponent(entity, new PlatformerPlayerInputs());
        }
    }
}