using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Transporting;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
	    public Transport Transport
	    {
		    get => _networkManager.Transport; 
		    set => _networkManager.Transport = value;
	    }

	    public bool IsOnline => _networkManager?.IsOnline ?? false;
	    public bool IsServer => _networkManager?.IsServer ?? false;
	    public bool IsClient => _networkManager?.IsClient ?? false;
	    public bool IsHost => _networkManager?.IsHost ?? false;
	    
	    public ServerInformation ServerInformation => _networkManager.ServerInformation;
	    public ELocalConnectionState Server_LocalState => _networkManager.Server_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients => _networkManager.Server_ConnectedClients;
	    public ClientInformation ClientInformation => _networkManager.ClientInformation;
	    public ELocalClientConnectionState Client_LocalState => _networkManager.Client_LocalState;
	    public ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients => _networkManager.Client_ConnectedClients;

	    
	    public event Action<ELocalConnectionState> Server_OnLocalStateUpdated;
	    public event Action<uint> Server_OnRemoteClientConnected;
	    public event Action<uint> Server_OnRemoteClientDisconnected;
	    public event Action<uint> Server_OnRemoteClientUpdated;
	    
	    public event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
	    public event Action<uint> Client_OnRemoteClientConnected;
	    public event Action<uint> Client_OnRemoteClientDisconnected;
	    public event Action<uint> Client_OnRemoteClientUpdated;

	    private NetworkManager _networkManager;

	    private void Awake()
	    {
		    _networkManager = new();
		    _networkManager.Server_OnLocalStateUpdated += state => Server_OnLocalStateUpdated?.Invoke(state);
		    _networkManager.Server_OnRemoteClientConnected += id => Server_OnRemoteClientConnected?.Invoke(id);
		    _networkManager.Server_OnRemoteClientDisconnected += id => Server_OnRemoteClientDisconnected?.Invoke(id);
		    _networkManager.Server_OnRemoteClientUpdated += id => Server_OnRemoteClientUpdated?.Invoke(id);
		    _networkManager.Client_OnLocalStateUpdated += state => Client_OnLocalStateUpdated?.Invoke(state);
		    _networkManager.Client_OnRemoteClientConnected += id => Client_OnRemoteClientConnected?.Invoke(id);
		    _networkManager.Client_OnRemoteClientDisconnected += id => Client_OnRemoteClientDisconnected?.Invoke(id);
		    _networkManager.Client_OnRemoteClientUpdated += id => Client_OnRemoteClientUpdated?.Invoke(id);
	    }

	    public void Update()
	    {
		    _networkManager?.Update();
	    }

	    private void OnDestroy()
	    {
		    _networkManager.Dispose();
	    }

	    public void StartServer(string servername, uint maxNumberConnectedClients)
	    {
		    _networkManager?.StartServer(servername, maxNumberConnectedClients);
	    }

	    public void StopServer()
	    {
		    _networkManager?.StopServer();
	    }

	    public void StartClient(string username, Color32 userColor)
	    {
		    _networkManager?.StartClient(username, userColor);
	    }

	    public void StopClient()
	    {
		    _networkManager?.StopClient();
	    }

	    public void StopNetwork()
	    {
		    _networkManager?.StopNetwork();
	    }

	    public void RegisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    _networkManager?.RegisterByteData(byteID, callback);
	    }

	    public void UnregisterByteData(string byteID, Action<uint, byte[]> callback)
	    {
		    _networkManager?.UnregisterByteData(byteID, callback);
	    }

	    public void SendByteDataToClient(uint clientID, uint byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    _networkManager?.SendByteDataToClient(clientID, byteID, byteData, channel);
	    }

	    public void SendByteDataToAll(uint byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    _networkManager?.SendByteDataToAll(byteID, byteData, channel);
	    }

	    public void SendByteDataToClients(uint[] clientIDs, uint byteID, byte[] byteData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
	    {
		    _networkManager?.SendByteDataToClients(clientIDs, byteID, byteData, channel);
	    }

	    public void RegisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    _networkManager?.RegisterStructData(callback);
	    }

	    public void UnregisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
	    {
		    _networkManager?.UnregisterStructData(callback);
	    }

	    public void SendStructDataToClient<T>(uint clientID, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    _networkManager?.SendStructDataToClient(clientID, structData, channel);
	    }

	    public void SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    _networkManager?.SendStructDataToAll(structData, channel);
	    }

	    public void SendStructDataToClients<T>(uint[] clientIDs, T structData,
		    ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
	    {
		    _networkManager?.SendStructDataToClients(clientIDs, structData, channel);
	    }
    }
}
