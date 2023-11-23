using UnityEngine;
using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
	[CustomPropertyDrawer(typeof(SerialiserConfiguration))]
	public class SerialiserConfigurationEditor : PropertyDrawer
    {
		private const float LINE_HEIGHT = 18f;
		private const float POSITION_OFFSET = 20f;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 1;

			float positionY = position.y;
			var compressFloatsRect = new Rect(position.x, positionY, position.width, LINE_HEIGHT);
			positionY += POSITION_OFFSET;

			SerializedProperty compressFloats = property.FindPropertyRelative("_compressFloats");
			EditorGUI.PropertyField(compressFloatsRect, compressFloats, 
				new GUIContent("Compress Floats:", "Whether floats should be automatically compressed by the serialiser."));
			if (compressFloats.boolValue)
			{
				var floatMinRect = new Rect(position.x, positionY, position.width, LINE_HEIGHT);
				positionY += POSITION_OFFSET;
				var floatMaxRect = new Rect(position.x, positionY, position.width, LINE_HEIGHT);
				positionY += POSITION_OFFSET;
				var floatResRect = new Rect(position.x, positionY, position.width, LINE_HEIGHT);
				positionY += POSITION_OFFSET;

				EditorGUI.indentLevel = 2;
				EditorGUI.PropertyField(floatMinRect, property.FindPropertyRelative("_floatMinValue"),
					new GUIContent("Min Range:", "The minimum value defining the range in which the compressed float can be saved."));
				EditorGUI.PropertyField(floatMaxRect, property.FindPropertyRelative("_floatMaxValue"),
					new GUIContent("Max Range:", "The maximum value defining the range in which the compressed float can be saved."));
				EditorGUI.PropertyField(floatResRect, property.FindPropertyRelative("_floatResolution"),
					new GUIContent("Resolution:", "The floating point resolution in which the float is serialised."));
				EditorGUI.indentLevel = 1;
			}

			var compressQuatRect = new Rect(position.x, positionY, position.width, LINE_HEIGHT);
			positionY += POSITION_OFFSET;
			var bitsPerCompRect = new Rect(position.x, positionY, position.width, LINE_HEIGHT);

			SerializedProperty compressQuat = property.FindPropertyRelative("_compressQuaternions");
			EditorGUI.PropertyField(compressQuatRect, compressQuat,
				new GUIContent("Compress Quaternions:", "Whether Quaternions should be automatically compressed by the serialiser."));
			if (compressQuat.boolValue)
			{
				EditorGUI.indentLevel = 2;
				EditorGUI.PropertyField(bitsPerCompRect, property.FindPropertyRelative("_bitsPerComponent"),
					new GUIContent("Bits per Component:", "The number of bits used by each compressed Quaternion component."));
				EditorGUI.indentLevel = 1;
			}

			property?.serializedObject.ApplyModifiedProperties();
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float totalHeight = EditorGUI.GetPropertyHeight(property, label);

			SerializedProperty compressFloats = property?.FindPropertyRelative("_compressFloats");
			if (compressFloats.boolValue) 
				totalHeight += POSITION_OFFSET * 3;
			
			totalHeight += POSITION_OFFSET;
			SerializedProperty compressQuats = property?.FindPropertyRelative("_compressQuaternions");
			if (compressQuats.boolValue)
				totalHeight += POSITION_OFFSET;

			return totalHeight;
		}
	}
}
