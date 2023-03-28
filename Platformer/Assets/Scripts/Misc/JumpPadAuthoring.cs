using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class JumpPadAuthoring : MonoBehaviour
{
    public JumpPad JumpPad;

    class Baker : Baker<JumpPadAuthoring>
    {
        public override void Bake(JumpPadAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.JumpPad);
        }
    }
}
