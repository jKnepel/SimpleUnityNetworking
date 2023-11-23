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
    [SerializeField] private Transform _obj1;
    [SerializeField] private Transform _obj2;
    
    private bool _registered = false;

    public void Register()
    {
        _manager.RegisterStructData<ObjectData>(ReceiveStruct);
        _registered = true;
    }

    public void Unregister()
    {
        _manager.UnregisterStructData<ObjectData>(ReceiveStruct);
        _registered = false;
    }

    private void Update()
    {
        if (!_manager.IsConnected || !_registered || !_obj1.hasChanged)
            return;

		ObjectData test = new()
        {
            Position = _obj1.position,
            Rotation = _obj1.rotation,
        };
        _manager.SendStructDataToAll(test, ENetworkChannel.UnreliableOrdered, (succ) => Debug.Log($"Packet Send: {succ}"));
        _obj1.hasChanged = false;
	}

    private void ReceiveStruct(byte userId, ObjectData test)
    {
        _obj2.SetPositionAndRotation(test.Position, test.Rotation);
    }

    private struct ObjectData : IStructData
    {
        public Vector3 Position;
        public Quaternion Rotation;
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
    }
}
#endif