using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Serialising
{
	[CreateAssetMenu(fileName = "SerialiserConfiguration", menuName = "SimpleUnityNetworking/SerialiserConfiguration")]
    public class SerialiserConfiguration : ScriptableObject
    {
	    /// <summary>
	    /// If compression should be used for all serialisation in the framework.
	    /// </summary>
	    public bool UseCompression = true;
	    /// <summary>
        /// If compression is active, this will define the number of decimal places to which
        /// floating point numbers will be compressed.
        /// </summary>
        public int NumberOfDecimalPlaces = 3;
        /// <summary>
        /// If compression is active, this will define the number of bits used by the three compressed Quaternion
        /// components in addition to the two flag bits.
        /// </summary>
        public int BitsPerComponent = 10;
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(SerialiserConfiguration), true)]
    public class SerialiserConfigurationEditor : Editor
    {
	    public override void OnInspectorGUI()
	    {
		    var useCompression = serializedObject.FindProperty("UseCompression");
		    EditorGUILayout.PropertyField(useCompression, new GUIContent("UseCompression:", "If compression should be used for all serialisation in the framework."));
		    if (useCompression.boolValue)
		    {
				EditorGUI.indentLevel++;
			    EditorGUILayout.PropertyField(serializedObject.FindProperty("NumberOfDecimalPlaces"), new GUIContent("Number of Decimal Places:", "If compression is active, this will define the number of decimal places to which floating point numbers will be compressed."));
			    EditorGUILayout.PropertyField(serializedObject.FindProperty("BitsPerComponent"), new GUIContent("Bits per Component:", "If compression is active, this will define the number of bits used by the three compressed Quaternion components in addition to the two flag bits."));
				EditorGUI.indentLevel--;
		    }
            
		    serializedObject.ApplyModifiedProperties();
	    }
    }
#endif
}