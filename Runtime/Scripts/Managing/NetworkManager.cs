using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.Networking.Sockets;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class NetworkManager : MonoBehaviour
    {
		#region public members

		/// <summary>
		/// The Configuration for the networking.
		/// </summary>
		public NetworkConfiguration NetworkConfiguration;
		/// <summary>
		/// Wether the local client is currently connected to or hosting a server.
		/// </summary>
		public bool IsConnected => _networkSocket?.IsConnected ?? false;
		/// <summary>
		/// Wether the local client is currently hosting a lobby.
		/// </summary>
		public bool IsHost => ClientInformation?.IsHost ?? false;
		/// <summary>
		/// The current connection status of the local client.
		/// </summary>
		public EConnectionStatus ConnectionStatus => _networkSocket?.ConnectionStatus ?? EConnectionStatus.IsDisconnected;
		/// <summary>
		/// Information on the server the client is currently connected to.
		/// </summary>
		public ServerInformation ServerInformation => _networkSocket?.ServerInformation ?? null;
		/// <summary>
		/// Information on the local clients information associated with the server they are connected to.
		/// </summary>
		public ClientInformation ClientInformation => _networkSocket?.ClientInformation ?? null;
		/// <summary>
		/// All other clients that are connected to the same server as the local client.
		/// </summary>
		public ConcurrentDictionary<byte, ClientInformation> ConnectedClients => _networkSocket?.ConnectedClients ?? null;
		/// <summary>
		/// The number of connected clients.
		/// </summary>
		public byte NumberConnectedClients => (byte)(ConnectedClients?.Values.Count ?? 0);

		/// <summary>
		/// Wether the server discovery is currently active or not.
		/// </summary>
		public bool IsServerDiscoveryActive => _serverDiscovery?.IsActive ?? false;
		/// <summary>
		/// All open servers that the local client could connect to.
		/// </summary>
		public List<OpenServer> OpenServers => _serverDiscovery?.OpenServers ?? null;

		/// <summary>
		/// Action for when connection to or creation of a server is being started.
		/// </summary>
		public Action OnConnecting;
		/// <summary>
		/// Action for when successfully connecting to or creating a Server.
		/// </summary>
		public Action OnConnected;
		/// <summary>
		/// Action for when disconnecting from or closing the Server.
		/// </summary>
		public Action OnDisconnected;
		/// <summary>
		/// Action for when the connection status of the local client was updated.
		/// </summary>
		public Action OnConnectionStatusUpdated;
		/// <summary>
		/// Action for when the server the local client was connected to was closed.
		/// </summary>
		public Action OnServerWasClosed;
		/// <summary>
		/// Action for when a remote Client connected to the current Server and can now receive Messages.
		/// </summary>
		public Action<byte> OnClientConnected;
		/// <summary>
		/// Action for when a remote Client disconnected from the current Server and can no longer receive any Messages.
		/// </summary>
		public Action<byte> OnClientDisconnected;
		/// <summary>
		/// Action for when a Client was added or removed from ConnectedClients.
		/// </summary>
		public Action OnConnectedClientListUpdated;
		/// <summary>
		/// Action for when the Server Discovery was activated.
		/// </summary>
		public Action OnServerDiscoveryActivated;
		/// <summary>
		/// Action for when the Server Discovery was deactivated.
		/// </summary>
		public Action OnServerDiscoveryDeactivated;
		/// <summary>
		/// Action for when a Server was added or removed from the OpenServers.
		/// </summary>
		public Action OnOpenServerListUpdated;
		/// <summary>
		/// Action for when a new Network Message was added.
		/// </summary>
		public Action OnNetworkMessageAdded;

		#endregion

		#region private members

		private ANetworkSocket _networkSocket;
		private ServerDiscoveryManager _serverDiscovery;

		#endregion

		#region lifecycle

		private void OnEnable()
		{
			if (NetworkConfiguration == null)
				NetworkConfiguration = (NetworkConfiguration)ScriptableObject.CreateInstance(typeof(NetworkConfiguration));

			Messaging.OnNetworkMessageAdded += FireOnNetworkMessageAdded;
			StartServerDiscovery();
		}

		private void OnDisable()
		{
			if (_serverDiscovery != null)
			{
				EndServerDiscovery();
				_serverDiscovery = null;
			}

			Messaging.OnNetworkMessageAdded -= OnNetworkMessageAdded;

			if (_networkSocket != null)
				DisconnectFromServer();
		}

		#endregion

		#region public methods

		/// <summary>
		/// Creates a new server with the local client has host.
		/// </summary>
		/// <param name="servername"></param>
		/// <param name="maxNumberClients"></param>
		/// <param name="onConnectionEstablished">Will be called once the server was successfully or failed to be created</param>
		public void CreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null)
		{
			if (!Application.isPlaying)
				return;

			if (IsConnected)
			{
				Messaging.DebugMessage("The Client is already hosting a server!");
				onConnectionEstablished?.Invoke(false);
				return;
			}

			NetworkServer server = new();
			_networkSocket = server;
			server.OnConnecting += FireOnConnecting;
			server.OnConnected += FireOnConnected;
			server.OnDisconnected += FireOnDisconnected;
			server.OnConnectionStatusUpdated += FireOnConnectionStatusUpdated;
			server.OnServerWasClosed += FireOnServerWasClosed;
			server.OnClientConnected += FireOnClientConnected;
			server.OnClientDisconnected += FireOnClientDisconnected;
			server.OnConnectedClientListUpdated += FireOnConnectedClientListUpdated;
			server.StartServer(NetworkConfiguration, servername, maxNumberClients, onConnectionEstablished);
		}

		/// <summary>
		/// Joins an open server as client.
		/// </summary>
		/// <param name="serverIP"></param>
		/// <param name="serverPort"></param>
		/// <param name="onConnectionEstablished">Will be called once the connection to the server was successfully or failed to be created</param>
		public void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
		{
			if (!Application.isPlaying)
				return;

			if (IsConnected)
			{
				Messaging.DebugMessage("The Client is already connected to a server!");
				onConnectionEstablished?.Invoke(false);
				return;
			}

			NetworkClient client = new();
			_networkSocket = client;
			client.OnConnecting += FireOnConnecting;
			client.OnConnected += FireOnConnected;
			client.OnDisconnected += FireOnDisconnected;
			client.OnConnectionStatusUpdated += FireOnConnectionStatusUpdated;
			client.OnServerWasClosed += FireOnServerWasClosed;
			client.OnClientConnected += FireOnClientConnected;
			client.OnClientDisconnected += FireOnClientDisconnected;
			client.OnConnectedClientListUpdated += FireOnConnectedClientListUpdated;
			client.ConnectToServer(NetworkConfiguration, serverIP, serverPort, onConnectionEstablished);
		}

		/// <summary>
		/// Disconnects from the current server. Also closes the server if the local client is the host.
		/// </summary>
		public void DisconnectFromServer()
		{
			if (_networkSocket == null)
				return;

			if (IsConnected)
				_networkSocket.DisconnectFromServer();

			switch (_networkSocket)
			{
				case NetworkServer server:
					server.OnConnecting -= FireOnConnecting;
					server.OnConnected -= FireOnConnected;
					server.OnDisconnected -= FireOnDisconnected;
					server.OnConnectionStatusUpdated -= FireOnConnectionStatusUpdated;
					server.OnServerWasClosed -= FireOnServerWasClosed;
					server.OnClientConnected -= FireOnClientConnected;
					server.OnClientDisconnected -= FireOnClientDisconnected;
					server.OnConnectedClientListUpdated -= FireOnConnectedClientListUpdated;
					break;
				case NetworkClient client:
					client.OnConnecting -= FireOnConnecting;
					client.OnConnected -= FireOnConnected;
					client.OnDisconnected -= FireOnDisconnected;
					client.OnConnectionStatusUpdated -= FireOnConnectionStatusUpdated;
					client.OnServerWasClosed -= FireOnServerWasClosed;
					client.OnClientConnected -= FireOnClientConnected;
					client.OnClientDisconnected -= FireOnClientDisconnected;
					client.OnConnectedClientListUpdated -= FireOnConnectedClientListUpdated;
					break;
			}
			_networkSocket = null;
		}

		/// <summary>
		/// Starts the Server Discovery unless it is already active.
		/// </summary>
		public void StartServerDiscovery()
		{
			if (_serverDiscovery != null && _serverDiscovery.IsActive)
				return;

			_serverDiscovery = new();
			_serverDiscovery.OnServerDiscoveryActivated += FireOnServerDiscoveryActivated;
			_serverDiscovery.OnServerDiscoveryDeactivated += FireOnServerDiscoveryDeactivated;
			_serverDiscovery.OnOpenServerListUpdated += FireOnOpenServerListUpdated;
			_serverDiscovery.StartServerDiscovery(NetworkConfiguration);
		}

		/// <summary>
		/// Ends the Server Discovery unless it is already inactive.
		/// </summary>
		public void EndServerDiscovery()
		{
			if (_serverDiscovery == null || !_serverDiscovery.IsActive)
				return;

			_serverDiscovery.EndServerDiscovery();
			_serverDiscovery.OnServerDiscoveryActivated -= FireOnServerDiscoveryActivated;
			_serverDiscovery.OnServerDiscoveryDeactivated -= FireOnServerDiscoveryDeactivated;
			_serverDiscovery.OnOpenServerListUpdated -= FireOnOpenServerListUpdated;
		}

		/// <summary>
		/// Restarts the Server Discovery.
		/// </summary>
		public void RestartServerDiscovery()
		{
			EndServerDiscovery();
			StartServerDiscovery();
		}

		/// <summary>
		/// Registers a callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
		/// </summary>
		/// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
		/// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
		public void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
		{
			if (!IsConnected)
			{
				Messaging.DebugMessage("The Client is not connected to a server and can't register any data callbacks!");
				return;
			}

			_networkSocket.RegisterStructData(callback);
		}

		/// <summary>
		/// Unregisters a registered callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
		/// </summary>
		/// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
		/// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
		public void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
		{
			if (!IsConnected)
			{
				Messaging.DebugMessage("The Client is not connected to a server and can't unregister any data callbacks!");
				return;
			}

			_networkSocket.UnregisterStructData(callback);
		}

		/// <summary>
		/// Registers a callback for received data bytes. Only works if the local client is currently connected to a server.
		/// </summary>
		/// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
		/// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
		public void RegisterByteData(string dataID, Action<byte, byte[]> callback)
		{
			if (!IsConnected)
			{
				Messaging.DebugMessage("The Client is not connected to a server and can't register any data callbacks!");
				return;
			}

			_networkSocket.RegisterByteData(dataID, callback);
		}

		/// <summary>
		/// Unregisters a registered callback for received data bytes. Only works if the local client is currently connected to a server.
		/// </summary>
		/// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
		/// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
		public void UnregisterByteData(string dataID, Action<byte, byte[]> callback)
		{
			if (!IsConnected)
			{
				Messaging.DebugMessage("The Client is not connected to a server and can't unregister any data callbacks!");
				return;
			}

			_networkSocket.UnregisterByteData(dataID, callback);
		}

		/// <summary>
		/// Sends a struct over the network to all other connected clients.
		/// </summary>
		/// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
		/// <param name="structData"></param>
		/// <param name="networkChannel"></param>
		/// <param name="onDataSend"></param>
		public void SendStructDataToAll<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
			Action<bool> onDataSend = null) where T : struct, IStructData
		{
			SendStructData(0, structData, networkChannel, onDataSend);
		}

		/// <summary>
		/// Sends a struct over the network to the server.
		/// </summary>
		/// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
		/// <param name="structData"></param>
		/// <param name="networkChannel"></param>
		/// <param name="onDataSend"></param>
		public void SendStructDataToServer<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
			Action<bool> onDataSend = null) where T : struct, IStructData
		{
			SendStructData(1, structData, networkChannel, onDataSend);
		}

		/// <summary>
		/// Sends a struct over the network to a specific client.
		/// </summary>
		/// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
		/// <param name="receiverID"></param>
		/// <param name="structData"></param>
		/// <param name="networkChannel"></param>
		/// <param name="onDataSend"></param>
		public void SendStructData<T>(byte receiverID, T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered, 
			Action<bool> onDataSend = null) where T : struct, IStructData
		{
			if (!IsConnected)
			{
				Messaging.DebugMessage("The Client is not connected to a server and can't send any data!");
				onDataSend?.Invoke(false);
				return;
			}

			_networkSocket.SendStructData(receiverID, structData, networkChannel, onDataSend);
		}

		/// <summary>
		/// Sends a byte array over the network to all other connected clients.
		/// </summary>
		/// <param name="dataID"></param>
		/// <param name="data"></param>
		/// <param name="networkChannel"></param>
		/// <param name="onDataSend"></param>
		public void SendByteDataToAll(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
			Action<bool> onDataSend = null)
		{
			SendByteData(0, dataID, data, networkChannel, onDataSend);
		}

		/// <summary>
		/// Sends a byte array over the network to the server.
		/// </summary>
		/// <param name="dataID"></param>
		/// <param name="data"></param>
		/// <param name="networkChannel"></param>
		/// <param name="onDataSend"></param>
		public void SendByteDataToServer(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
			Action<bool> onDataSend = null)
		{
			SendByteData(1, dataID, data, networkChannel, onDataSend);
		}

		/// <summary>
		/// Sends a byte array over the network to a specific client.
		/// </summary>
		/// <param name="receiverID"></param>
		/// <param name="dataID"></param>
		/// <param name="data"></param>
		/// <param name="networkChannel"></param>
		/// <param name="onDataSend"></param>
		public void SendByteData(byte receiverID, string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
			Action<bool> onDataSend = null)
		{
			if (!IsConnected)
			{
				Messaging.DebugMessage("The Client is not connected to a server and can't send any data!");
				onDataSend?.Invoke(false);
				return;
			}

			_networkSocket.SendByteData(receiverID, dataID, data, networkChannel, onDataSend);
		}

		#endregion

		#region private methods

		private void FireOnConnecting() => OnConnecting?.Invoke();
		private void FireOnConnected() => OnConnected?.Invoke();
		private void FireOnDisconnected() => OnDisconnected?.Invoke();
		private void FireOnConnectionStatusUpdated() => OnConnectionStatusUpdated?.Invoke();
		private void FireOnServerWasClosed() => OnServerWasClosed?.Invoke();
		private void FireOnClientConnected(byte clientID) => OnClientConnected?.Invoke(clientID);
		private void FireOnClientDisconnected(byte clientID) => OnClientDisconnected?.Invoke(clientID);
		private void FireOnConnectedClientListUpdated() => OnConnectedClientListUpdated?.Invoke();
		private void FireOnServerDiscoveryActivated() => OnServerDiscoveryActivated?.Invoke();
		private void FireOnServerDiscoveryDeactivated() => OnServerDiscoveryDeactivated?.Invoke();
		private void FireOnOpenServerListUpdated() => OnOpenServerListUpdated?.Invoke();
		private void FireOnNetworkMessageAdded() => OnNetworkMessageAdded?.Invoke();

		#endregion

	}
}
