
# OnlineFPS Sample - Game Management

## Connection Menu
The game's main menu allows you to join or host a game. Here's how this menu is implemented:
* A `UI` gameObject is present in the scene, Under it, there is a `UIDocument` named `Menu` for the connection menu. This uses UI Toolkit
* The `UI` gameObject has a `UIReferences` monobehaviour on it. It holds references to UI documents
* On Start, the `UIReferences` monobehaviour gets the `GameUISystem` and calls `SetUIReferences` on it. This is what gives the `GameUISystem` the necessary UI references that it needs to find in the scene
* `GameUISystem` handles all UI logic. If finds all UI element references in various UI documents, and subscribes methods to UI events such as button clicks or value changes. Based on the state of the game, it also handles showing/hiding certain UI elements
    * Joining is handled by creating an entity with a `GameManagementSystem.JoinRequest` component in `GameUISystem.JoinButtonPressed`. These requests are processed by the `GameManagementSystem` later
    * Hosting is handled by creating an entity with a `GameManagementSystem.HostRequest` component in `GameUISystem.HostButtonPressed`. These requests are processed by the `GameManagementSystem` later


## Game Management

`GameManagementSystem` updates in the default GameObject world, and is mainly responsible for handling joining, hosting, and disconnecting. It creates/destroys server & client worlds as needed, loads the game scene, etc...

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