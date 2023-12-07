using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace jKnepel.SimpleUnityNetworking
{
    public interface INetworkManager
    {
        NetworkConfiguration NetworkConfiguration { get; set; }

        bool IsConnected { get; }

        public bool IsHost { get; }

        EConnectionStatus ConnectionStatus { get; }

        ServerInformation ServerInformation { get; }

        ClientInformation ClientInformation { get; }

        ConcurrentDictionary<byte, ClientInformation> ConnectedClients { get; }

        byte NumberConnectedClients { get; }

        bool IsServerDiscoveryActive { get; }

        List<OpenServer> OpenServers { get; }

        NetworkEvents Events { get; }

        /// <summary>
        /// Creates a new server with the local client has host.
        /// </summary>
        /// <param name="servername"></param>
        /// <param name="maxNumberClients"></param>
        /// <param name="onConnectionEstablished">Will be called once the server was successfully or failed to be created</param>
        void CreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null);

        /// <summary>
        /// Joins an open server as client.
        /// </summary>
        /// <param name="serverIP"></param>
        /// <param name="serverPort"></param>
        /// <param name="onConnectionEstablished">Will be called once the connection to the server was successfully or failed to be created</param>
        void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null);

        /// <summary>
        /// Disconnects from the current server. Also closes the server if the local client is the host.
        /// </summary>
        void DisconnectFromServer();

        /// <summary>
        /// Starts the Server Discovery unless it is already active.
        /// </summary>
        void StartServerDiscovery();

        /// <summary>
        /// Ends the Server Discovery unless it is already inactive.
        /// </summary>
        void EndServerDiscovery();

        /// <summary>
        /// Restarts the Server Discovery.
        /// </summary>
        void RestartServerDiscovery();

        /// <summary>
        /// Registers a callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
        void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData;

        /// <summary>
        /// Unregisters a registered callback for received data structs of type <typeparamref name="T"/>. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="callback">Callback containing the sender ID and synchronised data struct</param>
        void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData;

        /// <summary>
        /// Registers a callback for received data bytes. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
        /// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
        void RegisterByteData(string dataID, Action<byte, byte[]> callback);

        /// <summary>
        /// Unregisters a registered callback for received data bytes. Only works if the local client is currently connected to a server.
        /// </summary>
        /// <param name="dataID">Global identifier for byte array. Sender and receiver must use the same ID.</param>
        /// <param name="callback">Callback containing the sender ID and synchronised data bytes</param>
        void UnregisterByteData(string dataID, Action<byte, byte[]> callback);

        /// <summary>
        /// Sends a struct over the network to all other connected clients.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="structData"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        void SendStructDataToAll<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
           Action<bool> onDataSend = null) where T : struct, IStructData;

        /// <summary>
        /// Sends a struct over the network to the server.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="structData"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        void SendStructDataToServer<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
           Action<bool> onDataSend = null) where T : struct, IStructData;

        /// <summary>
        /// Sends a struct over the network to a specific client.
        /// </summary>
        /// <typeparam name="T">A struct implementing IStructData, which containts the to be synchronised data</typeparam>
        /// <param name="receiverID"></param>
        /// <param name="structData"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        void SendStructData<T>(byte receiverID, T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
           Action<bool> onDataSend = null) where T : struct, IStructData;

        /// <summary>
        /// Sends a byte array over the network to all other connected clients.
        /// </summary>
        /// <param name="dataID"></param>
        /// <param name="data"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        void SendByteDataToAll(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
           Action<bool> onDataSend = null);

        /// <summary>
        /// Sends a byte array over the network to the server.
        /// </summary>
        /// <param name="dataID"></param>
        /// <param name="data"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        void SendByteDataToServer(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
           Action<bool> onDataSend = null);

        /// <summary>
        /// Sends a byte array over the network to a specific client.
        /// </summary>
        /// <param name="receiverID"></param>
        /// <param name="dataID"></param>
        /// <param name="data"></param>
        /// <param name="networkChannel"></param>
        /// <param name="onDataSend"></param>
        void SendByteData(byte receiverID, string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
           Action<bool> onDataSend = null);
    }
}
