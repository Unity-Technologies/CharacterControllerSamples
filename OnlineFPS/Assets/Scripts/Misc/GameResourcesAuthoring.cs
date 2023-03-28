using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

public class GameResourcesAuthoring : MonoBehaviour
{
    [Header("Network Parameters")] 
    public int TickRate = 60;
    public int SendRate = 60;
    public int MaxSimulationStepsPerFrame = 4;
    public float JoinTimeout = 10f;
    
    [Header("General Parameters")] 
    public float RespawnTime = 4f;
    
    [Header("Scenes")] 
    public BakedSubSceneReference MenuVisualsScene;
    public BakedSubSceneReference GameResourcesScene;
    public BakedSubSceneReference GameScene;
    
    [Header("Ghost Prefabs")] 
    public GameObject PlayerGhost;
    public GameObject CharacterGhost;
    public GameObject RailgunGhost;
    public GameObject MachineGunGhost;
    
    [Header("Other Prefabs")] 
    public GameObject SpectatorPrefab;

    public class Baker : Baker<GameResourcesAuthoring>
    {
        public override void Bake(GameResourcesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new GameResources
            {
                TickRate = authoring.TickRate,
                SendRate = authoring.SendRate,
                MaxSimulationStepsPerFrame = authoring.MaxSimulationStepsPerFrame,
                JoinTimeout = authoring.JoinTimeout,
                
                RespawnTime = authoring.RespawnTime,
            
                MenuVisualsScene = authoring.MenuVisualsScene.GetEntitySceneReference(),
                GameResourcesScene = authoring.GameResourcesScene.GetEntitySceneReference(),
                GameScene = authoring.GameScene.GetEntitySceneReference(),
            
                PlayerGhost = GetEntity(authoring.PlayerGhost, TransformUsageFlags.Dynamic),
                CharacterGhost = GetEntity(authoring.CharacterGhost, TransformUsageFlags.Dynamic),
                RailgunGhost = GetEntity(authoring.RailgunGhost, TransformUsageFlags.Dynamic),
                MachineGunGhost = GetEntity(authoring.MachineGunGhost, TransformUsageFlags.Dynamic),
            
                SpectatorPrefab = GetEntity(authoring.SpectatorPrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}