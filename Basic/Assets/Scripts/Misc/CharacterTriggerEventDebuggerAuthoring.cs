using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CharacterTriggerEventDebuggerAuthoring : MonoBehaviour
{
    class Baker : Baker<CharacterTriggerEventDebuggerAuthoring>
    {
        public override void Bake(CharacterTriggerEventDebuggerAuthoring authoring)
        {
            AddComponent(GetEntity(authoring, TransformUsageFlags.None), new CharacterTriggerEventDebugger());
        }
    }
}
