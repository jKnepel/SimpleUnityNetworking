using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    [CreateAssetMenu(fileName = "ServerDiscoveryConfiguration", menuName = "SimpleUnityNetworking/Modules/ServerDiscoveryConfiguration")]
    public class ServerDiscoveryConfiguration : ModuleConfiguration
    {
        public override string Name => "ServerDiscovery";
        
        private ServerDiscoveryModule _module;
        public override Module GetModule() => _module;
        public override void SetModule(INetworkManager networkManager) => _module = new(networkManager, Settings);
        
        public ServerDiscoverySettings Settings = new();
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(ServerDiscoveryConfiguration), true)]
    public class ServerDiscoveryConfigurationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (ServerDiscoveryConfiguration)target;
            EditorGUILayout.TextField("Type:", config.Name, EditorStyles.label);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Settings"));
            config.IsModuleGUIVisible = EditorGUILayout.Foldout(config.IsModuleGUIVisible, "Module UI:", true);
            if (config.IsModuleGUIVisible)
                config.GetModule()?.ModuleGUI();
            EditorGUI.indentLevel--;
        }
    }
#endif
}
