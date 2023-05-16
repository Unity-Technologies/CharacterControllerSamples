using System.Collections;
using System.Collections.Generic;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using UnityEngine;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct CharacterTriggerEventDebuggerSystem : ISystem
{
    void OnUpdate(ref SystemState state)
    {
        foreach (var (triggerEvents, entity) in SystemAPI.Query<DynamicBuffer<StatefulTriggerEvent>>().WithEntityAccess())
        {
            for (int i = 0; i < triggerEvents.Length; i++)
            {
                if (triggerEvents[i].State == StatefulEventState.Enter)
                {
                    Entity otherEntity = triggerEvents[i].GetOtherEntity(entity);
                    if (SystemAPI.HasComponent<KinematicCharacterBody>(otherEntity))
                    {
                        UnityEngine.Debug.Log($"Entity {entity.Index} detected trigger enter with {otherEntity.Index}");
                    }
                }
            }
        }
    }
}
