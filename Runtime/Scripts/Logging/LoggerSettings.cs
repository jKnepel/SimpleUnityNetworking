using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Logging
{
    [Serializable]
    public class LoggerSettings
    {
        /// <summary>
        /// Whether logged messages by the framework should also be printed to the console.
        /// </summary>
        public bool PrintToConsole = true;

        /// <summary>
        /// Whether log level messages should be printed to the console.
        /// </summary>
        public bool PrintLog = true;
        /// <summary>
        /// Whether warning level messages should be printed to the console.
        /// </summary>
        public bool PrintWarning = true;
        /// <summary>
        /// Whether error level messages should be printed to the console.
        /// </summary>
        public bool PrintError = true;
    }
    
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(LoggerSettings), true)]
    public class LoggerSettingsDrawer : PropertyDrawer
    {
        private bool _areSettingsVisible;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            _areSettingsVisible = EditorGUILayout.Foldout(_areSettingsVisible, "Settings:");

            if (_areSettingsVisible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property.FindPropertyRelative("PrintToConsole"), new GUIContent("Print To Console:", "Whether logged messages by the framework should also be printed to the console."));
                if (property.FindPropertyRelative("PrintToConsole").boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("PrintLog"), new GUIContent("Print Log:", "Whether log level messages should be printed to the console."));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("PrintWarning"), new GUIContent("Print Warning:", "Whether warning level messages should be printed to the console."));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("PrintError"), new GUIContent("Print Error:", "Whether error level messages should be printed to the console."));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return 0; }
    }
#endif
}
