using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using TMPro;
using Unity.Mathematics;
using Unity.Transforms;

public enum MenuState
{
    InMenu,
    Connecting,
    InGame,
}

public struct CrosshairRequest : IComponentData
{
    public bool Enable;
}

public struct RespawnMessageRequest : IRpcCommand
{
    public bool Start;
    public float CountdownTime;
}

public static class VisualElementExtensions
{
    public static void SetDisplay(this VisualElement element, bool enabled)
    {
        element.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
public partial class GameUISystem : SystemBase
{
    private FPSInputActions InputActions;

    private UIDocument MenuDocument;
    private UIDocument CrosshairDocument;
    private UIDocument RespawnScreenDocument;
    
    private MenuState LastKnownMenuState;
    private float RespawnCounter = -1f;
    private int PreviousRespawnCounterValue = -1;
    
    private VisualElement MainPanel;
    private VisualElement ConnectionPanel;
    private VisualElement ConnectingPanel;
    private VisualElement InGamePanel;
    private Button JoinButton;
    private Button HostButton;
    private Button DisconnectButton;
    private TextField NameTextField;
    private TextField JoinIPTextField;
    private TextField JoinPortTextField;
    private TextField HostPortTextField;
    private Toggle SpectatorToggle;
    private Slider LookSensitivitySlider;
    private Label RespawnMessageLabel;
    
    private const string UIName_MainPanel = "MainPanel";
    private const string UIName_ConnectionPanel = "ConnectionPanel";
    private const string UIName_ConnectingPanel = "ConnectingPanel";
    private const string UIName_InGamePanel = "InGamePanel";
    private const string UIName_JoinButton = "JoinButton";
    private const string UIName_HostButton = "HostButton";
    private const string UIName_DisconnectButton = "DisconnectButton";
    private const string UIName_NameTextField = "NameTextField";
    private const string UIName_JoinIPTextField = "JoinIPTextField";
    private const string UIName_JoinPortTextField = "JoinPortTextField";
    private const string UIName_HostPortTextField = "HostPortTextField";
    private const string UIName_SpectatorToggle = "SpectatorToggle";
    private const string UIName_LookSensitivitySlider = "LookSensitivitySlider";
    private const string UIName_RespawnMessageLabel = "RespawnMessageLabel";

    protected override void OnStartRunning()
    {
        base.OnStartRunning();

        InputActions = new FPSInputActions();
        InputActions.Enable();
        InputActions.DefaultMap.Enable();
    }
    
    public void SetUIReferences(UIReferences references)
    {
        MenuDocument = references.MenuDocument;
        CrosshairDocument = references.CrosshairDocument;
        RespawnScreenDocument = references.RespawnScreenDocument;
        
        // Get element refs
        MainPanel = MenuDocument.rootVisualElement.Q<VisualElement>(UIName_MainPanel);
        ConnectionPanel = MenuDocument.rootVisualElement.Q<VisualElement>(UIName_ConnectionPanel);
        ConnectingPanel = MenuDocument.rootVisualElement.Q<VisualElement>(UIName_ConnectingPanel);
        InGamePanel = MenuDocument.rootVisualElement.Q<VisualElement>(UIName_InGamePanel);
        JoinButton = MenuDocument.rootVisualElement.Q<Button>(UIName_JoinButton);
        HostButton = MenuDocument.rootVisualElement.Q<Button>(UIName_HostButton);
        DisconnectButton = MenuDocument.rootVisualElement.Q<Button>(UIName_DisconnectButton);
        NameTextField = MenuDocument.rootVisualElement.Q<TextField>(UIName_NameTextField);
        JoinIPTextField = MenuDocument.rootVisualElement.Q<TextField>(UIName_JoinIPTextField);
        JoinPortTextField = MenuDocument.rootVisualElement.Q<TextField>(UIName_JoinPortTextField);
        HostPortTextField = MenuDocument.rootVisualElement.Q<TextField>(UIName_HostPortTextField);
        SpectatorToggle = MenuDocument.rootVisualElement.Q<Toggle>(UIName_SpectatorToggle);
        LookSensitivitySlider = MenuDocument.rootVisualElement.Q<Slider>(UIName_LookSensitivitySlider);
        
        // Subscribe events
        JoinButton.clicked += JoinButtonPressed;
        HostButton.clicked += HostButtonPressed;
        DisconnectButton.clicked += DisconnectButtonPressed;
        LookSensitivitySlider.RegisterValueChangedCallback(LookSensitivitySliderChanged);
        
        // Initial state
        LookSensitivitySlider.value = GameSettings.LookSensitivity;
        LastKnownMenuState = MenuState.InMenu;
        JoinIPTextField.value = references.InitialJoinAddress;
        SetState(LastKnownMenuState);
        
        // Start with crosshair disabled
        CrosshairDocument.enabled = false;
        RespawnScreenDocument.enabled = false;

        // When launched in server mode, auto-host
#if UNITY_SERVER
        HostButtonPressed();
#endif
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(World.Unmanaged);
        FPSInputActions.DefaultMapActions inputsMap = InputActions.DefaultMap;
        GameManagementSystem.Singleton gameManagementSingleton = SystemAPI.GetSingleton<GameManagementSystem.Singleton>();

        HandleMenuStateChanges(inputsMap, gameManagementSingleton);
        HandleCrosshair(ref ecb);
        HandleRespawnMessage(ref ecb);
    }

    private void HandleMenuStateChanges(FPSInputActions.DefaultMapActions inputsMap, GameManagementSystem.Singleton gameManagementSingleton)
    {
        // Check for state changes
        if (gameManagementSingleton.MenuState != LastKnownMenuState)
        {
            SetState(gameManagementSingleton.MenuState);
            
            if (gameManagementSingleton.MenuState == MenuState.InGame)
            {
                // Set invisible the first time we enter play
                SetVisibleRecursive(MainPanel, false, gameManagementSingleton.MenuState);
            }
            if (gameManagementSingleton.MenuState == MenuState.InMenu)
            {
                // Set visible when we enter menu
                SetVisibleRecursive(MainPanel, true, gameManagementSingleton.MenuState);

                RespawnCounter = -1f;
                CrosshairDocument.enabled = false;
                RespawnScreenDocument.enabled = false;
            }
            
            LastKnownMenuState = gameManagementSingleton.MenuState;
        }

        // Toggle visibility
        if (inputsMap.ToggleMenu.WasPressedThisFrame())
        {
            SetVisibleRecursive(MainPanel, !MainPanel.enabledSelf, gameManagementSingleton.MenuState);
        }
    }

    private void HandleCrosshair(ref EntityCommandBuffer ecb)
    {
        foreach (var (crosshairRequest, entity) in SystemAPI.Query<CrosshairRequest>().WithEntityAccess())
        {
            CrosshairDocument.enabled = crosshairRequest.Enable;
            ecb.DestroyEntity(entity);
        }
    }

    private void HandleRespawnMessage(ref EntityCommandBuffer ecb)
    {
        foreach (var (respawnRequest, entity) in SystemAPI.Query<RespawnMessageRequest>().WithEntityAccess())
        {
            if (respawnRequest.Start)
            {
                RespawnScreenDocument.enabled = true;
                
                // Must get the label reference whenever the document is re-enabled
                RespawnMessageLabel = RespawnScreenDocument.rootVisualElement.Q<Label>(UIName_RespawnMessageLabel);
                
                RespawnCounter = respawnRequest.CountdownTime;
            }
            else
            {
                RespawnScreenDocument.enabled = false;
            }
            ecb.DestroyEntity(entity);
        }

        if (RespawnCounter >= 0f)
        {
            int respawnCounterValue = (int)math.ceil(RespawnCounter);
            if (respawnCounterValue != PreviousRespawnCounterValue)
            {
                RespawnMessageLabel.text = $"Respawning in {respawnCounterValue}...";
            }
            PreviousRespawnCounterValue = respawnCounterValue;
            RespawnCounter -= SystemAPI.Time.DeltaTime;
        }
    }
    
    private void SetState(MenuState state)
    {
        switch (state)
        {
            case MenuState.InMenu:
                ConnectionPanel.SetDisplay(true);
                ConnectingPanel.SetDisplay(false);
                InGamePanel.SetDisplay(false);
                break;
            case MenuState.Connecting:
                ConnectionPanel.SetDisplay(false);
                ConnectingPanel.SetDisplay(true);
                InGamePanel.SetDisplay(false);
                break;
            case MenuState.InGame:
                ConnectionPanel.SetDisplay(false);
                ConnectingPanel.SetDisplay(false);
                InGamePanel.SetDisplay(true);
                break;
        }
    }

    private void JoinButtonPressed()
    {
        if (ushort.TryParse(JoinPortTextField.text, out ushort port) && NetworkEndpoint.TryParse(JoinIPTextField.text, port, out NetworkEndpoint newEndPoint))
        {
            GameManagementSystem.JoinRequest joinRequest = new GameManagementSystem.JoinRequest
            {
                LocalPlayerName = new FixedString128Bytes(NameTextField.text),
                EndPoint = newEndPoint,
                Spectator = SpectatorToggle.value,
            };
            Entity joinRequestEntity = World.EntityManager.CreateEntity();
            World.EntityManager.AddComponentData(joinRequestEntity, joinRequest);
        }
        else
        {
            Debug.LogError("Unable to parse Join IP or Port fields");
        }
    }

    private void HostButtonPressed()
    {
        if (ushort.TryParse(HostPortTextField.text, out ushort port) && NetworkEndpoint.TryParse(GameManagementSystem.LocalHost, port, out NetworkEndpoint newLocalClientEndPoint))
        {
            NetworkEndpoint newServerEndPoint = NetworkEndpoint.AnyIpv4;
            newServerEndPoint.Port = port;
            GameManagementSystem.HostRequest hostRequest = new GameManagementSystem.HostRequest
            {
                EndPoint = newServerEndPoint,
            };
            Entity hostRequestEntity = World.EntityManager.CreateEntity();
            World.EntityManager.AddComponentData(hostRequestEntity, hostRequest);

            // Only create local client if not in server mode
#if !UNITY_SERVER
            GameManagementSystem.JoinRequest joinRequest = new GameManagementSystem.JoinRequest
            {
                LocalPlayerName = new FixedString128Bytes(NameTextField.text),
                EndPoint = newLocalClientEndPoint,
                Spectator = SpectatorToggle.value,
            };
            Entity joinRequestEntity = World.EntityManager.CreateEntity();
            World.EntityManager.AddComponentData(joinRequestEntity, joinRequest);
#endif
        }
        else
        {
            Debug.LogError("Unable to parse Host Port field");
        }
    }

    private void DisconnectButtonPressed()
    {
        Entity disconnectRequestEntity = World.EntityManager.CreateEntity();
        World.EntityManager.AddComponentData(disconnectRequestEntity, new GameManagementSystem.DisconnectRequest());
    }

    private void LookSensitivitySliderChanged(ChangeEvent<float> value)
    {
        GameSettings.LookSensitivity = value.newValue;
    }

    private void SetVisibleRecursive(VisualElement root, bool visible, MenuState menuState)
    {
        root.visible = visible;
        root.SetEnabled(visible);
        
        if(visible)
        {
            SetState(menuState);
        }

        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        
        foreach (var child in root.Children())
        {
            SetVisibleRecursive(child, visible, menuState);
        }
    }
}
