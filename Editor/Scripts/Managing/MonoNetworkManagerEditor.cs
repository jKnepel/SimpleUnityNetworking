using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    [CustomEditor(typeof(MonoNetworkManager))]
    internal class MonoNetworkManagerEditor : Editor
    {
        private MonoNetworkManager NetworkManager => (MonoNetworkManager)target;

        private INetworkManagerEditor _networkManagerEditor;
        private INetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                if (_networkManagerEditor != null)
                    return _networkManagerEditor;
                return _networkManagerEditor = new(
                    (MonoNetworkManager)target,
                    Repaint,
                    INetworkManagerEditor.EAllowStart.OnlyPlaymode
                );
            }
        }

        public TransportConfiguration TransportConfiguration
        {
            get => NetworkManager.TransportConfiguration;
            set
            {
                if (NetworkManager.TransportConfiguration == value) return;
                NetworkManager.TransportConfiguration = value;

#if UNITY_EDITOR
                if (!EditorApplication.isPlaying)
                    EditorSceneManager.MarkSceneDirty(NetworkManager.gameObject.scene);
#endif
            }
        }
        public SerialiserConfiguration SerialiserConfiguration
        {
            get => NetworkManager.SerialiserConfiguration;
            set
            {
                if (NetworkManager.SerialiserConfiguration == value) return;
                NetworkManager.SerialiserConfiguration = value;

#if UNITY_EDITOR
                if (!EditorApplication.isPlaying)
                    EditorSceneManager.MarkSceneDirty(NetworkManager.gameObject.scene);
#endif
            }
        }
        public LoggerConfiguration LoggerConfiguration
        {
            get => NetworkManager.LoggerConfiguration;
            set
            {
                if (NetworkManager.LoggerConfiguration == value) return;
                NetworkManager.LoggerConfiguration = value;

#if UNITY_EDITOR
                if (!EditorApplication.isPlaying)
                    EditorSceneManager.MarkSceneDirty(NetworkManager.gameObject.scene);
#endif
            }
        }

        [SerializeField] private bool _showTransportWindow = true;
        [SerializeField] private bool _showSerialiserWindow = true;
        [SerializeField] private bool _showLoggerWindow = true;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Configurations:", EditorStyles.boldLabel);
            {
                TransportGUI();
                SerialiserGUI();
                LoggerGUI();
            }

            NetworkManagerEditor.ManagerGUIs();

            serializedObject.ApplyModifiedProperties();
        }

        private void TransportGUI()
        {
            TransportConfiguration = NetworkManagerEditor.ConfigurationGUI<UnityTransportConfiguration>(TransportConfiguration, "Transport", ref _showTransportWindow);
        }

        private void SerialiserGUI()
        {
            SerialiserConfiguration = NetworkManagerEditor.ConfigurationGUI<SerialiserConfiguration>(SerialiserConfiguration, "Serialiser", ref _showSerialiserWindow);
        }

        private void LoggerGUI()
        {
            LoggerConfiguration = NetworkManagerEditor.ConfigurationGUI<LoggerConfiguration>(LoggerConfiguration, "Logger", ref _showLoggerWindow);
        }
    }
}
