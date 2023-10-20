using UnityEngine;
using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Networking
{
    [CustomEditor(typeof(NetworkConfiguration))]
    internal class NetworkConfigurationEditor : Editor
    {
        private bool _networkSettingsIsVisible = false;
        private bool _serverDiscoverySettingsIsVisible = false;
        private bool _debugSettingsIsVisible = false;

        // TODO : add descriptions to labels, was too lazy
        public override void OnInspectorGUI()
        {
            var settings = (NetworkConfiguration)target;

            // user settings
            settings.Username = EditorGUILayout.TextField(new GUIContent("Username", "The username of the client."), settings.Username);
            settings.Color = EditorGUILayout.ColorField(new GUIContent("Color", "The color of the client."), settings.Color);

            // network configuration settings
            _networkSettingsIsVisible = EditorGUILayout.Foldout(_networkSettingsIsVisible, "Network Configuration Settings", EditorStyles.foldoutHeader);
            if (_networkSettingsIsVisible)
            {
                EditorGUI.indentLevel++;

                settings.LocalIPAddressIndex = EditorGUILayout.Popup("Local IP Address:", settings.LocalIPAddressIndex, settings.LocalStringIPAddresses);
                EditorGUILayout.IntField(new GUIContent("Local Port:", "The Local Port used by the Network Socket. The Port is readonly and is assigned automatically upon starting a Socket."), settings.LocalPort);
                EditorGUILayout.Space();
                settings.MTU = EditorGUILayout.IntField("MTU:", settings.MTU);
                settings.RTT = EditorGUILayout.IntField("RTT:", settings.RTT);
                EditorGUILayout.Space();
                settings.ServerConnectionTimeout = EditorGUILayout.IntField("Connection Timeout:", settings.ServerConnectionTimeout);
                settings.ServerHeartbeatDelay = EditorGUILayout.IntField("Heartbeat Delay:", settings.ServerHeartbeatDelay);
                settings.ServerDiscoveryTimeout = EditorGUILayout.IntField("ServerDiscovery Timeout:", settings.ServerDiscoveryTimeout);
                settings.MaxNumberResendReliablePackets = EditorGUILayout.IntField("Number of Resends of Reliable Packets: ", settings.MaxNumberResendReliablePackets);
                
                EditorGUI.indentLevel--;
            }

            _serverDiscoverySettingsIsVisible = EditorGUILayout.Foldout(_serverDiscoverySettingsIsVisible, "Server Discovery Settings", EditorStyles.foldoutHeader);
            if (_serverDiscoverySettingsIsVisible)
            {
                EditorGUI.indentLevel++;

                settings.DiscoveryIP = EditorGUILayout.TextField(new GUIContent("Server Discovery Address:", "The Multicast Address used for the Server Discovery"), settings.DiscoveryIP);
                settings.DiscoveryPort = EditorGUILayout.IntField("Server Discovery Port:", settings.DiscoveryPort);

                EditorGUI.indentLevel--;
            }

            // debug settings
            _debugSettingsIsVisible = EditorGUILayout.Foldout(_debugSettingsIsVisible, "Debug Settings", EditorStyles.foldoutHeader);
            if (_debugSettingsIsVisible)
            {
                EditorGUI.indentLevel++;

                settings.ShowDebugMessages = EditorGUILayout.Toggle(new GUIContent("Show Debug Messages:", "Allows the display of debug messages."), settings.ShowDebugMessages);
                settings.AllowVirtualIPs = EditorGUILayout.Toggle("Allow Virtual IPs:", settings.AllowVirtualIPs);
                settings.AllowLocalConnections = EditorGUILayout.Toggle(new GUIContent("Allow Local Connection:", "Allows connection from the local IP."), settings.AllowLocalConnections);

                EditorGUI.indentLevel--;
            }

            EditorUtility.SetDirty(settings);
        }
    }
}
