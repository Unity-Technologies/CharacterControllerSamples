using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class BasicAICharacterAuthoring : MonoBehaviour
{
    public BasicAICharacter Data;

    public class Baker : Baker<BasicAICharacterAuthoring>
    {
        public override void Bake(BasicAICharacterAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), authoring.Data);
        }
    }
}