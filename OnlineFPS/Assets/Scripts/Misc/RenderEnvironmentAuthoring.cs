using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class RenderEnvironmentAuthoring : MonoBehaviour
{
        public BakedGameObjectSceneReference LightingScene;

        public class Baker : Baker<RenderEnvironmentAuthoring>
        {
                public override void Bake(RenderEnvironmentAuthoring authoring)
                {
                        AddComponentObject(new RenderEnvironment
                        {
                                LightingSceneIndex = authoring.LightingScene.GetIndexInBuildScenes(),
                        });
                }
        }
}
