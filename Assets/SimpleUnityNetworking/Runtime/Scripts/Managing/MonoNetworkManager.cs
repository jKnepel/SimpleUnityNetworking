using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
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
	    public Transport Transport => TransportConfiguration?.Transport;
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
                    EditorUtility.SetDirty(_cachedTransportConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public Logger Logger => LoggerConfiguration?.Logger;
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
                    EditorUtility.SetDirty(_cachedTransportConfiguration);
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
	    public ELocalServerConnectionState Server_LocalState => NetworkManager.Server_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients => NetworkManager.Server_ConnectedClients;
	    public ClientInformation ClientInformation => NetworkManager.ClientInformation;
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
