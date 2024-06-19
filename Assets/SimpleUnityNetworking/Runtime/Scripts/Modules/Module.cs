using System;
using UnityEditor;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Modules
{
    [Serializable]
    public abstract class Module : IDisposable
    {
        public abstract string Name { get; }

        protected readonly INetworkManager _networkManager;
        protected readonly ModuleConfiguration _moduleConfiguration;

        protected Module(INetworkManager networkManager, ModuleConfiguration moduleConfig)
        {
            _networkManager = networkManager;
            _moduleConfiguration = moduleConfig;
        }
        
        ~Module()
        {
            Dispose(false);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Module))]
    public class ModuleDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(property.FindPropertyRelative("Name"));
            GUI.enabled = true;
            
            EditorGUILayout.PropertyField(property.FindPropertyRelative("_moduleConfiguration"));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => 0;
    }
#endif
}
