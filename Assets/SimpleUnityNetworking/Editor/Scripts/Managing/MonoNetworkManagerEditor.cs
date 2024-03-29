using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    [CustomEditor(typeof(MonoNetworkManager))]
    internal class MonoNetworkManagerEditor : Editor
    {
        private INetworkManagerEditor _networkManagerEditor;
        private INetworkManagerEditor NetworkManagerEditor
        {
            get
            {
                if (_networkManagerEditor != null)
                    return _networkManagerEditor;
                return _networkManagerEditor = new(
                    (MonoNetworkManager)target, 
                    Repaint,
                    INetworkManagerEditor.EAllowStart.OnlyPlaymode
                );
            }
        }

        public override void OnInspectorGUI()
        {
            NetworkManagerEditor.OnInspectorGUI();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
