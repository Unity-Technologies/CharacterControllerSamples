
using Unity.Entities;
using Unity.CharacterController;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateAfter(typeof(KinematicCharacterPhysicsUpdateGroup))]
public partial class CharacterHitsDetectionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Iterate on non-stateful hits
        foreach (var hitsBuffer in SystemAPI.Query<DynamicBuffer<KinematicCharacterHit>>())
        {
            for (int i = 0; i < hitsBuffer.Length; i++)
            {
                KinematicCharacterHit hit = hitsBuffer[i];
                if(!hit.IsGroundedOnHit)
                {
                    // UnityEngine.Debug.Log($"Detected an ungrounded hit {hit.Entity.Index}");
                }
            }
        }
        
        // Iterate on stateful hits
        foreach (var statefulHitsBuffer in SystemAPI.Query<DynamicBuffer<StatefulKinematicCharacterHit>>())
        {
            for (int i = 0; i < statefulHitsBuffer.Length; i++)
            {
                StatefulKinematicCharacterHit hit = statefulHitsBuffer[i];
                if (hit.State == CharacterHitState.Enter)
                {
                    // UnityEngine.Debug.Log($"Entered new hit {hit.Hit.Entity.Index}");
                }
                else if (hit.State == CharacterHitState.Exit)
                {
                    // UnityEngine.Debug.Log($"Exited a hit {hit.Hit.Entity.Index}");
                }
            }
        }
    }
}