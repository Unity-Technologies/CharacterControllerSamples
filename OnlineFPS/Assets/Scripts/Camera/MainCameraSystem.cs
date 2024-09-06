using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace OnlineFPS
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MainCameraSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (MainGameObjectCamera.Instance != null && SystemAPI.HasSingleton<MainEntityCamera>())
            {
                Entity mainEntityCameraEntity = SystemAPI.GetSingletonEntity<MainEntityCamera>();
                MainEntityCamera mainEntityCamera = SystemAPI.GetSingleton<MainEntityCamera>();
                LocalToWorld targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(mainEntityCameraEntity);

                MainGameObjectCamera.Instance.transform.SetPositionAndRotation(targetLocalToWorld.Position,
                    targetLocalToWorld.Rotation);
                MainGameObjectCamera.Instance.fieldOfView = mainEntityCamera.CurrentFoV;
            }
        }
    }
}