using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace OnlineFPS
{
    [Serializable]
    public struct MainEntityCamera : IComponentData
    {
        public MainEntityCamera(float fov)
        {
            BaseFoV = fov;
            CurrentFoV = fov;
        }

        public float BaseFoV;
        public float CurrentFoV;
    }
}