using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
        #region public members

        /// <summary>
        /// The Configuration for the networking.
        /// </summary>
        public NetworkConfiguration NetworkConfiguration
        {
            get => NetworkManager.NetworkConfiguration;
            set => NetworkManager.NetworkConfiguration = value;
        }

        /// <summary>
        /// Wether the local client is currently connected to or hosting a server.
        /// </summary>
        public bool IsConnected => NetworkManager.IsConnected;
        /// <summary>
        /// Wether the local client is currently hosting a lobby.
        /// </summary>
        public bool IsHost => NetworkManager.IsHost;
        /// <summary>
        /// The current connection status of the local client.
        /// </summary>
        public EConnectionStatus ConnectionStatus => NetworkManager.ConnectionStatus;
        /// <summary>
        /// Information on the server the client is currently connected to.
        /// </summary>
        public ServerInformation ServerInformation => NetworkManager.ServerInformation;
        /// <summary>
        /// Information on the local clients information associated with the server they are connected to.
        /// </summary>
        public ClientInformation ClientInformation => NetworkManager.ClientInformation;
        /// <summary>
        /// All other clients that are connected to the same server as the local client.
        /// </summary>
        public ConcurrentDictionary<byte, ClientInformation> ConnectedClients => NetworkManager.ConnectedClients;
        /// <summary>
        /// The number of connected clients.
        /// </summary>
        public byte NumberConnectedClients => NetworkManager.NumberConnectedClients;

        /// <summary>
        /// Wether the server discovery is currently active or not.
        /// </summary>
        public bool IsServerDiscoveryActive => NetworkManager.IsServerDiscoveryActive;
        /// <summary>
        /// All open servers that the local client could connect to.
        /// </summary>
        public List<OpenServer> OpenServers => NetworkManager.OpenServers;

        /// <summary>
        /// Network events.
        /// </summary>
        public NetworkEvents Events => NetworkManager.Events;

        #endregion

        #region private members

        private NetworkManager _networkManager;

        public NetworkManager NetworkManager
        {
            get
            {
                if (_networkManager == null)
                    _networkManager = new(BeforeCreateServer, BeforeJoinServer, false);
                return _networkManager;
            }
        }

        #endregion

        #region lifecycle

        private void Start()
        {
            StartServerDiscovery();
        }

        private void OnDestroy()
        {
            NetworkManager.Dispose();
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
            NetworkManager.CreateServer(servername, maxNumberClients, onConnectionEstablished);
        }
        private bool BeforeCreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Can not create server with mono network manager while in edit mode.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Joins an open server as client.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="serverPort"></param>
        /// <param name="onConnectionEstablished">Will be called once the connection to the server was successfully or failed to be created</param>
        public void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
            NetworkManager.JoinServer(serverIP, serverPort, onConnectionEstablished);
        }
        public bool BeforeJoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Can not join server with mono network manager while in edit mode.");
                return false;
            }
            return true;
        }
        /// <summary>
        /// Disconnects from the current server. Also closes the server if the local client is the host.
        /// </summary>
        public void DisconnectFromServer()
        {
            NetworkManager.DisconnectFromServer();
        }

        /// <summary>
        /// Starts the Server Discovery unless it is already active.
        /// </summary>
        public void StartServerDiscovery()
        {
            NetworkManager.StartServerDiscovery();
        }

        /// <summary>
        /// Ends the Server Discovery unless it is already inactive.
        /// </summary>
        public void EndServerDiscovery()
        {
            NetworkManager.EndServerDiscovery();
        }

        /// <summary>
        /// Restarts the Server Discovery.
        /// </summary>
        public void RestartServerDiscovery()
        {
            NetworkManager.RestartServerDiscovery();
        }

        /// <summary>
        /// Registers a callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
        public void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            NetworkManager.RegisterStructData(callback);
        }

        /// <summary>
        /// Unregisters a registered callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
        public void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            NetworkManager.UnregisterStructData(callback);
        }

        /// <summary>
        /// Registers a callback for received data bytes. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
        /// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
        public void RegisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            NetworkManager.RegisterByteData(dataID, callback);
        }

        /// <summary>
        /// Unregisters a registered callback for received data bytes. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
        /// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
        public void UnregisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            NetworkManager.UnregisterByteData(dataID, callback);
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
            NetworkManager.SendStructDataToAll(structData, networkChannel, onDataSend);
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
            NetworkManager.SendStructDataToServer(structData, networkChannel, onDataSend);
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
            NetworkManager.SendStructData(receiverID, structData, networkChannel, onDataSend);
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
            NetworkManager.SendByteDataToAll(dataID, data, networkChannel, onDataSend);
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
            NetworkManager.SendByteDataToServer(dataID, data, networkChannel, onDataSend);
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
            NetworkManager.SendByteData(receiverID, dataID, data, networkChannel, onDataSend);
        }

        #endregion
    }
}
