using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public class TeleporterAuthoring : MonoBehaviour
{
    public GameObject Destination;

    public class Baker : Baker<TeleporterAuthoring>
    {
        public override void Bake(TeleporterAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Teleporter { DestinationEntity = GetEntity(authoring.Destination, TransformUsageFlags.Dynamic) });
        }
    }
}