using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Serialising
{
	[CreateAssetMenu(fileName = "SerialiserConfiguration", menuName = "SimpleUnityNetworking/SerialiserConfiguration")]
    public class SerialiserConfiguration : ScriptableObject
    {
	    public SerialiserSettings Settings = new();

	    public SerialiserSettings GetSerialiserSettings()
	    {
		    return new()
		    {
				UseCompression = Settings.UseCompression,
				NumberOfDecimalPlaces = Settings.NumberOfDecimalPlaces,
				BitsPerComponent = Settings.BitsPerComponent
		    };
	    }
    }

#if UNITY_EDITOR
	[CustomEditor(typeof(SerialiserConfiguration), true)]
	public class SerialiserConfigurationEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(serializedObject.FindProperty("Settings"), new GUIContent("Settings:"));
			EditorGUI.indentLevel--;
            
			serializedObject.ApplyModifiedProperties();
		}
	}
#endif
}
