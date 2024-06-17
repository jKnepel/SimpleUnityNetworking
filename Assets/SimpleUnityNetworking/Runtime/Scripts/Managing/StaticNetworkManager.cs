using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using System.Collections.Concurrent;
using System.Net;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public static class StaticNetworkManager
    {
        /// <summary>
        /// The transport instance defined by the configuration
        /// </summary>
        public static Transport Transport => _networkManager.Transport;
        /// <summary>
        /// The configuration that will create the instance of the <see cref="Transport"/>
        /// </summary>
        public static TransportConfiguration TransportConfiguration
        {
            get => _networkManager.TransportConfiguration;
            set => _networkManager.TransportConfiguration = value;
        }

        /// <summary>
        /// The configuration for the serialiser used by the network manager.
        /// </summary>
        public static SerialiserConfiguration SerialiserConfiguration
        {
            get => _networkManager.SerialiserConfiguration;
            set => _networkManager.SerialiserConfiguration = value;
        }

        /// <summary>
        /// The logger instance defined by the configuration
        /// </summary>
        public static Logger Logger => _networkManager.Logger;
        /// <summary>
        /// The configuration that will create the instance of the <see cref="Logger"/>
        /// </summary>
        public static LoggerConfiguration LoggerConfiguration
        {
            get => _networkManager.LoggerConfiguration;
            set => _networkManager.LoggerConfiguration = value;
        }

        /// <summary>
        /// Whether a local server is started
        /// </summary>
        public static bool IsServer => _networkManager.IsServer;
        /// <summary>
        /// Whether a local client is started and authenticated
        /// </summary>
        public static bool IsClient => _networkManager.IsClient;
        /// <summary>
        /// Whether a local server is started or local client is authenticated
        /// </summary>
        public static bool IsOnline => _networkManager.IsOnline;
        /// <summary>
        /// Whether a local server is started and local client is authenticated
        /// </summary>
        public static bool IsHost => _networkManager.IsHost;

        /// <summary>
        /// Endpoint address of the local server
        /// </summary>
        public static IPEndPoint Server_ServerEndpoint => _networkManager.Server_ServerEndpoint;
        /// <summary>
        /// Name of the local server
        /// </summary>
        public static string Server_Servername => _networkManager.Server_Servername;
        /// <summary>
        /// Max number of connected clients of the local server
        /// </summary>
        public static uint Server_MaxNumberOfClients => _networkManager.Server_MaxNumberOfClients;
        /// <summary>
        /// The current connection state of the local server
        /// </summary>
        public static ELocalServerConnectionState Server_LocalState => _networkManager.Server_LocalState;
        /// <summary>
        /// The clients that are connected to the local server
        /// </summary>
        public static ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients => _networkManager.Server_ConnectedClients;

        /// <summary>
        /// Endpoint of the server to which the local client is connected
        /// </summary>
        public static IPEndPoint Client_ServerEndpoint => _networkManager.Client_ServerEndpoint;
        /// <summary>
        /// Name of the local server
        /// </summary>
        public static string Client_Servername => _networkManager.Client_Servername;
        /// <summary>
        /// Max number of connected clients of the server to which the local client is connected
        /// </summary>
        public static uint Client_MaxNumberOfClients => _networkManager.Client_MaxNumberOfClients;
        /// <summary>
        /// Identifier of the local client
        /// </summary>
        public static uint Client_ClientID => _networkManager.Client_ClientID;
        /// <summary>
        /// Username of the local client
        /// </summary>
        public static string Client_Username => _networkManager.Client_Username;
        /// <summary>
        /// UserColour of the local client
        /// </summary>
        public static Color32 Client_UserColour => _networkManager.Client_UserColour;
        /// <summary>
        /// The current connection state of the local client
        /// </summary>
        public static ELocalClientConnectionState Client_LocalState => _networkManager.Client_LocalState;
        /// <summary>
        /// The remote clients that are connected to the same server
        /// </summary>
        public static ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients => _networkManager.Client_ConnectedClients;

        /// <summary>
        /// Called when the local server's connection state has been updated
        /// </summary>
        public static event Action<ELocalServerConnectionState> Server_OnLocalStateUpdated;
        /// <summary>
        /// Called by the local server when a new remote client has been authenticated
        /// </summary>
        public static event Action<uint> Server_OnRemoteClientConnected;
        /// <summary>
        /// Called by the local server when a remote client disconnected
        /// </summary>
        public static event Action<uint> Server_OnRemoteClientDisconnected;
        /// <summary>
        /// Called by the local server when a remote client updated its information
        /// </summary>
        public static event Action<uint> Server_OnRemoteClientUpdated;

        /// <summary>
        /// Called when the local client's connection state has been updated
        /// </summary>
        public static event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
        /// <summary>
        /// Called by the local client when a new remote client has been authenticated
        /// </summary>
        public static event Action<uint> Client_OnRemoteClientConnected;
        /// <summary>
        /// Called by the local client when a remote client disconnected
        /// </summary>
        public static event Action<uint> Client_OnRemoteClientDisconnected;
        /// <summary>
        /// Called by the local client when a remote client updated its information
        /// </summary>
        public static event Action<uint> Client_OnRemoteClientUpdated;

        /// <summary>
        /// Called when a tick was started
        /// </summary>
        public static event Action OnTickStarted;
        /// <summary>
        /// Called when a tick was completed
        /// </summary>
        public static event Action OnTickCompleted;

        private static NetworkManager _networkManager;
        public static NetworkManager NetworkManager
        {
            get
            {
                if (_networkManager != null) return _networkManager;
                _networkManager = new();
                _networkManager.TransportConfiguration = TransportConfiguration;
                _networkManager.SerialiserConfiguration = SerialiserConfiguration;
                _networkManager.LoggerConfiguration = LoggerConfiguration;
                return _networkManager;
            }
        }

        static StaticNetworkManager()
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
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += PreventPlayMode;
#endif
        }

        /// <summary>
        /// This method updates the incoming and outgoing packets,
        /// effectively dictating the state updates of the network. Should be called once per tick.
        /// </summary>
        public static void Tick()
        {
            _networkManager.Tick();
        }

        /// <summary>
        /// Method to start a local server with the given parameters
        /// </summary>
        /// <param name="servername"></param>
        public static void StartServer(string servername)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            _networkManager.StartServer(servername);
        }

        /// <summary>
        /// Method to stop the local server
        /// </summary>
        public static void StopServer()
        {
            _networkManager.StopServer();
        }

        /// <summary>
        /// Method to start a local client with the given parameters
        /// </summary>
        /// <param name="username"></param>
        /// <param name="userColour"></param>
        public static void StartClient(string username, Color32 userColour)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            _networkManager.StartClient(username, userColour);
        }

        /// <summary>
        /// Method to stop the local client 
        /// </summary>
        public static void StopClient()
        {
            _networkManager.StopClient();
        }

        /// <summary>
        /// Method to stop both the local client and server
        /// </summary>
        public static void StopNetwork()
        {
            _networkManager.StopNetwork();
        }

        /// <summary>
        /// Registers a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public static void Client_RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            _networkManager.Client_RegisterByteData(byteID, callback);
        }
        /// <summary>
        /// Unregisters a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public static void Client_UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            _networkManager.Client_UnregisterByteData(byteID, callback);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to the server.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Client_SendByteDataToServer(string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Client_SendByteDataToServer(byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Client_SendByteDataToClient(uint clientID, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Client_SendByteDataToClient(clientID, byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to all other remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Client_SendByteDataToAll(string byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Client_SendByteDataToAll(byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to a list of remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Client_SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Client_SendByteDataToClients(clientIDs, byteID, byteData, channel);
        }

        /// <summary>
        /// Registers a callback for a sent struct of type <see cref="IStructData"/>
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public static void Client_RegisterStructData<T>(Action<uint, T> callback) where T : struct
        {
            _networkManager.Client_RegisterStructData(callback);
        }
        /// <summary>
        /// Unregisters a callback for a sent struct of type <see cref="IStructData"/>
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public static void Client_UnregisterStructData<T>(Action<uint, T> callback) where T : struct
        {
            _networkManager.Client_UnregisterStructData(callback);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to the server.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Client_SendStructDataToServer<T>(T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Client_SendStructDataToServer(structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Client_SendStructDataToClient<T>(uint clientID, T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Client_SendStructDataToClient(clientID, structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to all other remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Client_SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Client_SendStructDataToAll(structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to a list of remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Client_SendStructDataToClients<T>(uint[] clientIDs, T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Client_SendStructDataToClients(clientIDs, structData, channel);
        }
        
        /// <summary>
        /// Registers a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public static void Server_RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            _networkManager.Server_RegisterByteData(byteID, callback);
        }
        /// <summary>
        /// Unregisters a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public static void Server_UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            _networkManager.Server_UnregisterByteData(byteID, callback);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Server_SendByteDataToClient(uint clientID, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Server_SendByteDataToClient(clientID, byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to all other remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Server_SendByteDataToAll(string byteID, byte[] byteData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Server_SendByteDataToAll(byteID, byteData, channel);
        }
        /// <summary>
        /// Sends byte data with a given id from the local client to a list of remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public static void Server_SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            _networkManager.Server_SendByteDataToClients(clientIDs, byteID, byteData, channel);
        }

        /// <summary>
        /// Registers a callback for a sent struct of type <see cref="IStructData"/>
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public static void Server_RegisterStructData<T>(Action<uint, T> callback) where T : struct
        {
            _networkManager.Server_RegisterStructData(callback);
        }
        /// <summary>
        /// Unregisters a callback for a sent struct of type <see cref="IStructData"/>
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public static void Server_UnregisterStructData<T>(Action<uint, T> callback) where T : struct
        {
            _networkManager.Server_UnregisterStructData(callback);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Server_SendStructDataToClient<T>(uint clientID, T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Server_SendStructDataToClient(clientID, structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to all other remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Server_SendStructDataToAll<T>(T structData, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Server_SendStructDataToAll(structData, channel);
        }
        /// <summary>
        /// Sends a struct of type <see cref="IStructData"/> from the local client to a list of remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public static void Server_SendStructDataToClients<T>(uint[] clientIDs, T structData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            _networkManager.Server_SendStructDataToClients(clientIDs, structData, channel);
        }

#if UNITY_EDITOR
        private static void PreventPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || !IsOnline) return;
            EditorApplication.isPlaying = false;
            Debug.LogWarning("Playmode is not possible while the static network manager is online!");
        }
#endif
    }
}
