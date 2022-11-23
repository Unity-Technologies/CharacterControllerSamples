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
            AddComponent(authoring.Vehicle);
        
            DynamicBuffer<VehicleWheels> wheelsBuffer = AddBuffer<VehicleWheels>();
            foreach (GameObject wheelGO in authoring.Wheels)
            {
                wheelsBuffer.Add(new VehicleWheels { 
                    MeshEntity = GetEntity(wheelGO.GetComponentInChildren<MeshRenderer>().gameObject),
                    CollisionEntity = GetEntity(wheelGO.GetComponentInChildren<PhysicsShapeAuthoring>().gameObject),
                });
            }
        }
    }
}