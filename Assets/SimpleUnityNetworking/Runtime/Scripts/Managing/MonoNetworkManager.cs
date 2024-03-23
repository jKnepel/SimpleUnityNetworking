using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.Transporting;
using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
	    [SerializeField] private TransportConfiguration _cachedTransportConfiguration;
	    public TransportConfiguration TransportConfiguration
	    {
		    get => _cachedTransportConfiguration;
		    set
		    {
			    if (_cachedTransportConfiguration == value) return;
			    NetworkManager.TransportConfiguration = _cachedTransportConfiguration = value;
			    
#if UNITY_EDITOR
				if (value != null)
				    EditorUtility.SetDirty(_cachedTransportConfiguration);
			    if (!EditorApplication.isPlaying)
					EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    [SerializeField] private SerialiserConfiguration _cachedSerialiserConfiguration;
	    public SerialiserConfiguration SerialiserConfiguration
	    {
		    get => _cachedSerialiserConfiguration;
		    set
		    {
			    if (_cachedSerialiserConfiguration == value) return;
			    NetworkManager.SerialiserConfiguration = _cachedSerialiserConfiguration = value;
			    
#if UNITY_EDITOR
			    if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public bool IsOnline => NetworkManager.IsOnline;
	    public bool IsServer => NetworkManager.IsServer;
	    public bool IsClient => NetworkManager.IsClient;
	    public bool IsHost => NetworkManager.IsHost;
	    
	    public ServerInformation ServerInformation => NetworkManager.ServerInformation;
	    public ELocalConnectionState Server_LocalState => NetworkManager.Server_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients => NetworkManager.Server_ConnectedClients;
	    public ClientInformation ClientInformation => NetworkManager.ClientInformation;
	    public ELocalClientConnectionState Client_LocalState => NetworkManager.Client_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients => NetworkManager.Client_ConnectedClients;

	    public event Action<ELocalConnectionState> Server_OnLocalStateUpdated;
	    public event Action<uint> Server_OnRemoteClientConnected;
	    public event Action<uint> Server_OnRemoteClientDisconnected;
	    public event Action<uint> Server_OnRemoteClientUpdated;
	    
	    public event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
	    public event Action<uint> Client_OnRemoteClientConnected;
	    public event Action<uint> Client_OnRemoteClientDisconnected;
	    public event Action<uint> Client_OnRemoteClientUpdated;

	    private NetworkManager _networkManager;
	    private NetworkManager NetworkManager
	    {
		    get
		    {
			    if (_networkManager != null) return _networkManager;
			    _networkManager = new();
			    _networkManager.TransportConfiguration = _cachedTransportConfiguration;
			    _networkManager.SerialiserConfiguration = _cachedSerialiserConfiguration;
			    return _networkManager;
		    }
	    }

	    private void Awake()
	    {
		    NetworkManager.Server_OnLocalStateUpdated += state => Server_OnLocalStateUpdated?.Invoke(state);
		    NetworkManager.Server_OnRemoteClientConnected += id => Server_OnRemoteClientConnected?.Invoke(id);
		    NetworkManager.Server_OnRemoteClientDisconnected += id => Server_OnRemoteClientDisconnected?.Invoke(id);
		    NetworkManager.Server_OnRemoteClientUpdated += id => Server_OnRemoteClientUpdated?.Invoke(id);
		    NetworkManager.Client_OnLocalStateUpdated += state => Client_OnLocalStateUpdated?.Invoke(state);
		    NetworkManager.Client_OnRemoteClientConnected += id => Client_OnRemoteClientConnected?.Invoke(id);
		    NetworkManager.Client_OnRemoteClientDisconnected += id => Client_OnRemoteClientDisconnected?.Invoke(id);
		    NetworkManager.Client_OnRemoteClientUpdated += id => Client_OnRemoteClientUpdated?.Invoke(id);
	    }

	    public void Update()
	    {
		    NetworkManager.Update();
	    }

	    private void OnDestroy()
	    {
		    NetworkManager.Dispose();
	    }

	    public void StartServer(string servername, uint maxNumberConnectedClients)
	    {
		    NetworkManager.StartServer(servername, maxNumberConnectedClients);
	    }

	    public void StopServer()
	    {
		    NetworkManager.StopServer();
	    }

	    public void StartClient(string username, Color32 userColor)
	    {
		    NetworkManager.StartClient(username, userColor);
	    }

	    public void StopClient()
	    {
		    NetworkManager.StopClient();
	    }

	    public void StopNetwork()
	    {
		    NetworkManager.StopNetwork();
	    }

	    public void RegisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    NetworkManager.RegisterByteData(byteID, callback);
	    }

	    public void UnregisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    NetworkManager.UnregisterByteData(byteID, callback);
	    }

	    public void SendByteDataToClient(uint clientID, string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.SendByteDataToClient(clientID, byteID, byteData, channel);
	    }

	    public void SendByteDataToAll(string byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.SendByteDataToAll(byteID, byteData, channel);
	    }

	    public void SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.SendByteDataToClients(clientIDs, byteID, byteData, channel);
	    }

	    public void RegisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    NetworkManager.RegisterStructData(callback);
	    }

	    public void UnregisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    NetworkManager.UnregisterStructData(callback);
	    }

	    public void SendStructDataToClient<T>(uint clientID, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.SendStructDataToClient(clientID, structData, channel);
	    }

	    public void SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.SendStructDataToAll(structData, channel);
	    }

	    public void SendStructDataToClients<T>(uint[] clientIDs, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.SendStructDataToClients(clientIDs, structData, channel);
	    }
    }
}
