using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Unity.CharacterController;
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

            Entity selfEntity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(selfEntity, authoring.Character);
            AddComponent(selfEntity, new StressTestCharacterControl());
        }
    }
}
