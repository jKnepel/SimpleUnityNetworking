using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    [CreateAssetMenu(fileName = "ServerDiscoveryConfiguration", menuName = "SimpleUnityNetworking/Modules/ServerDiscoveryConfiguration")]
    public class ServerDiscoveryConfiguration : ModuleConfiguration
    {
        public override Module GetModule(INetworkManager networkManager) 
            => new ServerDiscoveryModule(networkManager, this, Settings);
        
        public ServerDiscoverySettings Settings = new();
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(ServerDiscoveryConfiguration), true)]
    public class ServerDiscoveryConfigurationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (ServerDiscoveryConfiguration)target;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Settings"));
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
