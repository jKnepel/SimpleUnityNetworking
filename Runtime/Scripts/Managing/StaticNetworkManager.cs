using jKnepel.SimpleUnityNetworking.Logging;
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
        /// The instance of the local server, which provides access to the server's API, values and events
        /// </summary>
        public static Server Server => _networkManager.Server;
        /// <summary>
        /// The instance of the local client, which provides access to the client's API, values and events
        /// </summary>
        public static Client Client => _networkManager.Client;

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
        /// /// <remarks>
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
        /// /// <remarks>
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
        /// /// <remarks>
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
        /// /// <remarks>
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
        /// /// <remarks>
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
        /// /// <remarks>
        /// Should be ignored unless you specifically want to use transport layer data
        /// </remarks>
        public static event Action<string, EMessageSeverity> OnTransportLogAdded
        {
            add => NetworkManager.OnTransportLogAdded += value;
            remove => NetworkManager.OnTransportLogAdded -= value;
        }
        /// <summary>
        /// Called when a tick was started
        /// </summary>
        public static event Action OnTickStarted
        {
            add => NetworkManager.OnTickStarted += value;
            remove => NetworkManager.OnTickStarted -= value;
        }
        /// <summary>
        /// Called when a tick was completed
        /// </summary>
        public static event Action OnTickCompleted
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
            EditorApplication.playModeStateChanged += PreventPlayMode;
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
            _networkManager.Tick();
        }

        /// <summary>
        /// Method to start a local server
        /// </summary>
        public static void StartServer()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            _networkManager.StartServer();
        }

        /// <summary>
        /// Method to stop the local server
        /// </summary>
        public static void StopServer()
        {
            _networkManager.StopServer();
        }

        /// <summary>
        /// Method to start a local client
        /// </summary>
        public static void StartClient()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) return;
#endif
            _networkManager.StartClient();
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
