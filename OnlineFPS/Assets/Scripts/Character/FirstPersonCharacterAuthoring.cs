using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using Unity.CharacterController;
using Unity.Physics;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace OnlineFPS
{
    [DisallowMultipleComponent]
    public class FirstPersonCharacterAuthoring : MonoBehaviour
    {
        public GameObject ViewObject;
        public GameObject NameTagSocket;
        public GameObject WeaponSocket;
        public GameObject WeaponAnimationSocket;
        public GameObject DeathVFXSpawnPoint;
        public float DeathVFXSize = 1f;
        public float DeathVFXSpeed = 1f;
        public float DeathVFXLifetime = 1f;
        [ColorUsage(false, true)] public Color DeathVFXColor = Color.blue;

        public AuthoringKinematicCharacterProperties CharacterProperties =
            AuthoringKinematicCharacterProperties.GetDefault();

        public FirstPersonCharacterComponent Character = FirstPersonCharacterComponent.GetDefault();

        public class Baker : Baker<FirstPersonCharacterAuthoring>
        {
            public override void Bake(FirstPersonCharacterAuthoring authoring)
            {
                KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                authoring.Character.ViewEntity = GetEntity(authoring.ViewObject, TransformUsageFlags.Dynamic);
                authoring.Character.NameTagSocketEntity =
                    GetEntity(authoring.NameTagSocket, TransformUsageFlags.Dynamic);
                authoring.Character.WeaponSocketEntity = GetEntity(authoring.WeaponSocket, TransformUsageFlags.Dynamic);
                authoring.Character.WeaponAnimationSocketEntity =
                    GetEntity(authoring.WeaponAnimationSocket, TransformUsageFlags.Dynamic);
                authoring.Character.DeathVFXSpawnPoint =
                    GetEntity(authoring.DeathVFXSpawnPoint, TransformUsageFlags.Dynamic);

                authoring.Character.DeathVFXColor = ((float4)(Vector4)authoring.DeathVFXColor).xyz;
                authoring.Character.DeathVFXSize = authoring.DeathVFXSize;
                authoring.Character.DeathVFXSpeed = authoring.DeathVFXSpeed;
                authoring.Character.DeathVFXLifetime = authoring.DeathVFXLifetime;

                AddComponent(entity, authoring.Character);
                AddComponent(entity, new FirstPersonCharacterControl());
                AddComponent(entity, new OwningPlayer());
                AddComponent(entity, new ActiveWeapon());
                AddComponent(entity, new CharacterWeaponVisualFeedback());
                AddComponent(entity, new DelayedDespawn());
                SetComponentEnabled<DelayedDespawn>(entity, false);
                AddComponent(entity, new CharacterInitialized());
                SetComponentEnabled<CharacterInitialized>(entity, false);
            }
        }
    }
}