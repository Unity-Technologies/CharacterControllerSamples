
# Tutorial - Character Hits

We will now look at how we can iterate on character hits that happened during the frame. You can think of these as the character's "collision events". 

We will create a new system that updates after the `KinematicCharacterPhysicsUpdateGroup`, and iterates on all `DynamicBuffer<KinematicCharacterHit>`. During the character update, all hits that have been detected are added to this buffer. Therefore we must iterate on it *after* the entire character update is finished if we want all detected hits to be present in the buffer.

We will also create a second job within that new system, which iterates on `DynamicBuffer<StatefulKinematicCharacterHit>`. These hits are similar to `KinematicCharacterHit`, except they also keep track of the state of the hit (Enter, Exit, Stay).

```cs
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.CharacterController;

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
                    UnityEngine.Debug.Log($"Detected an ungrounded hit {hit.Entity.Index}");
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
                    UnityEngine.Debug.Log($"Entered new hit {hit.Hit.Entity.Index}");
                }
                else if (hit.State == CharacterHitState.Exit)
                {
                    UnityEngine.Debug.Log($"Exited a hit {hit.Hit.Entity.Index}");
                }
            }
        }
    }
}
```

This system demonstrates how to iterate over those character hits. In this example, the system will print a message in the console whenever the character detected an ungrounded hit, and every time we have entered any new hit.

Note: if you don't want to use the stateful hits and don't want to pay any processing cost for them, you can remove the feature by removing the `CharacterAspect.Update_ProcessStatefulCharacterHits();` line in your `ThirdPersonCharacterAspect.PhysicsUpdate`