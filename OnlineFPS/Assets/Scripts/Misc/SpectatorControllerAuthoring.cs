using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class SpectatorControllerAuthoring : MonoBehaviour
{
    public SpectatorController.Parameters Parameters; 

    public class Baker : Baker<SpectatorControllerAuthoring>
    {
        public override void Bake(SpectatorControllerAuthoring authoring)
        {
            AddComponent(new SpectatorController { Params = authoring.Parameters } );
        }
    }
}