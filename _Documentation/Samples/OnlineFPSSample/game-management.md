
# OnlineFPS Sample - Game Management

## Game Management

There are different ECS worlds in this sample game:
* The default world: always present, and mostly handles general game state (menu, in-game, etc...), client/server world management, and UI.
* Client world: only exists during gameplay
* Server world: only exists during gameplay

`GameManagementSystem` updates in the default world and is mainly responsible for handling joining, hosting, and disconnecting. It creates/destroys server & client worlds as needed, loads the game scene, receives requests from the Client world to toggle activation of UI elements, etc...

`GameUISystem` updates in the default world and handles all UI logic. It keeps references to various UI elements based on a `UIReferences` monobehaviour that registers itself into this system on start.

`ServerGameSystem` updates in the server world, and has the following responsibilities:
* Handle join requests from clients (`HandleAcceptJoinsOncePendingScenesAreLoaded`, `HandleJoinRequests`, `HandlePendingJoinClientTimeout`)
* Handle cleanup on client disconnect (`HandleDisconnect`)
* Handle triggering a respawn countdown on character death (`HandleCharacterDeath`)
* Handle character spawning (`HandleSpawnCharacter`)

`ClientGameSystem` updates in the client world, and has the following responsibilities:
* Handle sending a join request with player info to the server (`HandleSendJoinRequestOncePendingScenesLoaded`, `HandlePendingJoinRequest`)
* Handle setting up newly-spawned character ghosts (`HandleCharacterSetupAndDestruction`)
* Handle returning to menu on connection timeout (`HandleDisconnect`)
* Handle requests to activate the respawn screen when the local character dies (`HandleRespawnScreen`)


## Inter-world communication

Sometimes, a client world needs to communicate with the default world, or the default world needs to communicate with the client/server worlds, etc... In this sample, we handle this via the following systems:
* `MoveLocalEntitiesToClientServerSystem`
* `MoveClientServerEntitiesToLocalSystem`

These systems look for entities that have tags such as `MoveToClientWorld`, `MoveToServerWorld` or `MoveToLocalWorld`, and handles moving the entity to the appropriate world(s).

This is used in several places in the sample, where we create entities as "events" or "requests" to do something:
* Sending a disconnection request from the default world (where the UI is) to the client/server worlds
* Sending a request to display respawn timer screen from the client world to the default world (where the UI is)
* etc...