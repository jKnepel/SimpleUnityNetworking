using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Serialising
{
	[CreateAssetMenu(fileName = "SerialiserConfiguration", menuName = "SimpleUnityNetworking/SerialiserConfiguration")]
    public class SerialiserConfiguration : ScriptableObject
    {
	    public SerialiserSettings Settings => _settings;
	    [SerializeField] private SerialiserSettings _settings;
        
	    public SerialiserConfiguration()
		    : this(new()) {}

	    public SerialiserConfiguration(SerialiserSettings settings)
	    {
		    _settings = settings;
	    }
    }

#if UNITY_EDITOR
	[CustomEditor(typeof(SerialiserConfiguration), true)]
	public class SerialiserConfigurationEditor : Editor
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
