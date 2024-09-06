using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Logging;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using Cursor = UnityEngine.Cursor;
using Object = UnityEngine.Object;

namespace OnlineFPS
{
    public enum MenuState
    {
        InMenu,
        Connecting,
        InGame,
    }

    public class GameManager : MonoBehaviour
    {
        [Header("Prefabs")] public GameObject NameTagPrefab;

        [Header("UI")] public UIDocument MenuDocument;
        public UIDocument CrosshairDocument;
        public UIDocument RespawnScreenDocument;

        [Header("Scenes")] public WeakObjectSceneReference GameResourcesSubscene;

        //public CachedSubSceneReference GameResourcesSubscene;
        public CachedGameObjectSceneReference MenuScene;
        public CachedGameObjectSceneReference GameScene;

        [Header("Network")] public string DefaultIP = "127.0.0.1";
        public ushort DefaultPort = 7777;

        [Header("VFX")] public VisualEffect SparksGraph;
        public VisualEffect ExplosionsGraph;

        // ------------------

        public static GameManager Instance;

        public GameSession GameSession;

        private VisualElement _menuRootVisualElement;
        private VisualElement _ConnectionPanel;
        private VisualElement _ConnectingPanel;
        private VisualElement _InGamePanel;
        private TextField _IPField;
        private TextField _PortField;
        private Button _HostButton;
        private Button _JoinButton;
        private Button _DisconnectButton;
        private TextField _NameTextField;
        private Toggle _SpectatorToggle;
        private Slider _LookSensitivitySlider;
        private VisualElement _respawnScreenRootVisualElement;
        private Label _RespawnMessageLabel;

        private World _nonGameWorld;
        private bool _menuDisplayEnabled;
        private MenuState _menuState;
        private int _sceneBuildIndexForSubscenesLoad = -1;

        public const string ElementName_ConnectionPanel = "ConnectionPanel";
        public const string ElementName_ConnectingPanel = "ConnectingPanel";
        public const string ElementName_InGamePanel = "InGamePanel";
        public const string ElementName_IPField = "IPField";
        public const string ElementName_PortField = "PortField";
        public const string ElementName_HostButton = "HostButton";
        public const string ElementName_JoinButton = "JoinButton";
        public const string ElementName_DisconnectButton = "DisconnectButton";
        public const string ElementName_NameTextField = "NameTextField";
        public const string ElementName_SpectatorToggle = "SpectatorToggle";
        public const string ElementName_LookSensitivitySlider = "LookSensitivitySlider";
        public const string ElementName_RespawnMessageLabel = "RespawnMessageLabel";

        public const int SparksCapacity = 3000;
        public const int ExplosionsCapacity = 1000;

        public void OnValidate()
        {
            MenuScene.CacheData();
            GameScene.CacheData();
        }

        // Called by game bootstrap
        public void OnInitialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Singleton
            {
                if (Instance != null)
                {
                    GameObject.Destroy(Instance.gameObject);
                }

                Instance = this;
                GameObject.DontDestroyOnLoad(this.gameObject);
            }

            GameInput.Initialize();

            _menuState = MenuState.InMenu;
            _menuDisplayEnabled = true;

            // Get UI elements
            {
                _menuRootVisualElement = MenuDocument.rootVisualElement;
                _ConnectionPanel = _menuRootVisualElement.Q<VisualElement>(ElementName_ConnectionPanel);
                _ConnectingPanel = _menuRootVisualElement.Q<VisualElement>(ElementName_ConnectingPanel);
                _InGamePanel = _menuRootVisualElement.Q<VisualElement>(ElementName_InGamePanel);
                _IPField = _menuRootVisualElement.Q<TextField>(ElementName_IPField);
                _PortField = _menuRootVisualElement.Q<TextField>(ElementName_PortField);
                _HostButton = _menuRootVisualElement.Q<Button>(ElementName_HostButton);
                _JoinButton = _menuRootVisualElement.Q<Button>(ElementName_JoinButton);
                _DisconnectButton = _menuRootVisualElement.Q<Button>(ElementName_DisconnectButton);
                _NameTextField = _menuRootVisualElement.Q<TextField>(ElementName_NameTextField);
                _SpectatorToggle = _menuRootVisualElement.Q<Toggle>(ElementName_SpectatorToggle);
                _LookSensitivitySlider = _menuRootVisualElement.Q<Slider>(ElementName_LookSensitivitySlider);
                _respawnScreenRootVisualElement = RespawnScreenDocument.rootVisualElement;
                _RespawnMessageLabel = _respawnScreenRootVisualElement.Q<Label>(ElementName_RespawnMessageLabel);
            }

            // Default data
            _IPField.SetValueWithoutNotify(DefaultIP);
            _PortField.SetValueWithoutNotify(DefaultPort.ToString());
            _NameTextField.SetValueWithoutNotify("Player");
            _LookSensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity);

            // Events
            _HostButton.RegisterCallback<ClickEvent>(OnHostButton);
            _JoinButton.RegisterCallback<ClickEvent>(OnJoinButton);
            _DisconnectButton.RegisterCallback<ClickEvent>(OnDisconnectButton);
            _LookSensitivitySlider.RegisterValueChangedCallback(OnLookSensitivitySlider);

            // VFX
            VFXReferences.SparksGraph = SparksGraph;
            VFXReferences.ExplosionsGraph = ExplosionsGraph;
            VFXReferences.SparksRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SparksCapacity,
                Marshal.SizeOf(typeof(VFXSparksRequest)));
            VFXReferences.ExplosionsRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                ExplosionsCapacity,
                Marshal.SizeOf(typeof(VFXExplosionRequest)));

            SetMenuState(MenuState.InMenu);

            // Start a tmp server just once so we can get a firewall prompt when running the game for the first time
            {
                NetworkDriver tmpNetDriver = NetworkDriver.Create();
                NetworkEndpoint tmpEndPoint = NetworkEndpoint.Parse("127.0.0.1", 7777);
                if (tmpNetDriver.Bind(tmpEndPoint) == 0)
                {
                    tmpNetDriver.Listen();
                }

                tmpNetDriver.Dispose();
            }

#if UNITY_SERVER
        // Auto server
        StartServerOnly(DefaultIP, DefaultPort);
#else
            if (ShouldAutoPlayNetcode(out AutoNetcodePlayMode autoNetcodePlayMode))
            {
#if UNITY_EDITOR
                ClientServerBootstrap.PlayType requestedPlayType = GameBootstrap.RequestedPlayType;
                switch (requestedPlayType)
                {
                    case ClientServerBootstrap.PlayType.ClientAndServer:
                        GameSession =
                            GameSession.CreateClientServerSession(autoNetcodePlayMode.IP, autoNetcodePlayMode.Port,
                                ClientServerBootstrap.RequestedNumThinClients, "Player", false);
                        break;
                    case ClientServerBootstrap.PlayType.Client:
                        GameSession =
                            GameSession.CreateClientSession(autoNetcodePlayMode.IP, autoNetcodePlayMode.Port,
                                _NameTextField.value, _SpectatorToggle.value);
                        break;
                    case ClientServerBootstrap.PlayType.Server:
                        GameSession = GameSession.CreateServerSession(autoNetcodePlayMode.IP, autoNetcodePlayMode.Port,
                            ClientServerBootstrap.RequestedNumThinClients);
                        break;
                }

                GameSession.LoadSubsceneInAllGameWorlds(GameResourcesSubscene);
                GameSession.CreateSubscenesLoadRequest();
#endif
            }
            else
            {
                GameSession = GameSession.CreateLocalSession(_NameTextField.value, false);
            }
#endif
        }

        private void OnDestroy()
        {
            VFXReferences.SparksRequestsBuffer?.Dispose();
            VFXReferences.ExplosionsRequestsBuffer?.Dispose();
        }

        void Update()
        {
            // Toggle menu visibility in game
            if (_menuState == MenuState.InGame)
            {
                if (GameInput.InputActions.DefaultMap.ToggleMenu.WasPressedThisFrame())
                {
                    _menuDisplayEnabled = !_menuDisplayEnabled;
                    _menuRootVisualElement.SetDisplay(_menuDisplayEnabled);
                    if (_menuDisplayEnabled)
                    {
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }
                    else
                    {
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                    }
                }
            }

            GameSession?.Update();
        }

        private void OnHostButton(ClickEvent evt)
        {
            if (GameSession != null)
            {
                GameSession.OnAllDisconnected -= GameSession.DestroyAll;
                GameSession.OnAllDisconnected += GameSession.DestroyAll;
                GameSession.OnAllDestroyed -= StartHostGame;
                GameSession.OnAllDestroyed += StartHostGame;
                GameSession.DisconnectAll();
            }
            else
            {
                if (_nonGameWorld != null && _nonGameWorld.IsCreated)
                {
                    _nonGameWorld?.Dispose();
                }

                _nonGameWorld = null;

                StartHostGame();
            }
        }

        private void OnJoinButton(ClickEvent evt)
        {
            if (GameSession != null)
            {
                GameSession.OnAllDisconnected -= GameSession.DestroyAll;
                GameSession.OnAllDisconnected += GameSession.DestroyAll;
                GameSession.OnAllDestroyed -= StartJoinGame;
                GameSession.OnAllDestroyed += StartJoinGame;
                GameSession.DisconnectAll();
            }
            else
            {
                if (_nonGameWorld != null && _nonGameWorld.IsCreated)
                {
                    _nonGameWorld?.Dispose();
                }

                _nonGameWorld = null;

                StartJoinGame();
            }
        }

        private void OnDisconnectButton(ClickEvent evt)
        {
            if (GameSession != null)
            {
                GameSession.OnAllDisconnected -= GameSession.DestroyAll;
                GameSession.OnAllDisconnected += GameSession.DestroyAll;
                GameSession.OnAllDestroyed -= EndSessionAndReturnToMenu;
                GameSession.OnAllDestroyed += EndSessionAndReturnToMenu;
                GameSession.DisconnectAll();
            }
            else
            {
                if (_nonGameWorld != null && _nonGameWorld.IsCreated)
                {
                    _nonGameWorld?.Dispose();
                }

                _nonGameWorld = null;

                EndSessionAndReturnToMenu();
            }
        }

        private void StartHostGame()
        {
            int numThinClients = 0;
#if UNITY_EDITOR
            numThinClients = ClientServerBootstrap.RequestedNumThinClients;
#endif

            TryGetIPAndPort(out string ip, out ushort port);
            GameSession = GameSession.CreateClientServerSession(ip, port, numThinClients,
                _NameTextField.value, _SpectatorToggle.value);
            GameSession.LoadSubsceneInAllGameWorlds(GameResourcesSubscene);
            LoadGameScene();
        }

        private void StartJoinGame()
        {
            TryGetIPAndPort(out string ip, out ushort port);
            GameSession = GameSession.CreateClientSession(ip, port, _NameTextField.value, _SpectatorToggle.value);
            GameSession.LoadSubsceneInAllGameWorlds(GameResourcesSubscene);
            LoadGameScene();
        }

        public void EndSessionAndReturnToMenu()
        {
            GameSession = null;
            GameSession = GameSession.CreateLocalSession(_NameTextField.value, false);
            SetMenuState(MenuState.InMenu);
            LoadMenuScene();
        }

        public bool ShouldAutoPlayNetcode(out AutoNetcodePlayMode autoNetcodePlayMode)
        {
            autoNetcodePlayMode = null;

#if UNITY_EDITOR
            GameObject[] sceneRootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < sceneRootObjects.Length; i++)
            {
                autoNetcodePlayMode = sceneRootObjects[i].GetComponent<AutoNetcodePlayMode>();
                if (autoNetcodePlayMode != null && autoNetcodePlayMode.gameObject.activeSelf &&
                    autoNetcodePlayMode.enabled)
                {
                    break;
                }
            }
#endif

            return autoNetcodePlayMode != null;
        }

        private void LoadMenuScene()
        {
            SceneManager.LoadScene(MenuScene.CachedBuildIndex, LoadSceneMode.Single);
        }

        private void LoadGameScene()
        {
            _sceneBuildIndexForSubscenesLoad = GameScene.CachedBuildIndex;
            SceneManager.LoadScene(GameScene.CachedBuildIndex, LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == _sceneBuildIndexForSubscenesLoad)
            {
                GameSession?.CreateSubscenesLoadRequest();

                _sceneBuildIndexForSubscenesLoad = -1;
            }
        }

        public void SetMenuState(MenuState state)
        {
            _menuState = state;
            switch (_menuState)
            {
                case MenuState.InMenu:
                {
                    _menuDisplayEnabled = true;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    _ConnectionPanel.SetDisplay(true);
                    _ConnectingPanel.SetDisplay(false);
                    _InGamePanel.SetDisplay(false);
                    SetCrosshairActive(false);
                    SetRespawnScreenActive(false);
                    break;
                }
                case MenuState.Connecting:
                {
                    _menuDisplayEnabled = true;
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _ConnectionPanel.SetDisplay(false);
                    _ConnectingPanel.SetDisplay(true);
                    _InGamePanel.SetDisplay(false);
                    SetCrosshairActive(false);
                    SetRespawnScreenActive(false);
                    break;
                }
                case MenuState.InGame:
                {
                    _menuDisplayEnabled = false;
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _ConnectionPanel.SetDisplay(false);
                    _ConnectingPanel.SetDisplay(false);
                    _InGamePanel.SetDisplay(true);
                    SetCrosshairActive(true);
                    SetRespawnScreenActive(false);
                    break;
                }
            }

            _menuRootVisualElement.SetDisplay(_menuDisplayEnabled);
        }

        private void OnLookSensitivitySlider(ChangeEvent<float> value)
        {
            GameSettings.LookSensitivity = value.newValue;
        }

        public void SetCrosshairActive(bool active)
        {
            CrosshairDocument.enabled = active;
        }

        public void SetRespawnScreenActive(bool active)
        {
            _respawnScreenRootVisualElement.SetDisplay(active);
        }

        public void SetRespawnScreenTimer(float time)
        {
            _RespawnMessageLabel.text = $"Respawning in {((int)math.ceil(time))}...";
        }

        private bool TryGetIPAndPort(out string ip, out ushort port)
        {
            ip = _IPField.value;
            if (!ushort.TryParse(_PortField.value, out port))
            {
                Log.Error($"Error: couldn't get valid port: {_PortField.value}");
                return false;
            }

            return true;
        }
    }
}