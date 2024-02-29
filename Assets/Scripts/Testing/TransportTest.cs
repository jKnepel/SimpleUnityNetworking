using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.Transporting;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TransportTest : MonoBehaviour
{
    [SerializeField] private TransportConfiguration _config;
    [SerializeField] private int _targetClientID;
    [SerializeField] private string _message;

    public bool IsOnline => _config.Transport?.IsOnline ?? false;
    public bool IsServer => _config.Transport?.IsServer ?? false;
    public bool IsClient => _config.Transport?.IsClient ?? false;
    public bool IsHost => _config.Transport?.IsHost ?? false;

    private void Awake()
    {
        _config.Transport.OnClientStateUpdated += (state) =>
        {
            Debug.Log($"LocalClient: {state}");
        };
        _config.Transport.OnServerStateUpdated += (state) =>
        {
            Debug.Log($"LocalServer: {state}");
        };
    }

    private void Update()
    {
        if (_config.Transport != null)
        {
            _config.Transport.IterateIncoming();
            _config.Transport.IterateOutgoing();
        }
    }

    private void OnDestroy()
    {
        _config.Transport.StopNetwork();
    }

    public void CreateServer()
    {
        _config.Transport.StartServer();
        _config.Transport.OnServerReceivedData += DataFromClient;
        _config.Transport.OnConnectionUpdated += RemoteConnectionUpdated;
    }

    public void StopServer()
    {
        _config.Transport.OnConnectionUpdated -= RemoteConnectionUpdated;
        _config.Transport.OnServerReceivedData -= DataFromClient;
        _config.Transport.StopServer();
    }

    public void CreateClient()
    {
        _config.Transport.StartClient();
        _config.Transport.OnClientReceivedData += DataFromServer;
    }

    public void StopClient()
    {
        _config.Transport.OnClientReceivedData -= DataFromServer;
        _config.Transport.StopClient();
    }

    public void SendToServer()
    {
        Writer writer = new();
        writer.WriteString(_message);
        _config.Transport.SendDataToServer(writer.GetBuffer());
    }

    public void SendToClient()
    {
        Writer writer = new();
        writer.WriteString(_message);
        _config.Transport.SendDataToClient(_targetClientID, writer.GetBuffer());
    }

    private void DataFromClient(ServerReceivedData data)
    {
        Reader reader = new(data.Data);
        Debug.Log($"Server: {reader.ReadString()}");
    }
    
    private void DataFromServer(ClientReceivedData data)
    {
        Reader reader = new(data.Data);
        Debug.Log($"Client: {reader.ReadString()}");
    }

    private void RemoteConnectionUpdated(int clientID, ERemoteConnectionState state)
    {
        Debug.Log($"RemoteClient: {clientID} {state}");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TransportTest))]
public class TransportTestEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
        var test = (TransportTest)target;
        
        GUILayout.Label($"IsOnline: {test.IsOnline}");
        GUILayout.Label($"IsServer: {test.IsServer}");
        GUILayout.Label($"IsClient: {test.IsClient}");
        GUILayout.Label($"IsHost: {test.IsHost}");

        if (GUILayout.Button("CreateServer"))
            test.CreateServer();
        if (GUILayout.Button("StopServer"))
            test.StopServer();
        if (GUILayout.Button("CreateClient"))
            test.CreateClient();
        if (GUILayout.Button("StopClient"))
            test.StopClient();
        if (GUILayout.Button("SendServer"))
            test.SendToServer();
        if (GUILayout.Button("SendClient"))
            test.SendToClient();
    }
}
#endif