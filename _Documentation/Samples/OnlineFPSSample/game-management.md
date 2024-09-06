
# OnlineFPS Sample - Game Management

## Game Management

`GameManager` is a prefab and a monobehaviour that is loaded and instantiated automatically in the `GameBootstrap`. It serves as a central point where we define scenes, network parameters, and various other managed resources. `GameManager.OnInitialize` is called by the `GameBootstrap`, and handles initializing the game regardless of which scene we're playing from. By default, some game UI state is initialized, and a local ("offline") `GameSession` is created. When pressing the "Host" or "Join" UI buttons of the menu however, the `GameManager` will take care of creating Client/Server `GameSession`s accordingly.

`GameSession` represents a collection of 1 to many ECS worlds that are in play together. For example, when playing as a client only, a game session of one Client world will be created. On the other hand, when playing as a Client + Server + ThinClients, a game session of multiple worlds (client, server, thin client worlds) will be created. These worlds are grouped together as a `GameSession` in order to facilitate handling things like loading a subscene into all worlds of the session, managing the UI state of the main client world, or destroying all worlds of a same session when the main client world disconnects and returns to menu. 

`GameResources` represents the baked Entity data that should be available to game worlds. This contains some game parameters, as well as baked entity prefab references for the game's netcode ghosts. Whenever `GameManager` starts a new session, it loads the `GameResources` subscene into all worlds of that new `GameSession`.

`GameWorldSystem` is present in all worlds of a game session, and it is in charge of handling communication between the managed `GameSession` and the worlds that belong to that session. For example, if ECS systems detect that a client world has disconnected from server, the `GameWorldSystem` will take care of informing the managed `GameSession` that it is time to destroy the session and return to meny. As another example; if ECS systems determine that the UI crosshair should be disabled right now, they will set `GameWorldSystem.Singleton.CrosshairActive` to false, and the `GameWorldSystem` will take care of informing the `GameSession` that the UI crosshair should be disabled, but only if the main client world requests it (the world that is displayed for the player).

`ServerGameSystem` updates in the server world, and has the following responsibilities:
* Handle starting to listen to a network end point (`HandleListen`)
* Handle join requests from clients (`HandleAcceptJoinsOncePendingScenesAreLoaded`, `HandleJoinRequests`, `HandlePendingJoinClientTimeout`)
* Handle character initialization and spawning (`HandleCharacters`)

`ClientGameSystem` updates in the client world, and has the following responsibilities:
* Handle sending a join request with player info to the server (`HandleSendJoinRequest`, `HandleWaitForJoinConfirmation`)
* Handle setting up newly-spawned character ghosts (`HandleCharacterSetup`)
* Handle requests to activate the respawn screen when the local character dies (`HandleRespawnScreen`)
* Handle returning to menu on connection timeout (`HandleDisconnect`)


## Playing directly from game scenes

This project allows starting a fully-configured networked game directly from game scenes (and only from game scenes, such as `Map1`), rather than having to start from the menu.

Due to all of the special setup required around `GameManager`, `GameSession`, `GameResources`, etc... for starting a game, this is all handled by the `GameManager`. In `GameManager.OnInitialize` (which is called by our custom bootstrap), if we detect that there is a `AutoNetcodePlayMode` monobehaviour active in the scene, we will automatically create a `GameSession` that respects the Client/Server/ThinClient parameters that are set-up in the Multiplayer Playmode Tools window. Therefore, the presence of a `AutoNetcodePlayMode` object in the `Map1` scene is what allows us to play the game directly from that scene.