using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Utilities;
using UnityEngine;
using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class StaticNetworkManagerWindow : EditorWindow
    {
        private NetworkConfiguration NetworkConfiguration
        {
            get => StaticNetworkManager.NetworkConfiguration;
            set
            {
                if (StaticNetworkManager.NetworkConfiguration == value) return;
                
                _settingsEditor = null;
                StaticNetworkManager.NetworkConfiguration = value;
            }
        }

        private Editor _settingsEditor;
        public Editor SettingsEditor
        {
            get
            {
                if (_settingsEditor == null && NetworkConfiguration)
                    _settingsEditor = Editor.CreateEditor(NetworkConfiguration);
                return _settingsEditor;
            }
        }

        private Vector2 _scrollViewPosition = Vector2.zero;

        private NetworkManagerEditor _networkManagerEditor;

        private NetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                return _networkManagerEditor ??= new();
            } 
        }

        [MenuItem("Window/SimpleUnityNetworking/Network Manager")]
        public static void ShowWindow()
        {
            GetWindow(typeof(StaticNetworkManagerWindow), false, "Network Manager");
        }

        private void OnEnable()
        {
            NetworkManagerEditor.SubscribeNetworkEvents(StaticNetworkManager.Events, Repaint);
            NetworkConfiguration = NetworkConfiguration != null ? NetworkConfiguration : UnityUtilities.LoadOrCreateScriptableObject<NetworkConfiguration>("NetworkConfiguration", "Assets/Resources/");
        }

        private void OnDisable()
        {
            NetworkManagerEditor.UnsubscribeNetworkEvents(StaticNetworkManager.Events, Repaint);
        }

        private void OnGUI()
        {
            if (EditorApplication.isCompiling)
            {
                GUILayout.Label("The editor is compiling...", EditorStyles.largeLabel);
                return;
            }

            if (StaticNetworkManager.NetworkManager == null)
            {
                GUILayout.Label("The network manager is null. Can not show settings.", EditorStyles.largeLabel);
                return;
            }

            _scrollViewPosition = EditorGUILayout.BeginScrollView(_scrollViewPosition);
            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

            GUILayout.Label("Network Manager", EditorStyles.largeLabel);

            NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(StaticNetworkManager.NetworkConfiguration, typeof(NetworkConfiguration), false);

            if (NetworkConfiguration != null)
            {
                if (!StaticNetworkManager.IsServerDiscoveryActive)
                    StaticNetworkManager.StartServerDiscovery();
                
                EditorGUILayout.Space();

                SettingsEditor.OnInspectorGUI();

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                if (Application.isPlaying)
                    GUILayout.Label("Functionality is disabled in play mode.");

                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                NetworkManagerEditor.OnInspectorGUI(StaticNetworkManager.NetworkManager);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
    }
}
