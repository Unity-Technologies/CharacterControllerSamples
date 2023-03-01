
# OnlineFPS Sample - Input and Player Commands

Character input handling in this sample is structured in a very similar way to the first-person standard character, but with a few key differences:
* Inputs are stored in a `FirstPersonPlayerCommands` component, which implements `IInputComponentData` (see [NetCode Documentation](https://docs.unity3d.com/Packages/com.unity.netcode@latest/index.html?subfolder=/manual/command-stream.html) on player commands)
* `FirstPersonPlayerInputsSystem` updates in `GhostInputSystemGroup`, and writes to `FirstPersonPlayerCommands`
* `FirstPersonPlayerVariableStepControlSystem` and `FirstPersonPlayerFixedStepControlSystem` update in the `PredictedSimulationSystemGroup` and `PredictedFixedStepSimulationSystemGroup` respectively, because they handle getting the commands at the prediction tick and applying them to the character. They are therefore part of prediction.
* Camera "look" input (`FirstPersonPlayerCommands.LookInputDelta`) has special handling in `FirstPersonPlayerInputsSystem`. See the next section for details


## Variable-rate camera movement in prediction

Instead of setting the `FirstPersonPlayerCommands.LookInputDelta` based on the raw input vector every frame in `FirstPersonPlayerInputsSystem`, we "accumulate" it every frame as long as we haven't reached a new tick. We do this because:
* Camera look movement must be part of prediction
* We want camera look movement to be processed at a variable update
* The server always updates at a fixed rate

In this situation, we have camera movement logic that must yield the exact same outcome on client & server (due to prediction), but this movement logic is updated at different rates on client and server. "Accumulating" the look input delta for every tick is our strategy to tackle this situation. On clients, when camera rotation updates at a variable rate in the prediction group, camera rotation will first be reset to the state it had at the beginning of the current tick, and then we will rotate using all of the accumulated rotation input that happened on this tick so far. On the server, where camera rotation always updates at a fixed rate, we will be using the total accumulated rotation input for the simulated tick. In both cases, the simulation will result in the same rotation, even though they are updated at different rates.