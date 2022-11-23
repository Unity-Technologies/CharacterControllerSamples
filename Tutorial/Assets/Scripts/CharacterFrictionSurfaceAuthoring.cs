
using UnityEngine;
using Unity.Entities;

public class CharacterFrictionSurfaceAuthoring : MonoBehaviour
{
    public float VelocityFactor;

    class Baker : Baker<CharacterFrictionSurfaceAuthoring>
    {
        public override void Bake(CharacterFrictionSurfaceAuthoring authoring)
        {
            AddComponent(new CharacterFrictionSurface { VelocityFactor = authoring.VelocityFactor });
        }
    }
}