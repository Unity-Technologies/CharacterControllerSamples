
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class JumpPadAuthoring : MonoBehaviour
{
    public float3 JumpForce;

    class Baker : Baker<JumpPadAuthoring>
    {
        public override void Bake(JumpPadAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new JumpPad { JumpForce = authoring.JumpForce });
        }
    }
} 