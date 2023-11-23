

First, the `GameSetup` component will hold the various ghost prefab entities that we'll need to instantiate:

```cs
using System;
using Unity.Entities;

[Serializable]
public struct GameSetup : IComponentData
{
    public Entity CharacterPrefab;
    public Entity PlayerPrefab;
    public Entity CameraPrefab;
}
```

Then, the `GameSetupAuthoring` will allow us to setup our prefabs in the inspector and bake it into our subscene:

```cs
using Unity.Entities;
using UnityEngine;

public class GameSetupAuthoring : MonoBehaviour
{   
    public GameObject CharacterPrefab;
    public GameObject PlayerPrefab;
    public GameObject CameraPrefab;

    class Baker : Baker<GameSetupAuthoring>
    {
        public override void Bake(GameSetupAuthoring authoring)
        {
            AddComponent(GetEntity(authoring, TransformUsageFlags.None), new GameSetup
            {
                CharacterPrefab = GetEntity(authoring.CharacterPrefab, TransformUsageFlags.None),
                PlayerPrefab = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.None),
                CameraPrefab = GetEntity(authoring.CameraPrefab, TransformUsageFlags.None),
            });
        }
    }
}
```

Finally, the `GameSetupSystem` will take care of spawning and setting up character prefabs when a client joins the server:

```cs
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;

public struct ClientJoinRequest : IRpcCommand
{ }

public struct LocalInitialized : IComponentData
{ }

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ServerGameSetupSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        _random = Random.CreateFromIndex(0);
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<GameSetup>())
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Get our GameSetup singleton, which contains the prefabs we'll spawn
            GameSetup gameSetup = SystemAPI.GetSingleton<GameSetup>();
            
            // When a client wants to join, spawn and setup a character for them
            foreach (var (recieveRPC, joinRequest, entity) in SystemAPI.Query<ReceiveRpcCommandRequest, ClientJoinRequest>().WithEntityAccess())
            {                
                // Spawn character, player, and camera ghost prefabs
                Entity characterEntity = ecb.Instantiate(gameSetup.CharacterPrefab);
                Entity playerEntity = ecb.Instantiate(gameSetup.PlayerPrefab);
                Entity cameraEntity = ecb.Instantiate(gameSetup.CameraPrefab);
                    
                // Add spawned prefabs to the connection entity's linked entities, so they get destroyed along with it
                ecb.AppendToBuffer(recieveRPC.SourceConnection, new LinkedEntityGroup { Value = characterEntity });
                ecb.AppendToBuffer(recieveRPC.SourceConnection, new LinkedEntityGroup { Value = playerEntity });
                ecb.AppendToBuffer(recieveRPC.SourceConnection, new LinkedEntityGroup { Value = cameraEntity });
                
                // Setup the owners of the ghost prefabs (which are all owner-predicted) 
                // The owner is the client connection that sent the join request
                int clientConnectionId = SystemAPI.GetComponent<NetworkId>(recieveRPC.SourceConnection).Value;
                ecb.SetComponent(characterEntity, new GhostOwner { NetworkId = clientConnectionId });
                ecb.SetComponent(playerEntity, new GhostOwner { NetworkId = clientConnectionId });
                ecb.SetComponent(cameraEntity, new GhostOwner { NetworkId = clientConnectionId });

                // Setup links between the prefabs
                ThirdPersonPlayer player = SystemAPI.GetComponent<ThirdPersonPlayer>(gameSetup.PlayerPrefab);
                player.ControlledCharacter = characterEntity;
                player.ControlledCamera = cameraEntity;
                ecb.SetComponent(playerEntity, player);
                
                // Place character at a random point around world origin
                ecb.SetComponent(characterEntity, LocalTransform.FromPosition(_random.NextFloat3(new float3(-5f,0f,-5f), new float3(5f,0f,5f))));
                
                // Allow this client to stream in game
                ecb.AddComponent<NetworkStreamInGame>(recieveRPC.SourceConnection);
                    
                // Destroy the RPC since we've processed it
                ecb.DestroyEntity(entity);
            }
        }
    }
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
public partial struct ClientGameSetupSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
        
        // Send a join request to the server if we haven't done so yet
        foreach (var (netId, entity) in SystemAPI.Query<NetworkId>().WithNone<NetworkStreamInGame>().WithEntityAccess())
        {
            // Mark our connection as ready to go in game
            ecb.AddComponent(entity, new NetworkStreamInGame()); 
            
            // Send an RPC that asks the server if we can join
            Entity joinRPC = ecb.CreateEntity();
            ecb.AddComponent(joinRPC, new ClientJoinRequest());
            ecb.AddComponent(joinRPC, new SendRpcCommandRequest { TargetConnection = entity });
        }
        
        // Handle initialization for our local character camera (mark main camera entity)
        foreach (var (camera, entity) in SystemAPI.Query<OrbitCamera>().WithAll<GhostOwnerIsLocal>().WithNone<LocalInitialized>().WithEntityAccess())
        {
            ecb.AddComponent(entity, new MainEntityCamera());
            ecb.AddComponent(entity, new LocalInitialized());
        }
    }
}
```