using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using UnityEditor;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking
{
    public class StaticNetworkManagerWindow : EditorWindow
    {
        private const int PADDING = 10;

        private NetworkManager NetworkManager => StaticNetworkManager.NetworkManager;

        private INetworkManagerEditor _networkManagerEditor;
        private INetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                if (_networkManagerEditor != null)
                    return _networkManagerEditor;
                return _networkManagerEditor = new(
                    StaticNetworkManager.NetworkManager,
                    INetworkManagerEditor.EAllowStart.OnlyEditor
                );
            }
        }

        [SerializeField] private TransportConfiguration _cachedTransportConfiguration;
        public TransportConfiguration TransportConfiguration
        {
            get => _cachedTransportConfiguration;
            set
            {
                if (NetworkManager.TransportConfiguration == value) return;
                NetworkManager.TransportConfiguration = _cachedTransportConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedTransportConfiguration);
#endif
            }
        }
        [SerializeField] private SerialiserConfiguration _cachedSerialiserConfiguration;
        public SerialiserConfiguration SerialiserConfiguration
        {
            get => _cachedSerialiserConfiguration;
            set
            {
                if (NetworkManager.SerialiserConfiguration == value) return;
                NetworkManager.SerialiserConfiguration = _cachedSerialiserConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedSerialiserConfiguration);
#endif
            }
        }
        [SerializeField] private LoggerConfiguration _cachedLoggerConfiguration;
        public LoggerConfiguration LoggerConfiguration
        {
            get => _cachedLoggerConfiguration;
            set
            {
                if (NetworkManager.LoggerConfiguration == value) return;
                NetworkManager.LoggerConfiguration = _cachedLoggerConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedLoggerConfiguration);
#endif
            }
        }

        [SerializeField] private bool _showTransportWindow = true;
        [SerializeField] private bool _showSerialiserWindow = true;
        [SerializeField] private bool _showLoggerWindow = true;

        [MenuItem("Window/SimpleUnityNetworking/Static Network Manager")]
        public static void ShowWindow()
        {
            GetWindow(typeof(StaticNetworkManagerWindow), false, "Network Manager");
        }
        
        private void Awake()
        {
            NetworkManager.Client_OnRemoteClientConnected += RepaintOnUpdate;
            NetworkManager.Client_OnRemoteClientDisconnected += RepaintOnUpdate;
            NetworkManager.Client_OnRemoteClientUpdated += RepaintOnUpdate;
            NetworkManager.Client_OnLocalStateUpdated += RepaintOnUpdate;
            NetworkManager.Server_OnRemoteClientConnected += RepaintOnUpdate;
            NetworkManager.Server_OnRemoteClientDisconnected += RepaintOnUpdate;
            NetworkManager.Server_OnRemoteClientUpdated += RepaintOnUpdate;
            NetworkManager.Server_OnLocalStateUpdated += RepaintOnUpdate;
        }

        private void OnDestroy()
        {
            NetworkManager.Client_OnRemoteClientConnected -= RepaintOnUpdate;
            NetworkManager.Client_OnRemoteClientDisconnected -= RepaintOnUpdate;
            NetworkManager.Client_OnRemoteClientUpdated -= RepaintOnUpdate;
            NetworkManager.Client_OnLocalStateUpdated -= RepaintOnUpdate;
            NetworkManager.Server_OnRemoteClientConnected -= RepaintOnUpdate;
            NetworkManager.Server_OnRemoteClientDisconnected -= RepaintOnUpdate;
            NetworkManager.Server_OnRemoteClientUpdated -= RepaintOnUpdate;
            NetworkManager.Server_OnLocalStateUpdated -= RepaintOnUpdate;
        }

        private void RepaintOnUpdate(uint _) => Repaint();
        private void RepaintOnUpdate(ELocalClientConnectionState _) => Repaint();
        private void RepaintOnUpdate(ELocalServerConnectionState _) => Repaint();

        private void OnGUI()
        {
            var area = new Rect(PADDING, PADDING, position.width - PADDING * 2f, position.height - PADDING * 2f);

            GUILayout.BeginArea(area);
            GUILayout.Label("Static Network Manager", EditorStyles.largeLabel);

            EditorGUILayout.Space();
            GUILayout.Label("Configurations:", EditorStyles.boldLabel);
            {
                TransportGUI();
                SerialiserGUI();
                LoggerGUI();
            }

            NetworkManagerEditor.ManagerGUIs();
            GUILayout.EndArea();
        }

        private void TransportGUI()
        {
            TransportConfiguration = NetworkManagerEditor.ConfigurationGUI<TransportConfiguration>(_cachedTransportConfiguration, "Transport", ref _showTransportWindow);
        }

        private void SerialiserGUI()
        {
            SerialiserConfiguration = NetworkManagerEditor.ConfigurationGUI<SerialiserConfiguration>(_cachedSerialiserConfiguration, "Serialiser", ref _showSerialiserWindow);
        }

        private void LoggerGUI()
        {
            LoggerConfiguration = NetworkManagerEditor.ConfigurationGUI<LoggerConfiguration>(_cachedLoggerConfiguration, "Logger", ref _showLoggerWindow);
        }
    }
}
