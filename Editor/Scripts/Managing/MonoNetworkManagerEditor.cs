using UnityEngine;
using UnityEditor;
using jKnepel.SimpleUnityNetworking.Networking;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    [CustomEditor(typeof(MonoNetworkManager))]
    internal class MonoNetworkManagerEditor : Editor
    {
        private Editor _settingsEditor = null;
        public Editor SettingsEditor
        {
            get
            {
                if (_settingsEditor == null)
                    _settingsEditor = Editor.CreateEditor(((MonoNetworkManager)target).NetworkConfiguration);
                return _settingsEditor;
            }
        }

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

        private void OnEnable()
        {
            var manager = (MonoNetworkManager)target;
            NetworkManagerEditor.SubscribeNetworkEvents(manager.Events, Repaint);
        }

        private void OnDisable()
        {
            var manager = (MonoNetworkManager)target;
            NetworkManagerEditor.UnsubscribeNetworkEvents(manager.Events, Repaint);
        }

        public override void OnInspectorGUI()
        {
            var manager = (MonoNetworkManager)target;

            manager.NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(manager.NetworkConfiguration, typeof(NetworkConfiguration), true);
            if (manager.NetworkConfiguration != null)
                SettingsEditor.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (!Application.isPlaying)
                GUILayout.Label("Functionality is disabled in edit mode.");

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            NetworkManagerEditor.OnInspectorGUI(manager.NetworkManager);
            EditorGUI.EndDisabledGroup();
        }
    }
}
