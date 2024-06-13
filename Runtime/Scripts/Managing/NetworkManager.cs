using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Modules;
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

        private Transport _transport;
        public Transport Transport
        {
            get => _transport;
            private set
            {
                if (value == _transport) return;
                
                if (_transport is not null)
                {
                    _transport.Dispose();
                    _authenticatingClients.Clear();
                    Client_ConnectedClients.Clear();
                    Server_ConnectedClients.Clear();
                    Client_LocalState = ELocalClientConnectionState.Stopped;
                    Server_LocalState = ELocalServerConnectionState.Stopped;
                }
                
                _transport = value;
                
                if (_transport is null) return;
                _transport.OnServerStateUpdated += HandleTransportServerStateUpdate;
                _transport.OnClientStateUpdated += HandleTransportClientStateUpdate;
                _transport.OnServerReceivedData += OnServerReceivedData;
                _transport.OnClientReceivedData += OnClientReceivedData;
                _transport.OnConnectionUpdated += OnRemoteConnectionStateUpdated;
                _transport.OnTickStarted += TickStarted;
                _transport.OnTickCompleted += TickCompleted;
                
                if (Logger is not null)
                    _transport.OnTransportLogAdded += Logger.Log;
            }
        }
        private TransportConfiguration _transportConfiguration;
        public TransportConfiguration TransportConfiguration
        {
            get => _transportConfiguration;
            set
            {
                if (value == _transportConfiguration) return;
                if (IsOnline)
                {
                    Debug.LogError("Can't change the configuration while a local connection is established!");
                    return;
                }
                
                _transportConfiguration = value;
                if (_transportConfiguration is not null)
                    Transport = _transportConfiguration.GetTransport();
            }
        }

        public SerialiserSettings SerialiserSettings { get; private set; }
        private SerialiserConfiguration _serialiserConfiguration;
        public SerialiserConfiguration SerialiserConfiguration
        {
            get => _serialiserConfiguration;
            set
            {
                if (value == _serialiserConfiguration) return;
                if (IsOnline)
                {
                    Debug.LogError("Can't change the configuration while a local connection is established!");
                    return;
                }
                
                _serialiserConfiguration = value;
                if (_serialiserConfiguration is not null)
                    SerialiserSettings = _serialiserConfiguration.Settings;
            }
        }

        private Logger _logger;
        public Logger Logger
        {
            get => _logger;
            private set
            {
                if (value == _logger) return;
                if (_logger is not null && Transport is not null)
                    Transport.OnTransportLogAdded -= Logger.Log;

                _logger = value;
                if (_logger is not null && Transport is not null)
                    Transport.OnTransportLogAdded += Logger.Log;
            }
        }
        private LoggerConfiguration _loggerConfiguration;
        public LoggerConfiguration LoggerConfiguration
        {
            get => _loggerConfiguration;
            set
            {
                if (value == _loggerConfiguration) return;
                if (IsOnline)
                {
                    Debug.LogError("Can't change the configuration while a local connection is established!");
                    return;
                }
                
                _loggerConfiguration = value;
                if (_loggerConfiguration is not null)
                    Logger = _loggerConfiguration.GetLogger();
            }
        }

        public Module Module { get; private set; }
        private ModuleConfiguration _moduleConfiguration;
        public ModuleConfiguration ModuleConfiguration
        {
            get => _moduleConfiguration;
            set
            {
                if (_moduleConfiguration == value) return;
                if (value is null && Module is not null)
                {
                    Module.Dispose();
                    Module = null;
                }

                _moduleConfiguration = value;
                if (_moduleConfiguration is not null)
                    Module = _moduleConfiguration.GetModule(this);
            }
        }

        public bool IsServer => Server_LocalState == ELocalServerConnectionState.Started;
        public bool IsClient => Client_LocalState == ELocalClientConnectionState.Authenticated;
        public bool IsOnline => IsServer || IsClient;
        public bool IsHost => IsServer && IsClient;

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
                Module?.Dispose();
                Transport?.Dispose();
            }
        }

        public void Tick()
        {
            Transport?.Tick();
        }

        #endregion

        #region public methods

        public void StartServer(string servername)
        {
            if (TransportConfiguration == null)
            {
                Debug.LogError("The transport needs to be defined before a server can be started!");
                return;
            }

            _cachedServername = servername;
            Transport?.StartServer();
        }

        public void StopServer()
        {
            Transport?.StopServer();
        }

        public void StartClient(string username, Color32 userColour)
        {
            if (TransportConfiguration == null)
            {
                Debug.LogError("The transport needs to be defined before a client can be started!");
                return; 
            }
            
            _cachedUsername = username;
            _cachedUserColour = userColour;
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
        
        private delegate void ByteDataCallback(uint senderID, byte[] data);
        private delegate void StructDataCallback(uint senderID, byte[] data);
        
        private ByteDataCallback CreateByteDataDelegate(Action<uint, byte[]> callback)
        {
            return ParseDelegate;
            void ParseDelegate(uint senderID, byte[] data)
            {
                callback?.Invoke(senderID, data);
            }
        }
        
        private StructDataCallback CreateStructDataDelegate<T>(Action<uint, T> callback)
        {
            return ParseDelegate;
            void ParseDelegate(uint senderID, byte[] data)
            {
                Reader reader = new(data, SerialiserSettings);
                callback?.Invoke(senderID, reader.Read<T>());
            }
        }

        private void TickStarted() => OnTickStarted?.Invoke();
        private void TickCompleted() => OnTickCompleted?.Invoke();

        #endregion
    }
}
