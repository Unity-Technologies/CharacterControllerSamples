using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public class TestMovingPlatformAuthoring : MonoBehaviour
{
    public TestMovingPlatform.AuthoringData MovingPlatform;

    public class Baker : Baker<TestMovingPlatformAuthoring>
    {
        public override void Bake(TestMovingPlatformAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TestMovingPlatform
            {
                Data = authoring.MovingPlatform,
                OriginalPosition = authoring.transform.position,
                OriginalRotation = authoring.transform.rotation,
            });
        }
    }
}