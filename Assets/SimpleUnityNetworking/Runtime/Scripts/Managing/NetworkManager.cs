using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialising;
using jKnepel.SimpleUnityNetworking.Transporting;
using System;
using UnityEngine;

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager : INetworkManager, IDisposable
    {
        #region fields

        private Transport Transport => _transportConfiguration?.Transport;
        private TransportConfiguration _transportConfiguration;
        public TransportConfiguration TransportConfiguration
        {
            get => _transportConfiguration;
            set
            {
                if (value == _transportConfiguration) return;
                Transport?.Dispose();
                _transportConfiguration = value;

                if (_transportConfiguration is null) return;
                Transport.OnServerStateUpdated += HandleTransportServerStateUpdate;
                Transport.OnClientStateUpdated += HandleTransportClientStateUpdate;
                Transport.OnServerReceivedData += OnServerReceivedData;
                Transport.OnClientReceivedData += OnClientReceivedData;
                Transport.OnConnectionUpdated += OnRemoteConnectionStateUpdated;
            }
        }

        public SerialiserConfiguration SerialiserConfiguration { get; set; }

        private Logger Logger => LoggerConfiguration.Logger;
        private LoggerConfiguration _loggerConfiguration;
        public LoggerConfiguration LoggerConfiguration
        {
            get => _loggerConfiguration;
            set
            {
                if (value == _loggerConfiguration) return;
                _loggerConfiguration = value;
                Logger.OnMessageAdded += msg => OnLogMessageAdded?.Invoke(msg);
            }
        }

        public bool IsOnline => IsServer || IsClient;
        public bool IsServer => Transport?.IsServer ?? false;
        public bool IsClient => Client_LocalState == ELocalClientConnectionState.Authenticated;
        public bool IsHost => IsServer && IsClient;
        
        public ServerInformation ServerInformation { get; private set; }
        public ClientInformation ClientInformation { get; private set; }
        
        public event Action<Message> OnLogMessageAdded;

        private bool _disposed;

        #endregion

        #region lifecycle

        ~NetworkManager()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
            }
            
            Transport?.Dispose();
        }

        public void Update()
        {
            if (Transport != null)
            {
                Transport?.IterateIncoming();
                Transport?.IterateOutgoing();
            }
        }

        #endregion

        #region public methods

        public void StartServer(string servername)
        {
            if (Transport == null)
            {
                Logger?.Log("The transport needs to be defined before a server can be started!");
                return;
            }

            _cachedServername = servername;
            _cachedMaxNumberClients = TransportConfiguration.Settings.MaxNumberOfClients;
            Transport?.StartServer();
        }

        public void StopServer()
        {
            Transport?.StopServer();
        }

        public void StartClient(string username, Color32 userColour)
        {
            if (Transport == null)
            {
                Logger?.Log("The transport needs to be defined before a client can be started!");
                return;
            }

            _cachedUsername = username;
            _cachedColour = userColour;
            Transport?.StartClient();
        }
        
        public void StopClient()
        {
            Transport?.StopClient();
        }

        public void StopNetwork()
        {
            StopClient();
            StopServer();
        }

        #endregion
    }
}
