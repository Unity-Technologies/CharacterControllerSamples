using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class SpawnPointAuthoring : MonoBehaviour
{
    public class Baker : Baker<SpawnPointAuthoring>
    {
        public override void Bake(SpawnPointAuthoring authoring)
        {
            AddComponent(new SpawnPoint());
        }
    }
}