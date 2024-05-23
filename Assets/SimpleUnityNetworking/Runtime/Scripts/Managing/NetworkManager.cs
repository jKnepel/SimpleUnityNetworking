using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using UnityEngine;

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager : INetworkManager, IDisposable
    {
        #region fields

        public Transport Transport => _transportConfiguration?.Transport;
        private TransportConfiguration _transportConfiguration;
        public TransportConfiguration TransportConfiguration
        {
            get => _transportConfiguration;
            set
            {
                if (value == _transportConfiguration) return;

                if (Transport is not null)
                {
                    Transport.Dispose();
                    Transport.OnServerStateUpdated -= HandleTransportServerStateUpdate;
                    Transport.OnClientStateUpdated -= HandleTransportClientStateUpdate;
                    Transport.OnServerReceivedData -= OnServerReceivedData;
                    Transport.OnClientReceivedData -= OnClientReceivedData;
                    Transport.OnConnectionUpdated -= OnRemoteConnectionStateUpdated;
                    Transport.OnTickStarted -= TickStarted;
                    Transport.OnTickCompleted -= TickCompleted;
                    
                    if (Logger is not null)
                        Transport.OnTransportLogAdded -= Logger.Log;
                    
                    ClientInformation = null;
                    ServerInformation = null;
                    _authenticatingClients.Clear();
                    Client_ConnectedClients.Clear();
                    Server_ConnectedClients.Clear();
                    _localClientConnectionState = ELocalClientConnectionState.Stopped;
                    _localServerConnectionState = ELocalServerConnectionState.Stopped;
                }
                _transportConfiguration = value;
                
                if (_transportConfiguration is null) return;
                Transport.OnServerStateUpdated += HandleTransportServerStateUpdate;
                Transport.OnClientStateUpdated += HandleTransportClientStateUpdate;
                Transport.OnServerReceivedData += OnServerReceivedData;
                Transport.OnClientReceivedData += OnClientReceivedData;
                Transport.OnConnectionUpdated += OnRemoteConnectionStateUpdated;
                Transport.OnTickStarted += TickStarted;
                Transport.OnTickCompleted += TickCompleted;
                
                if (Logger is not null)
                    Transport.OnTransportLogAdded += Logger.Log;
            }
        }

        public SerialiserConfiguration SerialiserConfiguration { get; set; }

        public Logger Logger => LoggerConfiguration?.Logger;
        private LoggerConfiguration _loggerConfiguration;
        public LoggerConfiguration LoggerConfiguration
        {
            get => _loggerConfiguration;
            set
            {
                if (value == _loggerConfiguration) return;

                if (Logger is not null)
                {
                    if (Transport is not null)
                        Transport.OnTransportLogAdded -= Logger.Log;
                }
                _loggerConfiguration = value;

                if (_loggerConfiguration is null) return;
                
                if (Transport is not null)
                    Transport.OnTransportLogAdded += Logger.Log;
            }
        }

        public bool IsOnline => IsServer || IsClient;
        public bool IsServer => Server_LocalState == ELocalServerConnectionState.Started;
        public bool IsClient => Client_LocalState == ELocalClientConnectionState.Authenticated;
        public bool IsHost => IsServer && IsClient;
        
        public ServerInformation ServerInformation { get; private set; }
        public ClientInformation ClientInformation { get; private set; }

        public event Action OnTickStarted;
        public event Action OnTickCompleted;
        
        private bool _disposed;

        #endregion

        #region lifecycle

        ~NetworkManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
            }
            
            Transport?.Dispose();
        }

        public void Tick()
        {
            Transport?.Tick();
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
        
        #region utilities

        private void TickStarted() => OnTickStarted?.Invoke();
        private void TickCompleted() => OnTickCompleted?.Invoke();

        #endregion
    }
}
