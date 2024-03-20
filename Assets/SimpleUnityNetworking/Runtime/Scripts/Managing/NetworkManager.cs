using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.Transporting;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager : INetworkManager, IDisposable
    {
        #region fields

        private Transport _transport;
        public Transport Transport
        {
            get => _transport;
            set
            {
                if (value == _transport) return;
                _transport?.Dispose();
                _transport = value;
                _transport.OnServerStateUpdated += HandleTransportServerStateUpdate;
                _transport.OnClientStateUpdated += HandleTransportClientStateUpdate;
                _transport.OnServerReceivedData += OnServerReceivedData;
                _transport.OnClientReceivedData += OnClientReceivedData;
                _transport.OnConnectionUpdated += OnRemoteConnectionStateUpdated;
            }
        }
        
        public bool IsOnline => IsServer || IsClient;
        public bool IsServer => Transport?.IsServer ?? false;
        public bool IsClient => Client_LocalState == ELocalClientConnectionState.Authenticated;
        public bool IsHost => IsServer && IsClient;
        
        public ServerInformation ServerInformation { get; private set; }
        public ClientInformation ClientInformation { get; private set; }

        private bool _disposed;

        #endregion

        #region lifecycle

        public NetworkManager(Transport transport = null)
        {
            Transport = transport;
        }

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
                _transport?.Dispose();
            }
        }

        public void Update()
        {
            if (_transport != null)
            {
                _transport.IterateIncoming();
                _transport.IterateOutgoing();
            }
        }

        #endregion

        #region public methods

        public void StartServer(string servername, uint maxNumberConnectedClients)
        {
            if (_transport == null)
            {
                Messaging.DebugMessage("The transport needs to be defined before a server can be started!");
                return;
            }

            _cachedServername = servername;
            _cachedMaxNumberClients = maxNumberConnectedClients;
            _transport.StartServer();
        }

        public void StopServer()
        {
            _transport?.StopServer();
        }

        public void StartClient(string username, Color32 userColor)
        {
            if (_transport == null)
            {
                Messaging.DebugMessage("The transport needs to be defined before a client can be started!");
                return;
            }

            _cachedUsername = username;
            _cachedColor = userColor;
            _transport.StartClient();
        }
        
        public void StopClient()
        {
            _transport?.StopClient();
        }

        public void StopNetwork()
        {
            StopClient();
            StopServer();
        }

        #endregion
    }
}
