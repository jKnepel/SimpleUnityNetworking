using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Logging
{
    [CreateAssetMenu(fileName = "LoggerConfiguration", menuName = "SimpleUnityNetworking/LoggerConfiguration")]
    public class LoggerConfiguration : ScriptableObject
    {
        public Logger Logger => _logger;
        private Logger _logger;
        public LoggerSettings Settings => _settings;
        [SerializeField] private LoggerSettings _settings;
        
        public LoggerConfiguration()
            : this(new()) {}

        public LoggerConfiguration(LoggerSettings settings)
        {
            _settings = settings;
            _logger = new(Settings);
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(LoggerConfiguration), true)]
    public class LoggerConfigurationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_settings"), new GUIContent("Settings:"));
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
