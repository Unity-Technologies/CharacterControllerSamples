using UnityEngine;
using Unity.Entities;

[DisallowMultipleComponent]
public class ThirdPersonPlayerAuthoring : MonoBehaviour
{
    public GameObject ControlledCharacter;
    public GameObject ControlledCamera;

    public class Baker : Baker<ThirdPersonPlayerAuthoring>
    {
        public override void Bake(ThirdPersonPlayerAuthoring authoring)
        {
            Entity selfEntity = GetEntity(TransformUsageFlags.None);
        
            AddComponent(selfEntity, new ThirdPersonPlayer
            {
                ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.None),
                ControlledCamera = GetEntity(authoring.ControlledCamera, TransformUsageFlags.None),
            });
            AddComponent(selfEntity, new ThirdPersonPlayerInputs());
        }
    }
}