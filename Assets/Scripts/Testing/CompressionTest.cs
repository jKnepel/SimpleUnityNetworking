using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialising;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CompressionTest : MonoBehaviour
{
    [SerializeField] private MonoNetworkManager _manager;
    [SerializeField] private SerialiserConfiguration _serialiserConfiguration;
    [SerializeField] private uint _targetClientID;
    
    public bool IsOnline => _manager?.IsOnline ?? false;
    public bool IsServer => _manager?.IsServer ?? false;
    public bool IsClient => _manager?.IsClient ?? false;
    public bool IsHost => _manager?.IsHost ?? false;

    private void Update()
    {
        _manager.Tick();
	}

    public void StartServer()
    {
        _manager.StartServer("server");
    }

    public void StopServer()
    {
        _manager.StopServer();
    }

    public void StartClient()
    {
        _manager.StartClient("user", new());
    }

    public void StopClient()
    {
        _manager.StopClient();
    }

    public void Register()
    {
        _manager.RegisterByteData("values", ReceiveValueBytes);
    }

    public void Unregister()
    {
        _manager.UnregisterByteData("values", ReceiveValueBytes);
    }

    public void SendValuesToClient(ENetworkChannel channel)
    {
        ValueStruct data = new()
        {
            Byte = 1,
            Short = -2,
            UShort = 5,
            Int = -998,
            UInt = 213,
            Long = -12313123,
            ULong = 123123
        };

        Writer writer = new(_serialiserConfiguration.Settings);
        writer.Write(data);
        _manager.SendByteDataToClient(_targetClientID, "values", writer.GetBuffer(), channel);
    }

    private void ReceiveValueBytes(uint clientID, byte[] data)
    {
        Reader reader = new(data, _serialiserConfiguration.Settings);
        var message = reader.Read<ValueStruct>();
        
        Debug.Log($"Received {data.Length} bytes from {clientID}: " +
                  $"Byte = {message.Byte},\n" +
                  $"Short = {message.Short},\n" +
                  $"UShort = {message.UShort},\n" +
                  $"Int = {message.Int},\n" +
                  $"UInt = {message.UInt},\n" +
                  $"Long = {message.Long},\n" +
                  $"ULong = {message.ULong}");
    }

    private struct ValueStruct
    {
        public byte Byte;
        public short Short;
        public ushort UShort;
        public int Int;
        public uint UInt;
        public long Long;
        public ulong ULong;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CompressionTest))]
public class CompressionTestEditor : Editor
{
    private ENetworkChannel _channel = ENetworkChannel.ReliableOrdered;
    
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

        var test = (CompressionTest)target;
        
        GUILayout.Label($"IsOnline: {test.IsOnline}");
        GUILayout.Label($"IsServer: {test.IsServer}");
        GUILayout.Label($"IsClient: {test.IsClient}");
        GUILayout.Label($"IsHost: {test.IsHost}");
        _channel = (ENetworkChannel)EditorGUILayout.EnumPopup(_channel);
        
        if (GUILayout.Button("Register"))
            test.Register();
        if (GUILayout.Button("Unregister"))
            test.Unregister();
        if (GUILayout.Button("Start Server"))
            test.StartServer();
        if (GUILayout.Button("Stop Server"))
            test.StopServer();
        if (GUILayout.Button("Start Client"))
            test.StartClient();
        if (GUILayout.Button("Stop Client"))
            test.StopClient();
        if (GUILayout.Button("Send Values"))
            test.SendValuesToClient(_channel);
    }
}
#endif