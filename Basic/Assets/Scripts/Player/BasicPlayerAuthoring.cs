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
            AddComponent(new BasicPlayer
            {
                ControlledCharacter = GetEntity(authoring.ControlledCharacter),
                ControlledCamera = GetEntity(authoring.ControlledCamera),
            });
            AddComponent(new BasicPlayerInputs());
        }
    }
}