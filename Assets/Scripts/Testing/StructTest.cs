using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StructTest : MonoBehaviour
{
    [SerializeField] private NetworkManager _manager;

    public void Register()
    {
        _manager.RegisterStructData<TextTest>(ReceiveStruct);
    }

    public void Unregister()
    {
        _manager.UnregisterStructData<TextTest>(ReceiveStruct);
    }

    public void SendStruct()
    {
        TextTest test = new()
        {
            ID = 1,
            Text = "hello",
            Text2 = "world"
        };
        _manager.SendStructData(2, test, ENetworkChannel.ReliableOrdered, (succ) => Debug.Log($"Packet Send: {succ}"));
	}

    private void ReceiveStruct(byte userId, TextTest test)
    {
        Debug.Log($"Received: {userId} {test}");
    }

    private struct TextTest : IStructData
    {
        public int ID;
        public string Text;
        public string Text2;

        public override string ToString() { return $"{ID} {Text} {Text2}"; }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StructTest))]
public class UnreliableTestEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

        var test = (StructTest)target;
        if (GUILayout.Button("Register"))
            test.Register();
        if (GUILayout.Button("Unregister"))
            test.Unregister();
        if (GUILayout.Button("Send"))
            test.SendStruct();
    }
}
#endif