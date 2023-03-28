using UnityEngine;
using Unity.Entities;

[DisallowMultipleComponent]
public class BasicPlayerAuthoring : MonoBehaviour
{
    public GameObject ControlledCharacter;
    public GameObject ControlledCamera;

    public class Baker : Baker<BasicPlayerAuthoring>
    {
        public override void Bake(BasicPlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BasicPlayer
            {
                ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
                ControlledCamera = GetEntity(authoring.ControlledCamera, TransformUsageFlags.Dynamic),
            });
            AddComponent(entity, new BasicPlayerInputs());
        }
    }
}