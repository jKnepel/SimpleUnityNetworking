using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.Networking.Sockets;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class NetworkManager : INetworkManager, IDisposable
    {
        #region public members

        /// <summary>
        /// The Configuration for the networking.
        /// </summary>
        public NetworkConfiguration NetworkConfiguration
        {
            get
            {
                if (_networkConfiguration == null)
                    _networkConfiguration = LoadOrCreateConfiguration<NetworkConfiguration>();
                return _networkConfiguration;
            }
            set => _networkConfiguration = value;
        }
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
        /// Network events.
        /// </summary>
        public NetworkEvents Events { get; } = new NetworkEvents();

        #endregion

        public delegate bool BeforeCreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null);
        public delegate bool BeforeJoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null);

        #region private members

        private static NetworkConfiguration _networkConfiguration;

        private ANetworkSocket _networkSocket;
        private ServerDiscoveryManager _serverDiscovery;

        private BeforeCreateServer _beforeCreateServer = null;
        private BeforeJoinServer _beforeJoinServer = null;

        #endregion


        #region lifecycle

        public NetworkManager(BeforeCreateServer beforeCreateServer, BeforeJoinServer beforeJoinServer,
            bool startServerDiscovery = true)
        {
            _beforeCreateServer = beforeCreateServer;
            _beforeJoinServer = beforeJoinServer;

            Messaging.OnNetworkMessageAdded += Events.FireOnNetworkMessageAdded;
            if (startServerDiscovery) StartServerDiscovery();
        }

        public void Dispose()
        {
            if (_serverDiscovery != null)
            {
                EndServerDiscovery();
                _serverDiscovery = null;
            }

            Messaging.OnNetworkMessageAdded -= Events.FireOnNetworkMessageAdded;

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
            if (!(_beforeCreateServer?.Invoke(servername, maxNumberClients, onConnectionEstablished) ?? false)) return;

            if (IsConnected)
            {
                Messaging.DebugMessage("The Client is already hosting a server!");
                onConnectionEstablished?.Invoke(false);
                return;
            }

            NetworkServer server = new();
            _networkSocket = server;
            server.OnConnecting += Events.FireOnConnecting;
            server.OnConnected += Events.FireOnConnected;
            server.OnDisconnected += Events.FireOnDisconnected;
            server.OnConnectionStatusUpdated += Events.FireOnConnectionStatusUpdated;
            server.OnServerWasClosed += Events.FireOnServerWasClosed;
            server.OnClientConnected += Events.FireOnClientConnected;
            server.OnClientDisconnected += Events.FireOnClientDisconnected;
            server.OnConnectedClientListUpdated += Events.FireOnConnectedClientListUpdated;
            server.StartServer(NetworkConfiguration, servername, maxNumberClients, onConnectionEstablished); // Bug: The onConnectionEstablished event is not fired!
        }

        /// <summary>
        /// Joins an open server as client.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="serverPort"></param>
        /// <param name="onConnectionEstablished">Will be called once the connection to the server was successfully or failed to be created</param>
        public void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
            if (!(_beforeJoinServer?.Invoke(serverIP, serverPort, onConnectionEstablished) ?? false)) return;

            if (IsConnected)
            {
                Messaging.DebugMessage("The Client is already connected to a server!");
                onConnectionEstablished?.Invoke(false);
                return;
            }

            NetworkClient client = new();
            _networkSocket = client;
            client.OnConnecting += Events.FireOnConnecting;
            client.OnConnected += Events.FireOnConnected;
            client.OnDisconnected += Events.FireOnDisconnected;
            client.OnConnectionStatusUpdated += Events.FireOnConnectionStatusUpdated;
            client.OnServerWasClosed += Events.FireOnServerWasClosed;
            client.OnClientConnected += Events.FireOnClientConnected;
            client.OnClientDisconnected += Events.FireOnClientDisconnected;
            client.OnConnectedClientListUpdated += Events.FireOnConnectedClientListUpdated;
            client.ConnectToServer(NetworkConfiguration, serverIP, serverPort, onConnectionEstablished); // Bug: The onConnectionEstablished event is not fired!
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
                    server.OnConnecting -= Events.FireOnConnecting;
                    server.OnConnected -= Events.FireOnConnected;
                    server.OnDisconnected -= Events.FireOnDisconnected;
                    server.OnConnectionStatusUpdated -= Events.FireOnConnectionStatusUpdated;
                    server.OnServerWasClosed -= Events.FireOnServerWasClosed;
                    server.OnClientConnected -= Events.FireOnClientConnected;
                    server.OnClientDisconnected -= Events.FireOnClientDisconnected;
                    server.OnConnectedClientListUpdated -= Events.FireOnConnectedClientListUpdated;
                    break;
                case NetworkClient client:
                    client.OnConnecting -= Events.FireOnConnecting;
                    client.OnConnected -= Events.FireOnConnected;
                    client.OnDisconnected -= Events.FireOnDisconnected;
                    client.OnConnectionStatusUpdated -= Events.FireOnConnectionStatusUpdated;
                    client.OnServerWasClosed -= Events.FireOnServerWasClosed;
                    client.OnClientConnected -= Events.FireOnClientConnected;
                    client.OnClientDisconnected -= Events.FireOnClientDisconnected;
                    client.OnConnectedClientListUpdated -= Events.FireOnConnectedClientListUpdated;
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
            _serverDiscovery.OnServerDiscoveryActivated += Events.FireOnServerDiscoveryActivated;
            _serverDiscovery.OnServerDiscoveryDeactivated += Events.FireOnServerDiscoveryDeactivated;
            _serverDiscovery.OnOpenServerListUpdated += Events.FireOnOpenServerListUpdated;
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
            _serverDiscovery.OnServerDiscoveryActivated -= Events.FireOnServerDiscoveryActivated;
            _serverDiscovery.OnServerDiscoveryDeactivated -= Events.FireOnServerDiscoveryDeactivated;
            _serverDiscovery.OnOpenServerListUpdated -= Events.FireOnOpenServerListUpdated;
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

        public static T LoadOrCreateConfiguration<T>(string name = "NetworkConfiguration", string path = "Assets/Resources/") where T : ScriptableObject
        {
            T configuration = Resources.Load<T>(Path.GetFileNameWithoutExtension(name));

            string fullPath = path + name + ".asset";

            if (!configuration)
            {
                if (EditorApplication.isCompiling)
                {
                    UnityEngine.Debug.LogError("Can not load settings when editor is compiling!");
                    return null;
                }
                if (EditorApplication.isUpdating)
                {
                    UnityEngine.Debug.LogError("Can not load settings when editor is updating!");
                    return null;
                }

                configuration = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            }
            if (!configuration)
            {
                string[] allSettings = AssetDatabase.FindAssets($"t:{name}{".asset"}");
                if (allSettings.Length > 0)
                {
                    configuration = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(allSettings[0]));
                }
            }
            if (!configuration)
            {
                configuration = ScriptableObject.CreateInstance<T>();
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(configuration, fullPath);
                AssetDatabase.SaveAssets();
            }

            if (!configuration)
            {
                configuration = ScriptableObject.CreateInstance<T>();
            }

            return configuration;
        }

        #endregion
    }
}
