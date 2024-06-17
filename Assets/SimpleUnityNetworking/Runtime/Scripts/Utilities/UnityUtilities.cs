using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Utilities
{
    public static class UnityUtilities
    {
	    public static void DebugByteMessage(byte[] bytes, string msg, bool inBinary = false)
	    {   
		    foreach (byte d in bytes)
			    msg += Convert.ToString(d, inBinary ? 2 : 16).PadLeft(inBinary ? 8 : 2, '0') + " ";
		    Debug.Log(msg);
	    }

	    public static void DebugByteMessage(byte bytes, string msg, bool inBinary = false)
	    {
		    DebugByteMessage(new []{ bytes }, msg, inBinary);
	    }
	    
	    public static T LoadScriptableObject<T>(string name, string path = "Assets/Resources/") where T : ScriptableObject
	    {
		    T configuration = null;

#if UNITY_EDITOR
        	string fullPath = path + name + ".asset";

	        if (EditorApplication.isCompiling)
	        {
		        Debug.LogError("Can not load scriptable object when editor is compiling!");
		        return null;
	        }
	        if (EditorApplication.isUpdating)
	        {
		        Debug.LogError("Can not load scriptable object when editor is updating!");
		        return null;
	        }

	        configuration = AssetDatabase.LoadAssetAtPath<T>(fullPath);
	        
        	if (!configuration)
        	{
        		string[] allSettings = AssetDatabase.FindAssets($"t:{name}.asset");
        		if (allSettings.Length > 0)
        		{
        			configuration = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(allSettings[0]));
        		}
        	}
#endif
		    
		    if (!configuration)
		    {
        		configuration = Resources.Load<T>(Path.GetFileNameWithoutExtension(name));
		    }

        	return configuration;
        }
	    
		public static T LoadOrCreateScriptableObject<T>(string name, string path = "Assets/Resources/") where T : ScriptableObject
		{
			T configuration = LoadScriptableObject<T>(name, path);

#if UNITY_EDITOR
			if (!configuration)
			{
				string fullPath = path + name + ".asset";
				configuration = ScriptableObject.CreateInstance<T>();
				string dir = Path.GetDirectoryName(fullPath);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);
				AssetDatabase.CreateAsset(configuration, fullPath);
				AssetDatabase.SaveAssets();
			}
#endif

			if (!configuration)
			{
				configuration = ScriptableObject.CreateInstance<T>();
			}

			return configuration;
		}
		
#if UNITY_EDITOR
		public static void DrawToggleFoldout(string title, ref bool isExpanded,
            bool? checkbox = null, string checkboxLabel = null)
        {
            Color normalColour = new(0.24f, 0.24f, 0.24f);
            Color hoverColour = new(0.27f, 0.27f, 0.27f);
            var currentColour = normalColour;

            var backgroundRect = GUILayoutUtility.GetRect(1f, 17f);
            var labelRect = backgroundRect;
            labelRect.xMin += 16f;
            labelRect.xMax -= 2f;

            var foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;

            var toggleRect = backgroundRect;
            toggleRect.x = backgroundRect.width - 7f;
            toggleRect.y += 2f;
            toggleRect.width = 13f;
            toggleRect.height = 13f;

            var toggleLabelRect = backgroundRect;
            toggleLabelRect.x = -10f;

            var e = Event.current;
            if (labelRect.Contains(e.mousePosition))
                currentColour = hoverColour;
            EditorGUI.DrawRect(backgroundRect, currentColour);

            if (isExpanded)
            {
                var borderBot = GUILayoutUtility.GetRect(1f, 0.6f);
                EditorGUI.DrawRect(borderBot, new(0, 0, 0));
            }

            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            isExpanded = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

            if (checkbox is not null)
            {
                if (checkboxLabel is not null)
                {
                    var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                    EditorGUI.LabelField(toggleLabelRect, checkboxLabel, labelStyle);
                }
                EditorGUI.Toggle(toggleRect, (bool)checkbox, new("ShurikenToggle"));
            }

            if (e.type == EventType.MouseDown && labelRect.Contains(e.mousePosition) && e.button == 0)
            {
                isExpanded = !isExpanded;
                e.Use();
            }
        }
#endif
	}
}
