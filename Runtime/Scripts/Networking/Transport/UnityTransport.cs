using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    public class UnityTransport : Transport, IDisposable
    {
        private readonly struct SendTarget : IEquatable<SendTarget>
        {
            public readonly NetworkConnection Connection;
            // TODO : pipeline
            
            public SendTarget(NetworkConnection conn)
            {
                Connection = conn;
            }
            
            public bool Equals(SendTarget other)
            {
                return other.Connection.Equals(Connection);
            }

            public override bool Equals(object obj)
            {
                return obj is SendTarget other && Equals(other);
            }

            public override int GetHashCode()
            {
                return Connection.GetHashCode();
            }
        }
        
        // TODO : implement batching
        private struct SendQueue
        {
            private NativeQueue<NativeArray<byte>> _messages;

            public int Count => _messages.Count;

            public void Enqueue(NativeArray<byte> message)
            {
                if (_messages.IsCreated)
                {
                    _messages = new(Allocator.Persistent);
                }
                _messages.Enqueue(message);
            }

            public NativeArray<byte> Dequeue()
            {
                return _messages.Dequeue();
            }
        }

        [BurstCompile]
        private struct SendQueueJob : IJob
        {
            public NetworkDriver.Concurrent Driver;
            public SendTarget Target;
            public SendQueue Queue;
            
            public void Execute()
            {
                while (Queue.Count > 0)
                {
                    int result = Driver.BeginSend(Target.Connection, out var writer);
                    if (result != (int)StatusCode.Success)
                    {
                        Debug.LogError($"Sending data failed: {result}");
                        return;
                    }

                    var data = Queue.Dequeue();
                    writer.WriteBytes(data);
                    result = Driver.EndSend(writer);
                    
                    if (result == data.Length) return;

                    if (result == (int)StatusCode.NetworkSendQueueFull)
                    {
                        Queue.Enqueue(data);
                    }
                    
                    Debug.LogError("Error sending a message!");
                    // TODO : handle error
                }
            }
        }
        
        #region fields
        
        private bool _disposed;
        
        private ConnectionData _connectionData;
        
        private NetworkDriver _driver;
        private NetworkSettings _networkSettings;
        
        private Dictionary<SendTarget, SendQueue> _outgoingMessages = new();

        private Dictionary<int, NetworkConnection> _clientIDToConnection;
        private Dictionary<NetworkConnection, int> _connectionToClientID;
        private int _clientIDs;
        
        private NetworkConnection _serverConnection;
        
        private int _hostClientID; // client ID that the hosting server assigns its local client

        private ELocalConnectionState _serverState = ELocalConnectionState.Stopped;
        private ELocalConnectionState ServerState
        {
            get => _serverState;
            set
            {
                if (_serverState == value) return;
                _serverState = value;
                OnServerStateUpdated?.Invoke(_serverState);
            }
        }
        private ELocalConnectionState _clientState = ELocalConnectionState.Stopped;
        private ELocalConnectionState ClientState
        {
            get => _clientState;
            set
            {
                if (_clientState == value) return;
                _clientState = value;
                OnClientStateUpdated?.Invoke(_clientState);
            }
        }

        public override bool IsServer => ServerState == ELocalConnectionState.Started;
        public override bool IsClient => ClientState == ELocalConnectionState.Started;
        public override bool IsOnline => IsServer || IsClient;
        public override bool IsHost => IsServer && IsClient;

        public override event Action<ServerReceivedData> OnServerReceivedData;
        public override event Action<ClientReceivedData> OnClientReceivedData;
        public override event Action<ELocalConnectionState> OnServerStateUpdated;
        public override event Action<ELocalConnectionState> OnClientStateUpdated;
        public override event Action<int, ERemoteConnectionState> OnConnectionUpdated;

        #endregion
        
        #region lifecycle
        
        public UnityTransport(ConnectionData config)
        {
            _connectionData = config;
        }
        
        ~UnityTransport()
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
                _serverState = ELocalConnectionState.Stopped;
                _clientState = ELocalConnectionState.Stopped;
                _clientIDToConnection = null;
                _connectionToClientID = null;
                _serverConnection = default;
                _clientIDs = 0;
                _hostClientID = 0;
            }
            
            if (_driver.IsCreated) _driver.Dispose();
            if (_networkSettings.IsCreated) _networkSettings.Dispose();

            _disposed = true;
        }

        public override void StartServer()
        {
            if (ServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                Debug.LogError("Failed to start the server, there already exists a local server.");
                return;
            }

            ServerState = ELocalConnectionState.Starting;

            var endpoint = ParseNetworkEndpoint(_connectionData.LocalAddress, _connectionData.LocalPort);
            if (endpoint.Family == NetworkFamily.Invalid)
            {
                ServerState = ELocalConnectionState.Stopping;
                Debug.LogError("The given local or remote address use an invalid IP family.");
                ServerState = ELocalConnectionState.Stopped;
                return;
            }
            
            _networkSettings = new(Allocator.Persistent);
            _driver = NetworkDriver.Create(_networkSettings);
            _clientIDToConnection = new();
            _connectionToClientID = new();
            
            if (_driver.Bind(endpoint) != 0)
            {
                ServerState = ELocalConnectionState.Stopping;
                Debug.LogError($"Failed to bind server to local address {endpoint.Address} and port {endpoint.Port}");
                _driver.Dispose();
                _networkSettings.Dispose();
                ServerState = ELocalConnectionState.Stopped;
                return;
            }

            _driver.Listen();
            ServerState = ELocalConnectionState.Started;
        }

        public override void StopServer()
        {
            if (ServerState is ELocalConnectionState.Stopping or ELocalConnectionState.Stopped) return;
            if (ClientState is ELocalConnectionState.Starting or ELocalConnectionState.Started) 
                StopHostClient();
            
            ServerState = ELocalConnectionState.Stopping;

            var conns = _clientIDToConnection.Values.ToArray();
            foreach (var conn in conns)
            {
                if (conn.GetState(_driver) == NetworkConnection.State.Disconnected) continue;
                _driver.Disconnect(conn);
                while (_driver.PopEventForConnection(conn, out _) != NetworkEvent.Type.Empty) {}
                // TODO : flush send queue
            }

            _clientIDs = 0;
            _clientIDToConnection.Clear();
            _connectionToClientID.Clear();
            _driver.Dispose();
            _networkSettings.Dispose();

            ServerState = ELocalConnectionState.Stopped;
        }

        public override void StartClient()
        {
            if (ClientState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                Debug.LogError("Failed to start the client, there already exists a local client.");
                return;
            }

            if (ServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                StartHostClient();
                return;
            }
            
            ClientState = ELocalConnectionState.Starting;
            
            var localEndpoint = ParseNetworkEndpoint(_connectionData.LocalAddress, _connectionData.LocalPort);
            var serverEndpoint = ParseNetworkEndpoint(_connectionData.Address, _connectionData.Port);
            if (localEndpoint.Family == NetworkFamily.Invalid)
            {
                ClientState = ELocalConnectionState.Stopping;
                Debug.LogError("The given local or remote address use an invalid IP family.");
                ClientState = ELocalConnectionState.Stopped;
                return;
            }
            if (localEndpoint.Family != serverEndpoint.Family)
            {
                ClientState = ELocalConnectionState.Stopping;
                Debug.LogError("The given local and remote addresses don't possess matching IP families.");
                ClientState = ELocalConnectionState.Stopped;
                return;
            }
            
            _networkSettings = new(Allocator.Persistent);
            _driver = NetworkDriver.Create(_networkSettings);
            
            if (_driver.Bind(localEndpoint) != 0)
            {
                ClientState = ELocalConnectionState.Stopping;
                Debug.LogError($"Failed to bind client to local address {localEndpoint.Address} and port {localEndpoint.Port}");
                _driver.Dispose();
                _networkSettings.Dispose();
                ClientState = ELocalConnectionState.Stopped;
                return;
            }

            _serverConnection = _driver.Connect(serverEndpoint);
        }

        public override void StopClient()
        {
            if (ClientState is ELocalConnectionState.Stopping or ELocalConnectionState.Stopped) return;
            if (ServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                StopHostClient();
                return;
            }

            ClientState = ELocalConnectionState.Stopping;
            
            // TODO : flush send queue
            if (_driver.Disconnect(_serverConnection) == 0)
            {
                // TODO : flush receive queues
            }

            ClientState = ELocalConnectionState.Stopped;
        }

        public override void StopNetwork()
        {
            StopClient();
            StopServer();
        }

        private void StartHostClient()
        {
            ClientState = ELocalConnectionState.Starting;
            _hostClientID = ++_clientIDs;
            OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Connected);
            ClientState = ELocalConnectionState.Started;
        }

        private void StopHostClient()
        {
            ClientState = ELocalConnectionState.Stopping;
            OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Disconnected);
            _hostClientID = 0;
            ClientState = ELocalConnectionState.Stopped;
        }

        public override void DisconnectClient(int clientID)
        {
            if (!IsServer)
            {
                Debug.LogError("The server has to be started to disconnect a client.");
                return;
            }

            if (IsHost && clientID == _hostClientID)
            {
                // TODO : flush messages
                OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Disconnected);
                _hostClientID = 0;
                ClientState = ELocalConnectionState.Stopping;
                ClientState = ELocalConnectionState.Stopped;
                return;
            }

            if (!_clientIDToConnection.TryGetValue(clientID, out var conn))
            {
                Debug.LogError($"The client with the ID {clientID} does not exist");
                return;
            }

            if (_driver.GetConnectionState(conn) != NetworkConnection.State.Disconnected)
            {
                _driver.Disconnect(conn);
                while (_driver.PopEventForConnection(conn, out _) != NetworkEvent.Type.Empty) {}
            }
            
            OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Disconnected);
        }
        
        #endregion
        
        #region incoming

        public override void IterateIncoming()
        {
            if (!_driver.IsCreated) return;

            _driver.ScheduleUpdate().Complete();
            
            while (AcceptConnection() && _driver.IsCreated) {}
            while (ProcessEvent() && _driver.IsCreated) {}
        }

        private bool AcceptConnection()
        {
            var conn = _driver.Accept();
            if (conn == default) return false;
            
            // TODO : check if space is left
            int clientID = ++_clientIDs;
            _clientIDToConnection[clientID] = conn;
            _connectionToClientID[conn] = clientID;
            OnConnectionUpdated?.Invoke(clientID, ERemoteConnectionState.Connected);
            
            return true;
        }

        private bool ProcessEvent()
        {
            var eventType = _driver.PopEvent(out var conn, out var reader, out var pipe);
            switch (eventType)
            {
                case NetworkEvent.Type.Empty:
                    break;
                case NetworkEvent.Type.Data:
                    HandleData(conn, reader, pipe);
                    break;
                case NetworkEvent.Type.Connect:
                    ClientState = ELocalConnectionState.Started;
                    break;
                case NetworkEvent.Type.Disconnect:
                    if (ClientState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
                    {
                        ClientState = ELocalConnectionState.Stopping;
                        _driver.Dispose();
                        _networkSettings.Dispose();
                        ClientState = ELocalConnectionState.Stopped;
                        // TODO : flush receive queues
                        // TODO : flush send queues
                    }
                    else if (ServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
                    {
                        // TODO : flush send queues
                        _connectionToClientID.Remove(conn, out var clientID);
                        _clientIDToConnection.Remove(clientID);
                        OnConnectionUpdated?.Invoke(clientID, ERemoteConnectionState.Disconnected);
                    }
                    // TODO : handle reason
                    break;
            }

            return false;
        }

        private void HandleData(NetworkConnection conn, DataStreamReader reader, NetworkPipeline pipe)
        {
            byte[] data = new byte[reader.Length];
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    reader.ReadBytesUnsafe(dataPtr, reader.Length);
                }
            }

            if (IsServer)
            {
                OnServerReceivedData?.Invoke(new()
                {
                    ClientID = _connectionToClientID[conn],
                    Data = data,
                    Timestamp = DateTime.Now
                });
            }
            else if (IsClient)
            {
                OnClientReceivedData?.Invoke(new()
                {
                    Data = data,
                    Timestamp = DateTime.Now
                });
            }
        }
        
        #endregion
        
        #region outgoing

        public override void SendDataToServer(byte[] data)
        {
            if (IsHost)
            {
                OnServerReceivedData?.Invoke(new()
                {
                    ClientID = _hostClientID,
                    Data = data,
                    Timestamp = DateTime.Now
                });
                return;
            }
            
            if (!IsClient)
            {
                Debug.LogError("The local client has to be started to send data to the server.");
                return;
            }

            SendTarget target = new(_serverConnection);
            if (!_outgoingMessages.TryGetValue(target, out var queue))
            {
                _outgoingMessages[target] = queue = new();
            }

            queue.Enqueue(new(data, Allocator.Persistent));
        }

        public override void SendDataToClient(int clientID, byte[] data)
        {
            if (!IsServer)
            {
                Debug.LogError("The server has to be started to send data to clients.");
                return;
            }

            if (IsHost && clientID == _hostClientID)
            {
                OnClientReceivedData?.Invoke(new()
                {
                    Data = data,
                    Timestamp = DateTime.Now
                });
                return;
            }

            if (!_clientIDToConnection.TryGetValue(clientID, out var conn))
            {
                Debug.LogError($"The client with the ID {clientID} does not exist");
                // TODO : handle failed sends
                return;
            }
            
            SendTarget target = new(conn);
            if (!_outgoingMessages.TryGetValue(target, out var queue))
            {
                _outgoingMessages[target] = queue = new();
            }

            queue.Enqueue(new(data, Allocator.Persistent));
        }

        public override void IterateOutgoing()
        {
            if (!_driver.IsCreated) return;

            foreach (var queues in _outgoingMessages)
            {
                if (!_driver.IsCreated) return;
                new SendQueueJob
                {
                    Driver = _driver.ToConcurrent(),
                    Target = queues.Key,
                    Queue = queues.Value
                }.Run();
            }
        }
        
        #endregion
        
        #region utilities
        
        private static NetworkEndpoint ParseNetworkEndpoint(string ip, ushort port)
        {
            NetworkEndpoint endpoint = default;

            if (NetworkEndpoint.TryParse(ip, port, out endpoint, NetworkFamily.Ipv4) ||
                NetworkEndpoint.TryParse(ip, port, out endpoint, NetworkFamily.Ipv6)) return endpoint;

            return endpoint;
        }
        
        #endregion
    }
}
