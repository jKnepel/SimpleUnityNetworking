using UnityEngine;
using UnityEditor;
using jKnepel.SimpleUnityNetworking.Networking;

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
                if (_settingsEditor == null)
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

            _scrollViewPosition = EditorGUILayout.BeginScrollView(_scrollViewPosition);
            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

            GUILayout.Label("Network Manager", EditorStyles.largeLabel);

            StaticNetworkManager.NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(StaticNetworkManager.NetworkConfiguration, typeof(NetworkConfiguration), true);
            if (StaticNetworkManager.NetworkConfiguration != null)
                SettingsEditor.OnInspectorGUI();

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
