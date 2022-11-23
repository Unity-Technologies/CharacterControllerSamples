using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Rival;
using Unity.Physics;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PhysicsShapeAuthoring))]
public class ThirdPersonCharacterAuthoring : MonoBehaviour
{
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    public ThirdPersonCharacterComponent Character = ThirdPersonCharacterComponent.GetDefault();

    public class Baker : Baker<ThirdPersonCharacterAuthoring>
    {
        public override void Bake(ThirdPersonCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

            AddComponent(authoring.Character);
            AddComponent(new ThirdPersonCharacterControl());
        }
    }

}