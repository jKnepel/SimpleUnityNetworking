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
                if (Target.NetworkConfiguration == value) return;
                
                _settingsEditor = null;
                Target.NetworkConfiguration = value;
            }
        }

        private Editor _settingsEditor;
        private Editor SettingsEditor
        {
            get
            {
                if (_settingsEditor == null && NetworkConfiguration)
                    _settingsEditor = Editor.CreateEditor(NetworkConfiguration);
                return _settingsEditor;
            }
        }

        private NetworkManagerEditor _networkManagerEditor;
        private NetworkManagerEditor NetworkManagerEditor
        {
            get
            {
               return _networkManagerEditor ??= new();
            }
        }

        [SerializeField] private MonoNetworkManager _target;
        private MonoNetworkManager Target
        {
            get
            {
                if (_target == null)
                {
                    _target = (MonoNetworkManager)target;
                    NetworkManagerEditor.SubscribeNetworkEvents(_target.Events, Repaint);
                }
                return _target;
            }
        }

        public void OnEnable()
        {
            NetworkConfiguration = NetworkConfiguration != null 
                ? NetworkConfiguration 
                : UnityUtilities.LoadOrCreateScriptableObject<NetworkConfiguration>("NetworkConfiguration", "Assets/Resources/");
        }

        private void OnDestroy()
        {
            NetworkManagerEditor.UnsubscribeNetworkEvents(Target.Events, Repaint);
        }

        public override void OnInspectorGUI()                   
        {
            if (Target.NetworkManager == null)
            {
                GUILayout.Label("The network manager is null. Can not show settings.", EditorStyles.largeLabel);
                return;
            }
            
            NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(NetworkConfiguration, typeof(NetworkConfiguration), false);

            if (SettingsEditor == null) return;
            
            EditorGUILayout.Space();
            
            SettingsEditor.OnInspectorGUI();

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
