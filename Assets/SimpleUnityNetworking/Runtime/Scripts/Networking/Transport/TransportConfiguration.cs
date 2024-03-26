using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    [Serializable]
    public abstract class TransportConfiguration : ScriptableObject
    {
        private Transport _transport;
        public Transport Transport => _transport;
        public TransportSettings Settings;

        protected TransportConfiguration(Transport transport, TransportSettings settings)
        {
            _transport = transport;
            Settings = settings;
            Transport.SetTransportSettings(Settings);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(TransportConfiguration), true)]
    public class TransportConfigurationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (TransportConfiguration)target;
            
            EditorGUILayout.TextField("Type:", config.Transport.GetType().Name, EditorStyles.label);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Settings"), new GUIContent("Settings:"));
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(TransportSettings), true)]
    public class TransportSettingsDrawer : PropertyDrawer
    {
        private bool _areSettingsVisible;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            _areSettingsVisible = EditorGUILayout.Foldout(_areSettingsVisible, "Settings:");

            if (_areSettingsVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property.FindPropertyRelative("Address"), new GUIContent("Address:", "The address to which the local client will attempt to connect with."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("Port"), new GUIContent("Port:", "The port to which the local client will attempt to connect with or the server will bind to locally."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("ServerListenAddress"), new GUIContent("Server Listen Address:", "Address to which the local server will be bound. If no address is provided, the IPv4 Loopback address will be used instead."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("MaxNumberOfClients"), new GUIContent("Max Number of Clients:", "The maximum number of connections allowed by the local server."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("ConnectTimeoutMS"), new GUIContent("Connect Timeout:", "Time between connection attempts."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("MaxConnectAttempts"), new GUIContent("Max Connect Attempts:", "Maximum number of connection attempts to try. If no answer is received from the server after this number of attempts, a disconnect event is generated for the connection."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("DisconnectTimeoutMS"), new GUIContent("Disconnect Timeout:", "Inactivity timeout for a connection. If nothing is received on a connection for this amount of time, it is disconnected. To prevent this from happening when the game session is simply quiet, set HeartbeatTimeoutMS to a positive non-zero value."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("HeartbeatTimeoutMS"), new GUIContent("Heartbeat Timeout:", "Time after which if nothing from a peer is received, a heartbeat message will be sent to keep the connection alive. Prevents the DisconnectTimeoutMS mechanism from kicking when nothing happens on a connection. A value of 0 will disable heartbeats."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("PayloadCapacity"), new GUIContent("Payload Capacity:", "Maximum size that can be fragmented. Attempting to send a message larger than that will result in the send operation failing. Maximum value is ~20MB for unreliable packets, and ~88KB for reliable ones."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("WindowSize"), new GUIContent("Window Size:", "Maximum number in-flight packets per pipeline/connection combination. Default value is 32 but can be increased to 64 at the cost of slightly larger packet headers."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("MinimumResendTime"), new GUIContent("Minimum Resend Time", "Minimum amount of time to wait before a reliable packet is resent if it's not been acknowledged."));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("MaximumResendTime"), new GUIContent("Maximum Resend Time", "Maximum amount of time to wait before a reliable packet is resent if it's not been acknowledged. That is, even with a high RTT the reliable pipeline will never wait longer than this value to resend a packet."));
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return 0; }
    }
#endif
}
