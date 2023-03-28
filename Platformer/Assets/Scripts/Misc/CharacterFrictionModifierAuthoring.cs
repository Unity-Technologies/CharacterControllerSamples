using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CharacterFrictionModifierAuthoring : MonoBehaviour
{
    public float Friction = 1f;

    class Baker : Baker<CharacterFrictionModifierAuthoring>
    {
        public override void Bake(CharacterFrictionModifierAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new CharacterFrictionModifier { Friction = authoring.Friction });
        }
    }
}
