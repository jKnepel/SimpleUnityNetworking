using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
	    public Transport Transport => NetworkManager.Transport;
	    [SerializeField] private TransportConfiguration _cachedTransportConfiguration;
	    public TransportConfiguration TransportConfiguration
	    {
		    get => NetworkManager.TransportConfiguration;
		    set
		    {
			    if (NetworkManager.TransportConfiguration == value) return;
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
		    get => NetworkManager.SerialiserConfiguration;
		    set
		    {
			    if (NetworkManager.SerialiserConfiguration == value) return;
			    NetworkManager.SerialiserConfiguration = _cachedSerialiserConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedSerialiserConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public Logger Logger => NetworkManager.Logger;
	    [SerializeField] private LoggerConfiguration _cachedLoggerConfiguration;
	    public LoggerConfiguration LoggerConfiguration
	    {
		    get => NetworkManager.LoggerConfiguration;
		    set
		    {
			    if (NetworkManager.LoggerConfiguration == value) return;
			    NetworkManager.LoggerConfiguration = _cachedLoggerConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedLoggerConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public bool IsServer => NetworkManager.IsServer;
	    public bool IsClient => NetworkManager.IsClient;
	    public bool IsOnline => NetworkManager.IsOnline;
	    public bool IsHost => NetworkManager.IsHost;

	    public IPEndPoint Server_ServerEndpoint => NetworkManager.Server_ServerEndpoint;
	    public string Server_Servername => NetworkManager.Server_Servername;
	    public uint Server_MaxNumberOfClients => NetworkManager.Server_MaxNumberOfClients;
	    public ELocalServerConnectionState Server_LocalState => NetworkManager.Server_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients => NetworkManager.Server_ConnectedClients;

	    public IPEndPoint Client_ServerEndpoint => NetworkManager.Client_ServerEndpoint;
	    public string Client_Servername => NetworkManager.Client_Servername;
	    public uint Client_MaxNumberOfClients => NetworkManager.Client_MaxNumberOfClients;
	    public uint Client_ClientID => NetworkManager.Client_ClientID;
	    public string Client_Username => NetworkManager.Client_Username;
	    public Color32 Client_UserColour => NetworkManager.Client_UserColour;
	    public ELocalClientConnectionState Client_LocalState => NetworkManager.Client_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients => NetworkManager.Client_ConnectedClients;

	    public event Action<ELocalServerConnectionState> Server_OnLocalStateUpdated;
	    public event Action<uint> Server_OnRemoteClientConnected;
	    public event Action<uint> Server_OnRemoteClientDisconnected;
	    public event Action<uint> Server_OnRemoteClientUpdated;
	    
	    public event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
	    public event Action<uint> Client_OnRemoteClientConnected;
	    public event Action<uint> Client_OnRemoteClientDisconnected;
	    public event Action<uint> Client_OnRemoteClientUpdated;

	    public event Action OnTickStarted;
	    public event Action OnTickCompleted;

	    private NetworkManager _networkManager;
	    public NetworkManager NetworkManager
	    {
		    get
		    {
			    if (_networkManager != null) return _networkManager;
			    _networkManager = new();
			    _networkManager.TransportConfiguration = _cachedTransportConfiguration;
			    _networkManager.SerialiserConfiguration = _cachedSerialiserConfiguration;
			    _networkManager.LoggerConfiguration = _cachedLoggerConfiguration;
			    return _networkManager;
		    }
		    private set => _networkManager = value;
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
		    NetworkManager.OnTickStarted += () => OnTickStarted?.Invoke();
		    NetworkManager.OnTickCompleted += () => OnTickCompleted?.Invoke();
	    }

	    public void Tick()
	    {
		    NetworkManager.Tick();
	    }

	    private void OnDestroy()
	    {
		    NetworkManager.Dispose();
		    NetworkManager = null;
	    }

	    public void StartServer(string servername)
	    {
#if UNITY_EDITOR
		    if (!EditorApplication.isPlaying) return;		    
#endif
		    NetworkManager.StartServer(servername);
	    }

	    public void StopServer()
	    {
		    NetworkManager.StopServer();
	    }

	    public void StartClient(string username, Color32 userColour)
	    {
#if UNITY_EDITOR
		    if (!EditorApplication.isPlaying) return;		    
#endif
		    NetworkManager.StartClient(username, userColour);
	    }

	    public void StopClient()
	    {
		    NetworkManager.StopClient();
	    }

	    public void StopNetwork()
	    {
		    NetworkManager.StopNetwork();
	    }

	    public void Client_RegisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    NetworkManager.Client_RegisterByteData(byteID, callback);
	    }

	    public void Client_UnregisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    NetworkManager.Client_UnregisterByteData(byteID, callback);
	    }

	    public void Client_SendByteDataToServer(string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Client_SendByteDataToServer(byteID, byteData, channel);
	    }
	    
	    public void Client_SendByteDataToClient(uint clientID, string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Client_SendByteDataToClient(clientID, byteID, byteData, channel);
	    }

	    public void Client_SendByteDataToAll(string byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Client_SendByteDataToAll(byteID, byteData, channel);
	    }

	    public void Client_SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Client_SendByteDataToClients(clientIDs, byteID, byteData, channel);
	    }

	    public void Client_RegisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    NetworkManager.Client_RegisterStructData(callback);
	    }

	    public void Client_UnregisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    NetworkManager.Client_UnregisterStructData(callback);
	    }
	    
	    public void Client_SendStructDataToServer<T>(T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Client_SendStructDataToServer(structData, channel);
	    }

	    public void Client_SendStructDataToClient<T>(uint clientID, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Client_SendStructDataToClient(clientID, structData, channel);
	    }

	    public void Client_SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Client_SendStructDataToAll(structData, channel);
	    }

	    public void Client_SendStructDataToClients<T>(uint[] clientIDs, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Client_SendStructDataToClients(clientIDs, structData, channel);
	    }
	    
	    public void Server_RegisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    NetworkManager.Server_RegisterByteData(byteID, callback);
	    }

	    public void Server_UnregisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    NetworkManager.Server_UnregisterByteData(byteID, callback);
	    }

	    public void Server_SendByteDataToClient(uint clientID, string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Server_SendByteDataToClient(clientID, byteID, byteData, channel);
	    }

	    public void Server_SendByteDataToAll(string byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Server_SendByteDataToAll(byteID, byteData, channel);
	    }

	    public void Server_SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    NetworkManager.Server_SendByteDataToClients(clientIDs, byteID, byteData, channel);
	    }

	    public void Server_RegisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    NetworkManager.Server_RegisterStructData(callback);
	    }

	    public void Server_UnregisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    NetworkManager.Server_UnregisterStructData(callback);
	    }

	    public void Server_SendStructDataToClient<T>(uint clientID, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Server_SendStructDataToClient(clientID, structData, channel);
	    }

	    public void Server_SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Server_SendStructDataToAll(structData, channel);
	    }

	    public void Server_SendStructDataToClients<T>(uint[] clientIDs, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    NetworkManager.Server_SendStructDataToClients(clientIDs, structData, channel);
	    }
    }
}
