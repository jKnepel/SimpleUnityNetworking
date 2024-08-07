using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Modules;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
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
        public static Transport Transport => NetworkManager.Transport;
        /// <summary>
        /// The configuration that will create the instance of the <see cref="Transport"/>
        /// </summary>
        public static TransportConfiguration TransportConfiguration
        {
            get => NetworkManager.TransportConfiguration;
            set => NetworkManager.TransportConfiguration = value;
        }

        /// <summary>
        /// Settings for the serialiser used when sending byte and struct data
        /// </summary>
        public static SerialiserSettings SerialiserSettings => NetworkManager.SerialiserSettings;
        /// <summary>
        /// The configuration that will create the instance of the <see cref="SerialiserSettings"/>
        /// </summary>
        public static SerialiserConfiguration SerialiserConfiguration
        {
            get => NetworkManager.SerialiserConfiguration;
            set => NetworkManager.SerialiserConfiguration = value;
        }

        /// <summary>
        /// The logger instance defined by the configuration
        /// </summary>
        public static Logger Logger => NetworkManager.Logger;
        /// <summary>
        /// The configuration that will create the instance of the <see cref="Logger"/>
        /// </summary>
        public static LoggerConfiguration LoggerConfiguration
        {
            get => NetworkManager.LoggerConfiguration;
            set => NetworkManager.LoggerConfiguration = value;
        }
        
        /// <summary>
        /// List of modules currently registered with the network manager
        /// </summary>
        public static ModuleList Modules => NetworkManager.Modules;

        /// <summary>
        /// The instance of the local server, which provides access to the server's API, values and events
        /// </summary>
        public static Server Server => NetworkManager.Server;
        /// <summary>
        /// The instance of the local client, which provides access to the client's API, values and events
        /// </summary>
        public static Client Client => NetworkManager.Client;

        /// <summary>
        /// Whether a local server is started
        /// </summary>
        public static bool IsServer => NetworkManager.IsServer;
        /// <summary>
        /// Whether a local client is started and authenticated
        /// </summary>
        public static bool IsClient => NetworkManager.IsClient;
        /// <summary>
        /// Whether a local server is started or local client is authenticated
        /// </summary>
        public static bool IsOnline => NetworkManager.IsOnline;
        /// <summary>
        /// Whether a local server is started and local client is authenticated
        /// </summary>
        public static bool IsHost => NetworkManager.IsHost;

        /// <summary>
        /// Called when <see cref="Transport"/> was disposed
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action OnTransportDisposed
        {
            add => NetworkManager.OnTransportDisposed += value;
            remove => NetworkManager.OnTransportDisposed -= value;
        }
        /// <summary>
        /// Called when the local server received new data from the transport layer
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<ServerReceivedData> OnServerReceivedData
        {
            add => NetworkManager.OnServerReceivedData += value;
            remove => NetworkManager.OnServerReceivedData -= value;
        }
        /// <summary>
        /// Called when the local client received new data from the transport layer
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<ClientReceivedData> OnClientReceivedData
        {
            add => NetworkManager.OnClientReceivedData += value;
            remove => NetworkManager.OnClientReceivedData -= value;
        }
        /// <summary>
        /// Called when the local server's transport state was updated
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<ELocalConnectionState> OnServerStateUpdated
        {
            add => NetworkManager.OnServerStateUpdated += value;
            remove => NetworkManager.OnServerStateUpdated -= value;
        }
        /// <summary>
        /// Called when the local client's transport state was updated
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<ELocalConnectionState> OnClientStateUpdated
        {
            add => NetworkManager.OnClientStateUpdated += value;
            remove => NetworkManager.OnClientStateUpdated -= value;
        }
        /// <summary>
        /// Called when a remote client's transport state was updated
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<uint, ERemoteConnectionState> OnConnectionUpdated
        {
            add => NetworkManager.OnConnectionUpdated += value;
            remove => NetworkManager.OnConnectionUpdated -= value;
        }
        /// <summary>
        /// Called when a new log was added by the transport
        /// </summary>
        /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<string, EMessageSeverity> OnTransportLogAdded
        {
            add => NetworkManager.OnTransportLogAdded += value;
            remove => NetworkManager.OnTransportLogAdded -= value;
        }
        /// <summary>
        /// Called when a tick was started. Contains the tick number as parameter
        /// </summary>
        public static event Action<uint> OnTickStarted
        {
            add => NetworkManager.OnTickStarted += value;
            remove => NetworkManager.OnTickStarted -= value;
        }
        /// <summary>
        /// Called when a tick was completed. Contains the tick number as parameter
        /// </summary>
        public static event Action<uint> OnTickCompleted
        {
            add => NetworkManager.OnTickCompleted += value;
            remove => NetworkManager.OnTickCompleted -= value;
        }

        private static NetworkManager _networkManager;
        /// <summary>
        /// Instance of the internal network manager held by the static context 
        /// </summary>
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
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode || !IsOnline) return;
                EditorApplication.isPlaying = false;
                Debug.LogWarning("Play mode is not possible while the static network manager is online!");
            };
#endif
        }

        /// <summary>
        /// This method calls the transport's internal tick method, updating connections and
        /// incoming and outgoing packets.
        /// </summary>
        /// <remarks>
        /// Calling this method will disable automatic ticks in the transport settings.
        /// Only use this method if ticks are to be handled manually.
        /// </remarks>
        public static void Tick()
        {
            NetworkManager.Tick();
        }

        /// <summary>
        /// Method to start a local server
        /// </summary>
        public static void StartServer()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            NetworkManager.StartServer();
        }

        /// <summary>
        /// Method to stop the local server
        /// </summary>
        public static void StopServer()
        {
            NetworkManager.StopServer();
        }

        /// <summary>
        /// Method to start a local client
        /// </summary>
        public static void StartClient()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            NetworkManager.StartClient();
        }

        /// <summary>
        /// Method to stop the local client 
        /// </summary>
        public static void StopClient()
        {
            NetworkManager.StopClient();
        }

        /// <summary>
        /// Method to start both the local server and client
        /// </summary>
        public static void StartHost()
        {
            NetworkManager.StartHost();
        }
        
        /// <summary>
        /// Method to stop both the local server and client
        /// </summary>
        public static void StopNetwork()
        {
            NetworkManager.StopHost();
        }
    }
}
