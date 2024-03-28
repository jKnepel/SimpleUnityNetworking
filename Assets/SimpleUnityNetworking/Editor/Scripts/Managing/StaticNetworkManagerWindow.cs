using jKnepel.SimpleUnityNetworking.Managing;
using UnityEditor;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking
{
    public class StaticNetworkManagerWindow : EditorWindow
    {
        private const int PADDING = 10;
        
        private INetworkManagerEditor _networkManagerEditor;
        private INetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                if (_networkManagerEditor != null)
                    return _networkManagerEditor;
                return _networkManagerEditor = new (StaticNetworkManager.NetworkManager, Repaint);
            }
        }
        
        [MenuItem("Window/SimpleUnityNetworking/Static Network Manager")]
        public static void ShowWindow()
        {
            GetWindow(typeof(StaticNetworkManagerWindow), false, "Network Manager");
        }

        private void OnGUI()
        {
            var area = new Rect(PADDING, PADDING, position.width - PADDING * 2f, position.height - PADDING * 2f);
            
            GUILayout.BeginArea(area);
            NetworkManagerEditor.OnInspectorGUI();
            GUILayout.EndArea();
        }
    }
}
