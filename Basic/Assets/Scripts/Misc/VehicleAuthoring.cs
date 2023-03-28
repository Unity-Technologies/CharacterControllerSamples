using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using Unity.Physics.Authoring;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class VehicleAuthoring : MonoBehaviour
{
    public Vehicle Vehicle;
    public List<GameObject> Wheels;

    public class Baker : Baker<VehicleAuthoring>
    {
        public override void Bake(VehicleAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, authoring.Vehicle);
        
            DynamicBuffer<VehicleWheels> wheelsBuffer = AddBuffer<VehicleWheels>(entity);
            foreach (GameObject wheelGO in authoring.Wheels)
            {
                wheelsBuffer.Add(new VehicleWheels { 
                    MeshEntity = GetEntity(wheelGO.GetComponentInChildren<MeshRenderer>().gameObject, TransformUsageFlags.Dynamic),
                    CollisionEntity = GetEntity(wheelGO.GetComponentInChildren<PhysicsShapeAuthoring>().gameObject, TransformUsageFlags.Dynamic),
                });
            }
        }
    }
}