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
            AddComponent(new ThirdPersonPlayer
            {
                ControlledCharacter = GetEntity(authoring.ControlledCharacter),
                ControlledCamera = GetEntity(authoring.ControlledCamera),
            });
            AddComponent(new ThirdPersonPlayerInputs());
        }
    }
}