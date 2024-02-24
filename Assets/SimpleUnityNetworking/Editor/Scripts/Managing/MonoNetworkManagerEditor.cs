using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Utilities;
using UnityEngine;
using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    [CustomEditor(typeof(MonoNetworkManager))]
    internal class MonoNetworkManagerEditor : Editor
    {
        private NetworkConfiguration NetworkConfiguration
        {
            get => Target.NetworkConfiguration;
            set
            {
                if (Target.NetworkConfiguration != value)
                    _settingsEditor = null;

                Target.NetworkConfiguration = value;
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

        private NetworkManagerEditor _networkManagerEditor = null;
        private NetworkManagerEditor NetworkManagerEditor
        {
            get
            {
               return _networkManagerEditor ??= new();
            }
        }

        [SerializeField] private MonoNetworkManager _target = null;
        private MonoNetworkManager Target
        {
            get
            {
                if (_target is null)
                {
                    _target = (MonoNetworkManager)target;
                    NetworkManagerEditor.SubscribeNetworkEvents(_target.Events, Repaint);
                }
                return _target;
            }
        }

        private void OnDestroy()
        {
            NetworkManagerEditor.UnsubscribeNetworkEvents(Target.Events, Repaint);
        }

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isCompiling)
            {
                GUILayout.Label("The editor is compiling...\nSettings will show up after recompile.", EditorStyles.largeLabel);
                return;
            }

            if (Target.NetworkManager == null)
            {
                GUILayout.Label("The network manager is null. Can not show settings.", EditorStyles.largeLabel);
                return;
            }

            if (!EditorApplication.isPlaying)
                NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(NetworkConfiguration, typeof(NetworkConfiguration), false) ??
					UnityUtilities.LoadOrCreateScriptableObject<NetworkConfiguration>("NetworkConfiguration", "Assets/Resources/");

            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Randomize User Information"), GUILayout.ExpandWidth(false)))
            {
                NetworkConfiguration.Username = $"User_{Random.Range(0, 100)}";
                NetworkConfiguration.Color = new Color(Random.value, Random.value, Random.value);
            }

            SettingsEditor?.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (!Application.isPlaying)
                GUILayout.Label("Functionality is disabled in edit mode.");

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            NetworkManagerEditor.OnInspectorGUI(Target.NetworkManager);
            EditorGUI.EndDisabledGroup();
        }
    }
}
