using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Logging;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using Cursor = UnityEngine.Cursor;

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

        VisualElement m_MenuRootVisualElement;
        VisualElement m_ConnectionPanel;
        VisualElement m_ConnectingPanel;
        VisualElement m_InGamePanel;
        TextField m_IPField;
        TextField m_PortField;
        Button m_HostButton;
        Button m_JoinButton;
        Button m_DisconnectButton;
        TextField m_NameTextField;
        Toggle m_SpectatorToggle;
        Slider m_LookSensitivitySlider;
        VisualElement m_RespawnScreenRootVisualElement;
        Label m_RespawnMessageLabel;

        World m_NonGameWorld;
        bool m_MenuDisplayEnabled;
        MenuState m_MenuState;
        int m_SceneBuildIndexForSubscenesLoad = -1;

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
                    Destroy(Instance.gameObject);
                }

                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            m_MenuState = MenuState.InMenu;
            m_MenuDisplayEnabled = true;

            // Get UI elements
            {
                m_MenuRootVisualElement = MenuDocument.rootVisualElement;
                m_ConnectionPanel = m_MenuRootVisualElement.Q<VisualElement>(ElementName_ConnectionPanel);
                m_ConnectingPanel = m_MenuRootVisualElement.Q<VisualElement>(ElementName_ConnectingPanel);
                m_InGamePanel = m_MenuRootVisualElement.Q<VisualElement>(ElementName_InGamePanel);
                m_IPField = m_MenuRootVisualElement.Q<TextField>(ElementName_IPField);
                m_PortField = m_MenuRootVisualElement.Q<TextField>(ElementName_PortField);
                m_HostButton = m_MenuRootVisualElement.Q<Button>(ElementName_HostButton);
                m_JoinButton = m_MenuRootVisualElement.Q<Button>(ElementName_JoinButton);
                m_DisconnectButton = m_MenuRootVisualElement.Q<Button>(ElementName_DisconnectButton);
                m_NameTextField = m_MenuRootVisualElement.Q<TextField>(ElementName_NameTextField);
                m_SpectatorToggle = m_MenuRootVisualElement.Q<Toggle>(ElementName_SpectatorToggle);
                m_LookSensitivitySlider = m_MenuRootVisualElement.Q<Slider>(ElementName_LookSensitivitySlider);
                m_RespawnScreenRootVisualElement = RespawnScreenDocument.rootVisualElement;
                m_RespawnMessageLabel = m_RespawnScreenRootVisualElement.Q<Label>(ElementName_RespawnMessageLabel);
            }

            // Default data
            m_IPField.SetValueWithoutNotify(DefaultIP);
            m_PortField.SetValueWithoutNotify(DefaultPort.ToString());
            m_NameTextField.SetValueWithoutNotify("Player");
            m_LookSensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity);

            // Events
            m_HostButton.RegisterCallback<ClickEvent>(OnHostButton);
            m_JoinButton.RegisterCallback<ClickEvent>(OnJoinButton);
            m_DisconnectButton.RegisterCallback<ClickEvent>(OnDisconnectButton);
            m_LookSensitivitySlider.RegisterValueChangedCallback(OnLookSensitivitySlider);

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
                                m_NameTextField.value, m_SpectatorToggle.value);
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
                GameSession = GameSession.CreateLocalSession(m_NameTextField.value, false);
            }
#endif
        }

        void OnDestroy()
        {
            VFXReferences.SparksRequestsBuffer?.Dispose();
            VFXReferences.ExplosionsRequestsBuffer?.Dispose();
        }

        void Update()
        {
            // Toggle menu visibility in game
            if (m_MenuState == MenuState.InGame)
            {
                if (GameInput.InputActions.DefaultMap.ToggleMenu.WasPressedThisFrame())
                {
                    m_MenuDisplayEnabled = !m_MenuDisplayEnabled;
                    m_MenuRootVisualElement.SetDisplay(m_MenuDisplayEnabled);
                    if (m_MenuDisplayEnabled)
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

        void OnHostButton(ClickEvent evt)
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
                if (m_NonGameWorld != null && m_NonGameWorld.IsCreated)
                {
                    m_NonGameWorld?.Dispose();
                }

                m_NonGameWorld = null;

                StartHostGame();
            }
        }

        void OnJoinButton(ClickEvent evt)
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
                if (m_NonGameWorld != null && m_NonGameWorld.IsCreated)
                {
                    m_NonGameWorld?.Dispose();
                }

                m_NonGameWorld = null;

                StartJoinGame();
            }
        }

        void OnDisconnectButton(ClickEvent evt)
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
                if (m_NonGameWorld != null && m_NonGameWorld.IsCreated)
                {
                    m_NonGameWorld?.Dispose();
                }

                m_NonGameWorld = null;

                EndSessionAndReturnToMenu();
            }
        }

        void StartHostGame()
        {
            int numThinClients = 0;
#if UNITY_EDITOR
            numThinClients = ClientServerBootstrap.RequestedNumThinClients;
#endif

            TryGetIPAndPort(out string ip, out ushort port);
            GameSession = GameSession.CreateClientServerSession(ip, port, numThinClients,
                m_NameTextField.value, m_SpectatorToggle.value);
            GameSession.LoadSubsceneInAllGameWorlds(GameResourcesSubscene);
            LoadGameScene();
        }

        void StartJoinGame()
        {
            TryGetIPAndPort(out string ip, out ushort port);
            GameSession = GameSession.CreateClientSession(ip, port, m_NameTextField.value, m_SpectatorToggle.value);
            GameSession.LoadSubsceneInAllGameWorlds(GameResourcesSubscene);
            LoadGameScene();
        }

        public void EndSessionAndReturnToMenu()
        {
            GameSession = null;
            GameSession = GameSession.CreateLocalSession(m_NameTextField.value, false);
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

        void LoadMenuScene()
        {
            SceneManager.LoadScene(MenuScene.CachedBuildIndex, LoadSceneMode.Single);
        }

        void LoadGameScene()
        {
            m_SceneBuildIndexForSubscenesLoad = GameScene.CachedBuildIndex;
            SceneManager.LoadScene(GameScene.CachedBuildIndex, LoadSceneMode.Single);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == m_SceneBuildIndexForSubscenesLoad)
            {
                GameSession?.CreateSubscenesLoadRequest();

                m_SceneBuildIndexForSubscenesLoad = -1;
            }
        }

        public void SetMenuState(MenuState state)
        {
            m_MenuState = state;
            switch (m_MenuState)
            {
                case MenuState.InMenu:
                {
                    m_MenuDisplayEnabled = true;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    m_ConnectionPanel.SetDisplay(true);
                    m_ConnectingPanel.SetDisplay(false);
                    m_InGamePanel.SetDisplay(false);
                    SetCrosshairActive(false);
                    SetRespawnScreenActive(false);
                    break;
                }
                case MenuState.Connecting:
                {
                    m_MenuDisplayEnabled = true;
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    m_ConnectionPanel.SetDisplay(false);
                    m_ConnectingPanel.SetDisplay(true);
                    m_InGamePanel.SetDisplay(false);
                    SetCrosshairActive(false);
                    SetRespawnScreenActive(false);
                    break;
                }
                case MenuState.InGame:
                {
                    m_MenuDisplayEnabled = false;
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    m_ConnectionPanel.SetDisplay(false);
                    m_ConnectingPanel.SetDisplay(false);
                    m_InGamePanel.SetDisplay(true);
                    SetCrosshairActive(true);
                    SetRespawnScreenActive(false);
                    break;
                }
            }

            m_MenuRootVisualElement.SetDisplay(m_MenuDisplayEnabled);
        }

        void OnLookSensitivitySlider(ChangeEvent<float> value)
        {
            GameSettings.LookSensitivity = value.newValue;
        }

        public void SetCrosshairActive(bool active)
        {
            CrosshairDocument.enabled = active;
        }

        public void SetRespawnScreenActive(bool active)
        {
            m_RespawnScreenRootVisualElement.SetDisplay(active);
        }

        public void SetRespawnScreenTimer(float time)
        {
            m_RespawnMessageLabel.text = $"Respawning in {((int)math.ceil(time))}...";
        }

        bool TryGetIPAndPort(out string ip, out ushort port)
        {
            ip = m_IPField.value;
            if (!ushort.TryParse(m_PortField.value, out port))
            {
                Log.Error($"Error: couldn't get valid port: {m_PortField.value}");
                return false;
            }

            return true;
        }
    }
}