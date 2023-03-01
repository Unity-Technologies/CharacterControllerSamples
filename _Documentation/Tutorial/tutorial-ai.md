
# Tutorial - AI Character

We will now go over the implementation of very rudimentary AI characters. 

We want to have characters that detect human-controlled characters at a distance, and start moving towards them as long as they are in their detection range. We will also demonstrate how we can structure things so that we don't have to create an entirely new character controller for AIs, and we can simply reuse the same that our player character uses.

Let's first create our `AIController` component & authoring. This component represents an "AI Brain" that controls a specific character:
```cs
using System;
using Unity.Entities;
using Unity.Physics.Authoring;

[Serializable]
public struct AIController : IComponentData
{
    public float DetectionDistance;
    public PhysicsCategoryTags DetectionFilter;
}
```

```cs
using UnityEngine;
using Unity.Entities;

public class AIControllerAuthoring : MonoBehaviour
{
    public AIController AIController;

    class Baker : Baker<AIControllerAuthoring>
    {
        public override void Bake(AIControllerAuthoring authoring)
        {
            AddComponent(authoring.AIController);
        }
    }
}
```

And now the `AIControllerSystem`. This system iterates on entities that have `AIController` and character components, handles detecting player characters, and handles writing move inputs to the `ThirdPersonCharacterControl` component so that the AI character move towards detected player characters:
```cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public partial class AIControllerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        PhysicsWorld physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        NativeList<DistanceHit> distanceHits = new NativeList<DistanceHit>(Allocator.TempJob);

        foreach (var (characterControl, aiController, localTransform) in SystemAPI.Query<RefRW<ThirdPersonCharacterControl>, AIController, LocalTransform>())
        {
            // Clear our detected hits list between each use
            distanceHits.Clear();

            // Create a hit collector for the detection hits
            AllHitsCollector<DistanceHit> hitsCollector = new AllHitsCollector<DistanceHit>(aiController.DetectionDistance, ref distanceHits);

            // Detect hits that are within the detection range of the AI character
            PointDistanceInput distInput = new PointDistanceInput
            {
                Position = localTransform.Position,
                MaxDistance = aiController.DetectionDistance,
                Filter = new CollisionFilter { BelongsTo = CollisionFilter.Default.BelongsTo, CollidesWith = aiController.DetectionFilter.Value },
            };
            physicsWorld.CalculateDistance(distInput, ref hitsCollector);

            // Iterate on all detected hits to try to find a human-controlled character...
            Entity selectedTarget = Entity.Null;
            for (int i = 0; i < hitsCollector.NumHits; i++)
            {
                Entity hitEntity = distanceHits[i].Entity;

                // If it has a character component but no AIController component, that means it's a human player character
                if (SystemAPI.HasComponent<ThirdPersonCharacterComponent>(hitEntity) && !SystemAPI.HasComponent<AIController>(hitEntity))
                {
                    selectedTarget = hitEntity;
                    break; // early out
                }
            }

            // In the character control component, set a movement vector that will make the ai character move towards the selected target
            if (selectedTarget != Entity.Null)
            {
                characterControl.ValueRW.MoveVector = math.normalizesafe(SystemAPI.GetComponent<LocalTransform>(selectedTarget).Position - localTransform.Position);
            }
            else
            {
                characterControl.ValueRW.MoveVector = float3.zero;
            }
        }
    }
}
```

Note: you could also choose to make the AI detection zones work with trigger colliders instead of a `CalculateDistance` query, if you prefer that approach.

Now you can create a copy of your character object in the Subscene, name it "AICharacter", and add an `AIController` component to it. Set the `DetectionDistance` to 8 for example, and the `DetectionFilter` to "Everything". 

If you press Play, AICharacters should start chasing you once you get within detection range

![](../Images/tutorial_ai.gif)
