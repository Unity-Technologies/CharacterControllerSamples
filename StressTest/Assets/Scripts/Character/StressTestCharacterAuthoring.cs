using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Rival;
using Unity.Physics;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PhysicsShapeAuthoring))]
public class StressTestCharacterAuthoring : MonoBehaviour
{
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    public StressTestCharacterComponent Character = StressTestCharacterComponent.GetDefault();

    public class Baker : Baker<StressTestCharacterAuthoring>
    {
        public override void Bake(StressTestCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

            AddComponent(authoring.Character);
            AddComponent(new StressTestCharacterControl());
        }
    }
}
