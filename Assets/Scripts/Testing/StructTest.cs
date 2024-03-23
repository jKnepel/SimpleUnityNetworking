using System;
using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StructTest : MonoBehaviour
{
    [SerializeField] private MonoNetworkManager _manager;
    [SerializeField] private uint _targetClientID;
    [SerializeField] private string _message;
    
    public bool IsOnline => _manager?.IsOnline ?? false;
    public bool IsServer => _manager?.IsServer ?? false;
    public bool IsClient => _manager?.IsClient ?? false;
    public bool IsHost => _manager?.IsHost ?? false;
    
    private void Start()
    {
        _manager.Server_OnLocalStateUpdated += state =>
        {
            Debug.Log($"Server: {state}");
        };
        _manager.Client_OnLocalStateUpdated += state =>
        {
            Debug.Log($"Client: {state}");
        };
        _manager.Server_OnRemoteClientConnected += id =>
        {
            Debug.Log($"Server: Client {id} connected");
        };
        _manager.Server_OnRemoteClientDisconnected += id =>
        {
            Debug.Log($"Server: Client {id} disconnected");
        };
        _manager.Server_OnRemoteClientUpdated += id =>
        {
            Debug.Log($"Server: Client {id} updated");
        };
        _manager.Client_OnRemoteClientConnected += id =>
        {
            Debug.Log($"Client: Client {id} connected");
        };
        _manager.Client_OnRemoteClientDisconnected += id =>
        {
            Debug.Log($"Client: Client {id} disconnected");
        };
        _manager.Client_OnRemoteClientUpdated += id =>
        {
            Debug.Log($"Client: Client {id} updated");
        };
    }

    private void Update()
    {
        _manager.Update();
	}

    public void StartServer()
    {
        _manager.StartServer("server", 5);
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
        _manager.RegisterStructData<MessageStruct>(ReceiveStruct);
    }

    public void Unregister()
    {
        _manager.UnregisterStructData<MessageStruct>(ReceiveStruct);
    }

    public void SendToClient(ENetworkChannel channel = ENetworkChannel.ReliableOrdered)
    {
        MessageStruct message = new() { Message = _message };
        _manager.SendStructDataToClient<MessageStruct>(_targetClientID, message, channel);
    }

    private void ReceiveStruct(uint clientID, MessageStruct message)
    {
        Debug.Log($"Received {message.Message} from {clientID}");
    }

    private struct MessageStruct : IStructData
    {
        public string Message;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(StructTest))]
public class UnreliableTestEditor : Editor
{
    private ENetworkChannel _channel = ENetworkChannel.ReliableOrdered;
    
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

        var test = (StructTest)target;
        
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
        if (GUILayout.Button("Send Message"))
            test.SendToClient(_channel);
    }
}
#endif