using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Modules
{
    public abstract class ModuleConfiguration : ScriptableObject
    {
        public abstract string Name { get; }
        public abstract Module GetModule();
        public abstract void SetModule(INetworkManager networkManager);
        
#if UNITY_EDITOR
        public bool IsModuleGUIVisible;
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ModuleConfiguration), true)]
    public class ModuleConfigurationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (ModuleConfiguration)target;
            EditorGUILayout.TextField("Type:", config.Name, EditorStyles.label);
            EditorGUI.indentLevel++;
            config.IsModuleGUIVisible = EditorGUILayout.Foldout(config.IsModuleGUIVisible, "Module UI:", true);
            if (config.IsModuleGUIVisible)
                config.GetModule()?.ModuleGUI();
            EditorGUI.indentLevel--;
        }
    }
#endif
}
