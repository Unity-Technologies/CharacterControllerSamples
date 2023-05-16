using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Unity.CharacterController;
using Unity.Physics;
using System.Collections.Generic;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FirstPersonCharacterAuthoring : MonoBehaviour
{
    public GameObject ViewObject;
    public GameObject NameTagSocket;
    public GameObject WeaponSocket;
    public GameObject WeaponAnimationSocket;
    public GameObject DeathVFX;
    public GameObject DeathVFXSpawnPoint;
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    public FirstPersonCharacterComponent Character = FirstPersonCharacterComponent.GetDefault();

    public class Baker : Baker<FirstPersonCharacterAuthoring>
    {
        public override void Bake(FirstPersonCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            
            authoring.Character.ViewEntity = GetEntity(authoring.ViewObject, TransformUsageFlags.Dynamic);
            authoring.Character.NameTagSocketEntity = GetEntity(authoring.NameTagSocket, TransformUsageFlags.Dynamic);
            authoring.Character.WeaponSocketEntity = GetEntity(authoring.WeaponSocket, TransformUsageFlags.Dynamic);
            authoring.Character.WeaponAnimationSocketEntity = GetEntity(authoring.WeaponAnimationSocket, TransformUsageFlags.Dynamic);
            authoring.Character.DeathVFX = GetEntity(authoring.DeathVFX, TransformUsageFlags.Dynamic);
            authoring.Character.DeathVFXSpawnPoint = GetEntity(authoring.DeathVFXSpawnPoint, TransformUsageFlags.Dynamic);
        
            AddComponent(entity, authoring.Character);
            AddComponent(entity, new FirstPersonCharacterControl());
            AddComponent(entity, new OwningPlayer());
            AddComponent(entity, new ActiveWeapon());
            AddComponent(entity, new CharacterWeaponVisualFeedback());
        }
    }
}