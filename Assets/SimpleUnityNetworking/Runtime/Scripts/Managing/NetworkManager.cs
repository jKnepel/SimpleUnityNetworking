using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Sockets;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class NetworkManager : INetworkManager, IDisposable
    {
        #region public members
        
        public NetworkConfiguration NetworkConfiguration { get; set; }
        public NetworkEvents Events { get; }

        public bool IsConnected => _networkSocket?.IsConnected ?? false;
        public bool IsHost => ClientInformation?.IsHost ?? false;
        public EConnectionStatus ConnectionStatus => _networkSocket?.ConnectionStatus ?? EConnectionStatus.IsDisconnected;
        public ServerInformation ServerInformation => _networkSocket?.ServerInformation;
        public ClientInformation ClientInformation => _networkSocket?.ClientInformation;
        public ConcurrentDictionary<byte, ClientInformation> ConnectedClients => _networkSocket?.ConnectedClients;
        public byte NumberConnectedClients => (byte)(ConnectedClients?.Values.Count ?? 0);
        
        public bool IsServerDiscoveryActive => _serverDiscovery?.IsActive ?? false;
        public List<OpenServer> OpenServers => _serverDiscovery?.OpenServers;
        

        #endregion

        #region private members

        private ANetworkSocket _networkSocket;
        private ServerDiscoveryManager _serverDiscovery;

        #endregion

        #region lifecycle

        public NetworkManager(bool startServerDiscovery = true)
        {
            Events = new();
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

        public void CreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null)
        {
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
            server.StartServer(NetworkConfiguration, servername, maxNumberClients, onConnectionEstablished);
        }

        public void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
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
            client.ConnectToServer(NetworkConfiguration, serverIP, serverPort, onConnectionEstablished);
        }

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

        public void StartServerDiscovery()
        {
            if (_serverDiscovery is { IsActive: true })
                return;

            _serverDiscovery = new();
            _serverDiscovery.OnServerDiscoveryActivated += Events.FireOnServerDiscoveryActivated;
            _serverDiscovery.OnServerDiscoveryDeactivated += Events.FireOnServerDiscoveryDeactivated;
            _serverDiscovery.OnOpenServerListUpdated += Events.FireOnOpenServerListUpdated;
            _serverDiscovery.StartServerDiscovery(NetworkConfiguration);
        }

        public void EndServerDiscovery()
        {
            if (_serverDiscovery is not { IsActive: true })
                return;

            _serverDiscovery.EndServerDiscovery();
            _serverDiscovery.OnServerDiscoveryActivated -= Events.FireOnServerDiscoveryActivated;
            _serverDiscovery.OnServerDiscoveryDeactivated -= Events.FireOnServerDiscoveryDeactivated;
            _serverDiscovery.OnOpenServerListUpdated -= Events.FireOnOpenServerListUpdated;
        }

        public void RestartServerDiscovery()
        {
            EndServerDiscovery();
            StartServerDiscovery();
        }

        public void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            if (!IsConnected)
            {
                Messaging.DebugMessage("The Client is not connected to a server and can't register any data callbacks!");
                return;
            }

            _networkSocket.RegisterStructData(callback);
        }

        public void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            if (!IsConnected)
            {
                Messaging.DebugMessage("The Client is not connected to a server and can't unregister any data callbacks!");
                return;
            }

            _networkSocket.UnregisterStructData(callback);
        }

        public void RegisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            if (!IsConnected)
            {
                Messaging.DebugMessage("The Client is not connected to a server and can't register any data callbacks!");
                return;
            }

            _networkSocket.RegisterByteData(dataID, callback);
        }

        public void UnregisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            if (!IsConnected)
            {
                Messaging.DebugMessage("The Client is not connected to a server and can't unregister any data callbacks!");
                return;
            }

            _networkSocket.UnregisterByteData(dataID, callback);
        }

        public void SendStructDataToAll<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            SendStructData(0, structData, networkChannel, onDataSend);
        }

        public void SendStructDataToServer<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            SendStructData(1, structData, networkChannel, onDataSend);
        }

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

        public void SendByteDataToAll(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            SendByteData(0, dataID, data, networkChannel, onDataSend);
        }

        public void SendByteDataToServer(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            SendByteData(1, dataID, data, networkChannel, onDataSend);
        }

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
    }
}
