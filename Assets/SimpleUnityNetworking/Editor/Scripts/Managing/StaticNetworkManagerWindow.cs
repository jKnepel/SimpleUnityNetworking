using UnityEngine;
using UnityEditor;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Utilities;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class StaticNetworkManagerWindow : EditorWindow
    {
        [SerializeField] private NetworkConfiguration _cachedNetworkConfiguration = null;
        private NetworkConfiguration NetworkConfiguration
        {
            get => StaticNetworkManager.NetworkConfiguration;
            set
            {
                if (_cachedNetworkConfiguration != value)
                    _settingsEditor = null;

                _cachedNetworkConfiguration = value;
                StaticNetworkManager.NetworkConfiguration = _cachedNetworkConfiguration;
            }
        }

        private Editor _settingsEditor = null;
        public Editor SettingsEditor
        {
            get
            {
                if (_settingsEditor is null && NetworkConfiguration)
                    _settingsEditor = Editor.CreateEditor(NetworkConfiguration);
                return _settingsEditor;
            }
        }

        private Vector2 _scrollViewPosition = Vector2.zero;

        private NetworkManagerEditor _networkManagerEditor = null;
        private NetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                if (_networkManagerEditor == null)
                    _networkManagerEditor = new();
                return _networkManagerEditor;
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
        }

        private void OnDisable()
        {
            NetworkManagerEditor.UnsubscribeNetworkEvents(StaticNetworkManager.Events, Repaint);
        }

        private void OnGUI()
        {
            if (EditorApplication.isCompiling)
            {
                GUILayout.Label("The editor is compiling...\nSettings will show up after recompile.", EditorStyles.largeLabel);
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

            if (!StaticNetworkManager.IsConnected)
                NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(_cachedNetworkConfiguration, typeof(NetworkConfiguration), false) ??
                    UnityUtilities.LoadOrCreateScriptableObject<NetworkConfiguration>("NetworkConfiguration", "Assets/Resources/");

            if (NetworkConfiguration is not null)
                StaticNetworkManager.StartServerDiscovery();
            
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Randomize User Information"), GUILayout.ExpandWidth(false)))
            {
                NetworkConfiguration.Username = $"User_{Random.Range(0, 100)}";
                NetworkConfiguration.Color = new Color(Random.value, Random.value, Random.value);
            }

            SettingsEditor?.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (Application.isPlaying)
                GUILayout.Label("Functionality is disabled in play mode.");

            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            NetworkManagerEditor.OnInspectorGUI(StaticNetworkManager.NetworkManager);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
    }
}
