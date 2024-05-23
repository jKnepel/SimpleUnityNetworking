using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager
    {
        public ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients { get; } = new();
        
        public ELocalServerConnectionState Server_LocalState => _localServerConnectionState;
        
        public event Action<ELocalServerConnectionState> Server_OnLocalStateUpdated;
        public event Action<uint> Server_OnRemoteClientConnected;
        public event Action<uint> Server_OnRemoteClientDisconnected;
        public event Action<uint> Server_OnRemoteClientUpdated;
        
        private readonly ConcurrentDictionary<uint, byte[]> _authenticatingClients = new();

        private string _cachedServername;
        private int _cachedMaxNumberClients;
        
        private ELocalServerConnectionState _localServerConnectionState = ELocalServerConnectionState.Stopped;
        
        private void HandleTransportServerStateUpdate(ELocalConnectionState state)
        {
            switch (state)
            {
                case ELocalConnectionState.Starting:
                    Logger?.Log("Server is starting...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Started:
                    ServerInformation = new(_cachedServername, _cachedMaxNumberClients);
                    Logger?.Log("Server was started", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopping:
                    Logger?.Log("Server is stopping...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopped:
                    ServerInformation = null;
                    Logger?.Log("Server was stopped", EMessageSeverity.Log);
                    break;
            }
            _localServerConnectionState = (ELocalServerConnectionState)state;
            Server_OnLocalStateUpdated?.Invoke(_localServerConnectionState);
        }
        
        private void OnRemoteConnectionStateUpdated(uint clientID, ERemoteConnectionState state)
        {
            switch (state)
            {
                case ERemoteConnectionState.Connected:
                    HandleRemoteClientConnected(clientID);
                    break;
                case ERemoteConnectionState.Disconnected:
                    HandleRemoteClientDisconnected(clientID);
                    break;
            }
        }

        private void HandleRemoteClientConnected(uint clientID)
        {
            if (Server_ConnectedClients.ContainsKey(clientID))
            {
                Logger?.Log($"An already existing connection was overwritten. Connection for client {clientID} was dropped!");
                Transport?.DisconnectClient(clientID);
                return;
            }
            
            // create and save challenge
            System.Random rnd = new();
            var challenge = (ulong)(rnd.NextDouble() * ulong.MaxValue);
            var hashedChallenge = SHA256.Create().ComputeHash(BitConverter.GetBytes(challenge));
            _authenticatingClients[clientID] = hashedChallenge;

            // send challenge to client
            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(ConnectionChallengePacket.PacketType);
            ConnectionChallengePacket.Write(writer, new(challenge));
            Transport?.SendDataToClient(clientID, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
        }

        private void HandleRemoteClientDisconnected(uint clientID)
        {
            // ignore authenticating or missing client IDs
            if (_authenticatingClients.TryRemove(clientID, out _)) return;
            if (!Server_ConnectedClients.TryRemove(clientID, out _)) return;
            
            Server_OnRemoteClientDisconnected?.Invoke(clientID);
            Logger?.Log($"Server: Remote client {clientID} was disconnected", EMessageSeverity.Log);

            // inform other clients of disconnected client
            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket.Write(writer, new(clientID));
            foreach (var id in Server_ConnectedClients.Keys)
                Transport?.SendDataToClient(id, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
        }
        
        private void OnServerReceivedData(ServerReceivedData data)
        {
            try
            {
                Reader reader = new(data.Data, SerialiserConfiguration?.Settings);
                var packetType = (EPacketType)reader.ReadByte();
                // Debug.Log($"Server Packet: {packetType} from {data.ClientID}");

                switch (packetType)
                {
                    case EPacketType.ChallengeAnswer:
                        HandleChallengeAnswerPacket(data.ClientID, reader);
                        break;
                    case EPacketType.ClientUpdate:
                        HandleClientUpdatePacket(data.ClientID, reader);
                        break;
                    case EPacketType.Data:
                        HandleDataPacket(data.ClientID, reader, data.Channel);
                        break;
                    default:
                        return;
                }
            }
            catch (Exception e)
            {
                Logger?.Log(e.Message, EMessageSeverity.Error);
            }
        }

        private void HandleChallengeAnswerPacket(uint clientID, Reader reader)
        {
            if (!_authenticatingClients.TryGetValue(clientID, out var challenge))
                return;

            var packet = ChallengeAnswerPacket.Read(reader);
            if (!CompareByteArrays(challenge, packet.ChallengeAnswer))
            {
                Transport?.DisconnectClient(clientID);
                _authenticatingClients.TryRemove(clientID, out _);
                return;
            }
            
            // inform client of authentication
            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(ConnectionAuthenticatedPacket.PacketType);
            ConnectionAuthenticatedPacket authentication = new(clientID, ServerInformation.Servername,
                ServerInformation.MaxNumberConnectedClients);
            ConnectionAuthenticatedPacket.Write(writer, authentication);
            Transport?.SendDataToClient(clientID, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
            writer.Clear();
            
            // inform client of other clients
            writer.WriteByte(ClientUpdatePacket.PacketType);
            var pos = writer.Position;
            foreach (var kvp in Server_ConnectedClients)
            {
                writer.Position = pos;
                var clientInfo = kvp.Value;
                ClientUpdatePacket existingClient = new(clientInfo.ID, ClientUpdatePacket.UpdateType.Connected,
                    clientInfo.Username, clientInfo.Colour);
                ClientUpdatePacket.Write(writer, existingClient);
                Transport?.SendDataToClient(clientID, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
            }
            writer.Clear();
            
            // inform other clients of new client
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket update = new(clientID, ClientUpdatePacket.UpdateType.Connected, packet.Username,
                packet.Colour);
            ClientUpdatePacket.Write(writer, update);
            var data = writer.GetBuffer();
            foreach (var id in Server_ConnectedClients.Keys)
                Transport?.SendDataToClient(id, data, ENetworkChannel.ReliableOrdered);
            writer.Clear();
            
            // authenticate client
            Server_ConnectedClients[clientID] = new(clientID, packet.Username, packet.Colour);
            _authenticatingClients.TryRemove(clientID, out _);
            Server_OnRemoteClientConnected?.Invoke(clientID);
            Logger?.Log($"Server: Remote client {clientID} was connected", EMessageSeverity.Log);
        }

        private void HandleClientUpdatePacket(uint clientID, Reader reader)
        {
            if (!Server_ConnectedClients.ContainsKey(clientID))
                return;

            var packet = ClientUpdatePacket.Read(reader);
            if (packet.ClientID != clientID || packet.Type != ClientUpdatePacket.UpdateType.Updated)
                return;
            
            // apply update
            Server_ConnectedClients[clientID].Username = packet.Username;
            Server_ConnectedClients[clientID].Colour = packet.Colour;
            Server_OnRemoteClientUpdated?.Invoke(clientID);

            // inform other clients of update
            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket.Write(writer, packet);
            var data = writer.GetBuffer();
            foreach (var id in Server_ConnectedClients.Keys)
            {
                if (id == clientID) continue;
                Transport?.SendDataToClient(id, data, ENetworkChannel.ReliableOrdered);
            }
        }

        private void HandleDataPacket(uint clientID, Reader reader, ENetworkChannel channel)
        {
            if (!Server_ConnectedClients.TryGetValue(clientID, out _))
                return;

            var packet = DataPacket.Read(reader);
            uint[] targetIDs = { };
            switch (packet.DataType)
            {
                case DataPacket.DataPacketType.Forwarded:
                    return;
                // ReSharper disable once PossibleInvalidOperationException
                case DataPacket.DataPacketType.Target:
                    targetIDs = new[] { (uint)packet.TargetID };
                    break;
                case DataPacket.DataPacketType.Targets:
                    targetIDs = packet.TargetIDs;
                    break;
            }
            
            // forward data to defined clients
            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket forwardedPacket = new(DataPacket.DataPacketType.Forwarded, clientID, packet.IsStructData,
                packet.DataID, packet.Data);
            DataPacket.Write(writer, forwardedPacket);
            var data = writer.GetBuffer();
            foreach (var id in targetIDs)
            {
                if (id == clientID) continue;
                Transport?.SendDataToClient(id, data, channel);
            }
        }
        
        private static bool CompareByteArrays(IEnumerable<byte> a, IEnumerable<byte> b)
        {
            return a.SequenceEqual(b);
        }
    }
    
    public enum ELocalServerConnectionState
    {
        /// <summary>
        /// Signifies the start of a local connection
        /// </summary>
        Starting = 0,
        /// <summary>
        /// Signifies that a local connection has been successfully established
        /// </summary>
        Started = 1,
        /// <summary>
        /// Signifies that an established local connection is being closed
        /// </summary>
        Stopping = 2,
        /// <summary>
        /// Signifies that an established local connection was closed
        /// </summary>
        Stopped = 3
    }
}
