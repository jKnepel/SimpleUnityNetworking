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
        public Transport Transport => _transport;
        protected Transport _transport;
        public TransportSettings Settings => _settings;
        [SerializeField] protected TransportSettings _settings;
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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_settings"), new GUIContent("Settings:"));
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
