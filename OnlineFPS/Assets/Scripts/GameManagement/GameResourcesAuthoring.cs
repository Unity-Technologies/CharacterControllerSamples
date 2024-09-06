using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.NetCode;
using UnityEngine;

namespace OnlineFPS
{
    public class GameResourcesAuthoring : MonoBehaviour
    {
        [Header("Network Parameters")] public NetCodeConfig NetCodeConfig;
        public float JoinTimeout = 10f;
        public uint DespawnTicks = 30;
        public uint PolledEventsTicks = 30;

        [Header("General Parameters")] public float RespawnTime = 4f;

        [Header("Ghost Prefabs")] public GameObject PlayerGhost;
        public GameObject CharacterGhost;
        public bool ForceOnlyFirstWeapon = false;
        public List<GameObject> WeaponGhosts = new List<GameObject>();

        [Header("Other Prefabs")] public GameObject SpectatorPrefab;

        public class Baker : Baker<GameResourcesAuthoring>
        {
            public override void Bake(GameResourcesAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new GameResources
                {
                    ClientServerTickRate = authoring.NetCodeConfig.ClientServerTickRate,
                    JoinTimeout = authoring.JoinTimeout,
                    DespawnTicks = authoring.DespawnTicks,
                    PolledEventsTicks = authoring.PolledEventsTicks,
                    RespawnTime = authoring.RespawnTime,

                    PlayerGhost = GetEntity(authoring.PlayerGhost, TransformUsageFlags.Dynamic),
                    CharacterGhost = GetEntity(authoring.CharacterGhost, TransformUsageFlags.Dynamic),
                    SpectatorPrefab = GetEntity(authoring.SpectatorPrefab, TransformUsageFlags.Dynamic),

                    ForceOnlyFirstWeapon = authoring.ForceOnlyFirstWeapon,
                });
                DynamicBuffer<GameResourcesWeapon> weaponsBuffer = AddBuffer<GameResourcesWeapon>(entity);
                for (int i = 0; i < authoring.WeaponGhosts.Count; i++)
                {
                    weaponsBuffer.Add(new GameResourcesWeapon
                    {
                        WeaponPrefab = GetEntity(authoring.WeaponGhosts[i], TransformUsageFlags.Dynamic),
                    });
                }
            }
        }
    }
}