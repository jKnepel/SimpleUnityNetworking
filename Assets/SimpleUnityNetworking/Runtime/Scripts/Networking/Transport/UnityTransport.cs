using System;
using System.Collections.Generic;
using System.Linq;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Utilities;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;
using Unity.Networking.Transport.Utilities;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    public sealed class UnityTransport : Transport
    {
        private readonly struct SendTarget : IEquatable<SendTarget>
        {
            public readonly NetworkConnection Connection;
            public readonly NetworkPipeline Pipeline;
            
            public SendTarget(NetworkConnection conn, NetworkPipeline pipe)
            {
                Connection = conn;
                Pipeline = pipe;
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
                return HashCode.Combine(Connection, Pipeline);
            }
        }
        
        // TODO : implement batching
        private struct SendQueue : IDisposable
        {
            private NativeQueue<NativeArray<byte>> _messages;

            public int Count => _messages.Count;

            public SendQueue(int i)
            {
                _messages = new(Allocator.Persistent);
            }

            public void Dispose()
            {
                if (_messages.IsCreated)
                    _messages.Dispose();
            }

            public void Enqueue(NativeArray<byte> message)
            {
                _messages.Enqueue(message);
            }

            public NativeArray<byte> Dequeue()
            {
                return _messages.Dequeue();
            }

            public NativeArray<byte> Peek()
            {
                return _messages.Peek();
            }
        }

        /*
         * TODO : implement once message batching works
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
                    var result = Driver.BeginSend(Target.Pipeline, Target.Connection, out var writer);
                    if (result != (int)StatusCode.Success)
                    {
                        Debug.LogError($"Sending data failed: {result}");
                        return;
                    }

                    var data = Queue.Peek();
                    writer.WriteBytes(data);
                    result = Driver.EndSend(writer);

                    if (result == data.Length)
                    {
                        Queue.Dequeue();
                        continue;
                    }

                    if (result != (int)StatusCode.NetworkSendQueueFull)
                    {
                        Debug.LogError("Error sending a message!");
                        Queue.Dequeue();
                        // TODO : handle error
                    }

                    return;
                }
            }
        }
        */
        
        #region fields
        
        private bool _disposed;
        
        private TransportSettings _settings;
        private int _maxNumberOfClients;
        
        private NetworkDriver _driver;
        private NetworkSettings _networkSettings;
        
        private readonly Dictionary<SendTarget, SendQueue> _outgoingMessages = new();

        private NetworkPipeline _unreliableSequencedPipeline;
        private NetworkPipeline _reliablePipeline;
        private NetworkPipeline _reliableSequencedPipeline;

        private Dictionary<uint, NetworkConnection> _clientIDToConnection;
        private Dictionary<NetworkConnection, uint> _connectionToClientID;
        private uint _clientIDs;
        
        private NetworkConnection _serverConnection;
        
        private uint _hostClientID; // client ID that the hosting server assigns its local client

        private ELocalConnectionState _serverState = ELocalConnectionState.Stopped;
        private ELocalConnectionState _clientState = ELocalConnectionState.Stopped;
        
        public override ELocalConnectionState LocalServerState => _serverState;
        public override ELocalConnectionState LocalClientState => _clientState;

        public override bool IsServer => LocalServerState == ELocalConnectionState.Started;
        public override bool IsClient => LocalClientState == ELocalConnectionState.Started;
        public override bool IsOnline => IsServer || IsClient;
        public override bool IsHost => IsServer && IsClient;

        public override event Action<ServerReceivedData> OnServerReceivedData;
        public override event Action<ClientReceivedData> OnClientReceivedData;
        public override event Action<ELocalConnectionState> OnServerStateUpdated;
        public override event Action<ELocalConnectionState> OnClientStateUpdated;
        public override event Action<uint, ERemoteConnectionState> OnConnectionUpdated;

        #endregion
        
        #region lifecycle
        
        public UnityTransport(TransportSettings settings = null)
        {
            SetTransportSettings(settings);
        }
        
        protected override void Dispose(bool disposing)
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

            foreach (var queue in _outgoingMessages.Values)
                queue.Dispose();
            // TODO : clean/flush outgoing messages
            DisposeDrivers();

            _disposed = true;
        }

        public override void SetTransportSettings(TransportSettings settings)
        {
            _settings = settings;
        }

        public override void StartServer()
        {
            if (LocalServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                Debug.LogError("Failed to start the server, there already exists a local server.");
                return;
            }

            SetLocalServerState(ELocalConnectionState.Starting);

            var port = _settings.Port == 0 ? NetworkUtilities.FindNextAvailablePort() : _settings.Port;
            NetworkEndpoint endpoint = default;
            
            if (!string.IsNullOrEmpty(_settings.ServerListenAddress))
            {
                if (!NetworkEndpoint.TryParse(_settings.Address, port, out endpoint))
                    NetworkEndpoint.TryParse(_settings.Address, port, out endpoint, NetworkFamily.Ipv6);
            }
            else
            {
                endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port);
            }
            
            if (endpoint.Family == NetworkFamily.Invalid)
            {
                SetLocalServerState(ELocalConnectionState.Stopping);
                Debug.LogError("The given local or remote address use an invalid IP family.");
                SetLocalServerState(ELocalConnectionState.Stopped);
                return;
            }
            
            InitialiseDrivers();
            
            if (_driver.Bind(endpoint) != 0)
            {
                SetLocalServerState(ELocalConnectionState.Stopping);
                Debug.LogError($"Failed to bind server to local address {endpoint.Address} and port {endpoint.Port}");
                DisposeDrivers();
                SetLocalServerState(ELocalConnectionState.Stopped);
                return;
            }

            _maxNumberOfClients = _settings.MaxNumberOfClients;
            _clientIDToConnection = new();
            _connectionToClientID = new();
            _driver.Listen();
            SetLocalServerState(ELocalConnectionState.Started);
        }

        public override void StopServer()
        {
            if (LocalServerState is ELocalConnectionState.Stopping or ELocalConnectionState.Stopped) return;
            if (LocalClientState is ELocalConnectionState.Starting or ELocalConnectionState.Started) 
                StopHostClient();
            
            SetLocalServerState(ELocalConnectionState.Stopping);

            var conns = _clientIDToConnection.Values.ToArray();
            foreach (var conn in conns)
            {
                if (conn.GetState(_driver) == NetworkConnection.State.Disconnected) continue;
                // TODO : flush send queue
                CleanOutgoingMessages(conn);
                _driver.Disconnect(conn);
                while (_driver.PopEventForConnection(conn, out _) != NetworkEvent.Type.Empty) {}
            }

            _driver.ScheduleUpdate().Complete();
            _clientIDToConnection = null;
            _connectionToClientID = null;
            _clientIDs = 0;
            DisposeDrivers();

            SetLocalServerState(ELocalConnectionState.Stopped);
        }

        public override void StartClient()
        {
            if (LocalClientState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                Debug.LogError("Failed to start the client, there already exists a local client.");
                return;
            }

            if (LocalServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                StartHostClient();
                return;
            }
            
            SetLocalClientState(ELocalConnectionState.Starting);

            if (!NetworkEndpoint.TryParse(_settings.Address, _settings.Port, out var serverEndpoint))
                NetworkEndpoint.TryParse(_settings.Address, _settings.Port, out serverEndpoint, NetworkFamily.Ipv6);
            if (serverEndpoint == default || serverEndpoint.Family == NetworkFamily.Invalid)
            {
                SetLocalClientState(ELocalConnectionState.Stopping);
                Debug.LogError("The server address is invalid.");
                SetLocalClientState(ELocalConnectionState.Stopped);
                return;
            }

            InitialiseDrivers();
            
            var localEndpoint = serverEndpoint.Family == NetworkFamily.Ipv4 ? NetworkEndpoint.AnyIpv4 : NetworkEndpoint.AnyIpv6;
            if (_driver.Bind(localEndpoint) != 0)
            {
                SetLocalClientState(ELocalConnectionState.Stopping);
                Debug.LogError($"Failed to bind client to local address {localEndpoint.Address} and port {localEndpoint.Port}");
                DisposeDrivers();
                SetLocalClientState(ELocalConnectionState.Stopped);
                return;
            }

            _serverConnection = _driver.Connect(serverEndpoint);
        }

        public override void StopClient()
        {
            if (LocalClientState is ELocalConnectionState.Stopping or ELocalConnectionState.Stopped) return;
            if (LocalServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
            {
                StopHostClient();
                return;
            }

            SetLocalClientState(ELocalConnectionState.Stopping);
            
            // TODO : flush send queue
            CleanOutgoingMessages(_serverConnection);
            if (_driver.Disconnect(_serverConnection) == 0)
            {
            }
            
            _driver.ScheduleUpdate().Complete();
            _serverConnection = default;
            DisposeDrivers();

            SetLocalClientState(ELocalConnectionState.Stopped);
        }

        public override void StopNetwork()
        {
            StopClient();
            StopServer();
        }

        private void StartHostClient()
        {
            SetLocalClientState(ELocalConnectionState.Starting);
            
            if (_clientIDToConnection.Count >= _maxNumberOfClients)
            {
                SetLocalClientState(ELocalConnectionState.Stopping);
                Debug.LogError("Maximum number of clients reached. Server cannot accept the connection.");
                SetLocalClientState(ELocalConnectionState.Stopped);
                return;
            }
            
            _hostClientID = _clientIDs++;
            SetLocalClientState(ELocalConnectionState.Started);
            OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Connected);
        }

        private void StopHostClient()
        {
            SetLocalClientState(ELocalConnectionState.Stopping);
            OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Disconnected);
            _hostClientID = 0;
            SetLocalClientState(ELocalConnectionState.Stopped);
        }

        public override void DisconnectClient(uint clientID)
        {
            if (!IsServer)
            {
                Debug.LogError("The server has to be started to disconnect a client.");
                return;
            }

            if (IsHost && clientID == _hostClientID)
            {
                OnConnectionUpdated?.Invoke(_hostClientID, ERemoteConnectionState.Disconnected);
                _hostClientID = 0;
                SetLocalClientState(ELocalConnectionState.Stopping);
                SetLocalClientState(ELocalConnectionState.Stopped);
                return;
            }

            if (!_clientIDToConnection.TryGetValue(clientID, out var conn))
            {
                Debug.LogError($"The client with the ID {clientID} does not exist");
                return;
            }
            
            if (_driver.GetConnectionState(conn) != NetworkConnection.State.Disconnected)
            {
                // TODO : flush send queues
                CleanOutgoingMessages(conn);
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

            int numberOfConnectedClients = _clientIDToConnection.Count;
            if (IsHost) numberOfConnectedClients++;
            if (numberOfConnectedClients >= _maxNumberOfClients)
            {
                _driver.Disconnect(conn);
                while (_driver.PopEventForConnection(conn, out _) != NetworkEvent.Type.Empty) {}
            }
            
            uint clientID = _clientIDs++;
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
                    SetLocalClientState(ELocalConnectionState.Started);
                    break;
                case NetworkEvent.Type.Disconnect:
                    if (LocalClientState is ELocalConnectionState.Starting or ELocalConnectionState.Started
                        && conn.Equals(_serverConnection))
                    {
                        SetLocalClientState(ELocalConnectionState.Stopping);
                        // TODO : flush send queues
                        CleanOutgoingMessages(_serverConnection);
                        DisposeDrivers();
                        SetLocalClientState(ELocalConnectionState.Stopped);
                    }
                    else if (LocalServerState is ELocalConnectionState.Starting or ELocalConnectionState.Started)
                    {
                        // TODO : flush send queues
                        CleanOutgoingMessages(_serverConnection);
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

            if (IsClient && conn.Equals(_serverConnection))
            {
                OnClientReceivedData?.Invoke(new()
                {
                    Data = data,
                    Timestamp = DateTime.Now,
                    Channel = ParseChannelPipeline(pipe)
                });
            }
            else if (IsServer)
            {
                OnServerReceivedData?.Invoke(new()
                {
                    ClientID = _connectionToClientID[conn],
                    Data = data,
                    Timestamp = DateTime.Now,
                    Channel = ParseChannelPipeline(pipe)
                });
            }
        }
        
        #endregion
        
        #region outgoing

        public override void SendDataToServer(byte[] data, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (IsHost)
            {
                OnServerReceivedData?.Invoke(new()
                {
                    ClientID = _hostClientID,
                    Data = data,
                    Timestamp = DateTime.Now,
                    Channel = channel
                });
                return;
            }
            
            if (!IsClient)
            {
                Debug.LogError("The local client has to be started to send data to the server.");
                return;
            }

            SendTarget target = new(_serverConnection, ParseChannelPipeline(channel));
            if (!_outgoingMessages.TryGetValue(target, out var queue))
            {
                _outgoingMessages[target] = queue = new(1);
            }

            queue.Enqueue(new(data, Allocator.Persistent));
        }

        public override void SendDataToClient(uint clientID, byte[] data, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (IsHost && clientID == _hostClientID)
            {
                OnClientReceivedData?.Invoke(new()
                {
                    Data = data,
                    Timestamp = DateTime.Now,
                    Channel = channel
                });
                return;
            }
            
            if (!IsServer)
            {
                Debug.LogError("The server has to be started to send data to clients.");
                return;
            }

            if (!_clientIDToConnection.TryGetValue(clientID, out var conn))
            {
                Debug.LogError($"The client with the ID {clientID} does not exist!");
                // TODO : handle failed sends
                return;
            }
            
            SendTarget target = new(conn, ParseChannelPipeline(channel));
            if (!_outgoingMessages.TryGetValue(target, out var queue))
            {
                _outgoingMessages[target] = queue = new(1);
            }

            queue.Enqueue(new(data, Allocator.Persistent));
        }

        public override void IterateOutgoing()
        {
            if (!_driver.IsCreated) return;

            foreach (var (sendTarget, sendQueue) in _outgoingMessages)
            {
                if (!_driver.IsCreated) return;

                /* TODO : implement once message batching works
                 new SendQueueJob
                 {
                    Driver = _driver.ToConcurrent(),
                    Target = sendTarget,
                    Queue = sendQueue
                 }.Run();
                 */
                
                while (sendQueue.Count > 0)
                {
                    var result = _driver.BeginSend(sendTarget.Pipeline, sendTarget.Connection, out var writer);
                    if (result != (int)StatusCode.Success)
                    {
                        Debug.LogError($"Sending data failed: {result}");
                        return;
                    }

                    var data = sendQueue.Peek();
                    writer.WriteBytes(data);
                    result = _driver.EndSend(writer);

                    if (result == data.Length)
                    {
                        sendQueue.Dequeue().Dispose();
                        continue;
                    }

                    if (result != (int)StatusCode.NetworkSendQueueFull)
                    {
                        Debug.LogError("Error sending a message!");
                        sendQueue.Dequeue().Dispose();
                        // TODO : handle error
                    }

                    return;
                }
            }
        }
        
        #endregion
        
        #region utility
        
        private void SetLocalServerState(ELocalConnectionState state)
        {
            if (_serverState == state) return;
            _serverState = state;
            OnServerStateUpdated?.Invoke(_serverState);
        }
        
        private void SetLocalClientState(ELocalConnectionState state)
        {
            if (_clientState == state) return;
            _clientState = state;
            OnClientStateUpdated?.Invoke(_clientState);
        }

        private void InitialiseDrivers()
        {
            _networkSettings = new(Allocator.Persistent);
            _networkSettings.WithNetworkConfigParameters(
                connectTimeoutMS: _settings.ConnectTimeoutMS,
                maxConnectAttempts: _settings.MaxConnectAttempts,
                disconnectTimeoutMS: _settings.DisconnectTimeoutMS,
                heartbeatTimeoutMS: _settings.HeartbeatTimeoutMS
            );
            _networkSettings.WithFragmentationStageParameters(
                payloadCapacity: _settings.PayloadCapacity
            );
            _networkSettings.WithReliableStageParameters(
                windowSize: _settings.WindowSize,
                minimumResendTime: _settings.MinimumResendTime,
                maximumResendTime: _settings.MaximumResendTime
            );
            
            _driver = NetworkDriver.Create(_networkSettings);
            _unreliableSequencedPipeline = _driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
            _reliablePipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage));
            _reliableSequencedPipeline = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        }

        private void DisposeDrivers()
        {
            if (_driver.IsCreated) _driver.Dispose();
            if (_networkSettings.IsCreated) _networkSettings.Dispose();
        }

        private NetworkPipeline ParseChannelPipeline(ENetworkChannel channel)
        {
            return channel switch
            {
                ENetworkChannel.ReliableOrdered => _reliableSequencedPipeline,
                ENetworkChannel.ReliableUnordered => _reliablePipeline,
                ENetworkChannel.UnreliableOrdered => _unreliableSequencedPipeline,
                ENetworkChannel.UnreliableUnordered => NetworkPipeline.Null,
                _ => NetworkPipeline.Null
            };
        }

        private ENetworkChannel ParseChannelPipeline(NetworkPipeline pipeline)
        {
            if (pipeline == _reliableSequencedPipeline) return ENetworkChannel.ReliableOrdered;
            if (pipeline == _reliablePipeline) return ENetworkChannel.ReliableUnordered;
            if (pipeline == _unreliableSequencedPipeline) return ENetworkChannel.UnreliableOrdered;
            if (pipeline == NetworkPipeline.Null) return ENetworkChannel.UnreliableUnordered;
            return default;
        }

        private void CleanOutgoingMessages(NetworkConnection conn)
        {
            var sendTargets = new NativeList<SendTarget>(4, Allocator.Persistent);
            foreach (var kvp in _outgoingMessages)
            {
                if (kvp.Key.Connection.Equals(conn))
                {
                    sendTargets.Add(kvp.Key);
                }
            }

            foreach (var sendTarget in sendTargets)
            {
                _outgoingMessages.Remove(sendTarget, out var queue);
                queue.Dispose();
            }

            sendTargets.Dispose();
        }
        
        #endregion
    }
}
