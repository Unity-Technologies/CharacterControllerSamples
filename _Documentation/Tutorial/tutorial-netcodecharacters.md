
# Networking the Standard Characters

In this section, we will go through the entire process of creating a minimal netcode project with predicted third-person or first-person characters, step by step.

There are many different ways to network the character controller, and different ways to optimize the bandwidth required to network character controllers. However, these largely depend on the specific game we are making. For this reason, the following tutorial only demonstrates one way to network the character (prediction on owning clients, interpolation on other clients) and will avoid most game-specific optimizations. An [Optimization Ideas](tutorial-netcodecharacters.md#optimization-ideas) section in this article will go over some of the potential optimizations that could be done, depending on you game's requirements. 

Note: this tutorial is meant for the "Standard Characters" of the latest release of the package, and is not guaranteed to work with "Standard Characters" of previous versions.


## Create a project and import required packages

Create a new project using a Unity version that's compatible with the latest Entities packages, and using the "3D (URP)" template. 

Import these packages into your project:
* `com.unity.charactercontroller`
* `com.unity.netcode`
* `com.unity.entities.graphics`

> Note: the `com.unity.entities` and `com.unity.physics` packages will also be imported automatically, as dependencies of the above packages.

Additionally, because of netcode, you will have to configure the project to run in background. Go to `Edit > Project Settings`, and select the `Player` section. In this window, enable the `Run In Background` setting under the `Resolution and Presentation` section. 


## Import Standard Characters

In the package manager, find the "Character Controller" package, and navigate to the "Samples" tab. Import the "Standard Characters" sample there.

The Stardard Characters will be added to your project under `Assets\Samples\Character Controller\[version]\Standard Characters`


## Setup character ghost prefabs

We will now add and configure ghost components on the various Stardard Character prefabs. 

* [Ghost prefab setup - Third Person](./tutorial-netcodecharacters-ghostprefab-thirdperson.md)
* [Ghost prefab setup - First Person](./tutorial-netcodecharacters-ghostprefab-firstperson.md)


## Create a bootstrap that allows clients to auto-connect to server

For the purpose of quick and easy testing, add this custom bootstrap to your project. This will make sure clients automatically connects to the server in play mode (based on the settings in the Multiplayer Playmode Tools window).

```cs
using Unity.NetCode;

public class AutoConnectBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {      
        AutoConnectPort = 7979;
        CreateDefaultClientServerWorlds();
        return true;
    }
}
```


## Create a game setup system

We will now add a "game setup" system, component, and authoring, that will take care of spawning and setting up characters for clients that join the server.

* [Game setup - Third Person](./tutorial-netcodecharacters-gamesetup-thirdperson.md)
* [Game setup - First Person](./tutorial-netcodecharacters-gamesetup-firstperson.md)


## Setup a game scene

* Create a new scene. We'll call this `GameScene`.
* Create a subscene within `GameScene`. We'll call this `GameSubScene`.
* Create a basic environment for your character in the `GameSubScene`: 
    * Add a floor and some walls or obstacles. 
    * Make sure to add physics colliders to all level geometry
    * Make sure it would be ok for your character to spawn at the world origin (0,0,0). There should at least be a floor to stand on there.
* In the `GameScene`, add a `MainGameObjectCamera` component to the default `Main Camera` GameObject.
* In the `GameSubScene`, add a new empty GameObject and name it `GameSetup`. On this GameObject, add a `GameSetupAuthoring` component, and assign the prefab references: 
    * If using the third-person character:
        * `CharacterPrefab` should be the `ThirdPersonCharacter` prefab
        * `PlayerPrefab` should be the `ThirdPersonPlayer` prefab
        * `CameraPrefab` should be the `OrbitCamera` prefab
    * If using the first-person character:
        * `CharacterPrefab` should be the `FirstPersonCharacter` prefab
        * `PlayerPrefab` should be the `FirstPersonPlayer` prefab


## Configure the Multiplayer PlayMode Tools

Open the Multiplayer PlayMode Tools window through the `Multiplayer > Window: PlayMode Tools` menu item in the top bar. 
* Make sure `Playmode Type` is set to `Client & Server`. This will take care of automatically setting up a server and a client that joins it upon entering Play mode.
* Make sure the lag simulator is set to `Ping View`. It should read `Simulator [ON]`.
* Set `RTT Delay (+ms)` to `100`, to simulate a lag of 100ms. This will allow us to confirm that our character prediction works well and compensates for the lag later on.

At this point, if you open your `GameScene` and press Play, you should see your character spawn into the world (at position 0,0,0) when you enter play mode. However, because we haven't yet defined how to sync things over network or how to handle player commands and prediction, character/camera controls will be broken. We'll see how to fix this in the following sections.


## Synchronize the required data over network

Create a new `GhostVariants.cs` file (the name could be anything), and add the following code to it. This will take care of telling netcode what to synchronize over network by default for the `KinematicCharacterBody`, `CharacterInterpolation` and `TrackedTransform` components on ghosts:

```cs
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.CharacterController;

public partial class DefaultVariantSystem : DefaultVariantSystemBase
{
    protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
    {
        defaultVariants.Add(typeof(KinematicCharacterBody), Rule.ForAll(typeof(KinematicCharacterBody_DefaultVariant)));
        defaultVariants.Add(typeof(CharacterInterpolation), Rule.ForAll(typeof(CharacterInterpolation_GhostVariant)));
        defaultVariants.Add(typeof(TrackedTransform), Rule.ForAll(typeof(TrackedTransform_DefaultVariant)));
    }
}

[GhostComponentVariation(typeof(KinematicCharacterBody))]
[GhostComponent()]
public struct KinematicCharacterBody_DefaultVariant
{
    // These two fields represent the basic synchronized state data that all networked characters will need.
    [GhostField()]
    public float3 RelativeVelocity;
    [GhostField()]
    public bool IsGrounded;
    
    // The following fields are only needed for characters that need to support parent entities (stand on moving platforms).
    // You can safely omit these from ghost sync if your game does not make use of character parent entities (any entities that have a TrackedTransform component).
    [GhostField()]
    public Entity ParentEntity;
    [GhostField()]
    public float3 ParentLocalAnchorPoint;
    [GhostField()]
    public float3 ParentVelocity;
}

// Character interpolation must only exist on predicted clients:
// - for remote interpolated ghost characters, interpolation is handled by netcode.
// - for server, interpolation is superfluous.
[GhostComponentVariation(typeof(CharacterInterpolation))]
[GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
public struct CharacterInterpolation_GhostVariant
{
}

[GhostComponentVariation(typeof(TrackedTransform))]
[GhostComponent()]
public struct TrackedTransform_DefaultVariant
{
    [GhostField()]
    public RigidTransform CurrentFixedRateTransform;
}
```

If you inspect the `Ghost Authoring Component` on your character prefab after adding and compiling the code above, you should now see that the ghost uses the `KinematicCharacterBody_DefaultVariant` variant for the `KinematicCharacterBody` component.

Now that we've defined ghost fields for internal components of the character package, let's define ghost fields for the standard character components. Since this code lives in your project and not in the package, we can directly add `[GhostComponent]` and `[GhostField]` attributes to the components and fields:
* [Ghost fields - Third Person](./tutorial-netcodecharacters-ghostfields-thirdperson.md)
* [Ghost fields - First Person](./tutorial-netcodecharacters-ghostfields-firstperson.md)


## Set up player commands and prediction

We must now alter the input handling and update groups of some of our systems in order to make them compatible with netcode prediction.

First, copy the following utility class into your project. This will help simplify the process of handling inputs that represent a "delta" that depends on frame time rather than a constant input value (for example: mouse movement delta). These have special considerations for netcode:
[NetworkInputUtilities](./tutorial-netcodecharacters-inpututil.md)

Then, follow these guides in order to convert the character input handling systems and update orders:
* [Commands and Prediction - Third Person](./tutorial-netcodecharacters-prediction-thirdperson.md)
* [Commands and Prediction - First Person](./tutorial-netcodecharacters-prediction-firstperson.md)


## Conclusion

You can now play the game, and enjoy perfectly responsive character controls despite network lag.


## Optimization ideas

The following section goes over some of the potential bandwidth usage optimizations that could be done, depending on you game's requirements: 
[Optimization ideas](./tutorial-netcodecharacters-optimization.md)
