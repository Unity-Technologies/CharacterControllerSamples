using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Unity.CharacterController;
using Unity.Physics;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class BasicCharacterAuthoring : MonoBehaviour
{
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    public BasicCharacterComponent Character = BasicCharacterComponent.GetDefault();

    public class Baker : Baker<BasicCharacterAuthoring>
    {
        public override void Bake(BasicCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

            AddComponent(GetEntity(TransformUsageFlags.Dynamic), authoring.Character);
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new BasicCharacterControl());
        }
    }
}