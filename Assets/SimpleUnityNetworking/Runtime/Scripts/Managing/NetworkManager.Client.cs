using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager
    {
        public ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients { get; } = new();
        
        public ELocalClientConnectionState Client_LocalState => _localClientConnectionState;
        
        public event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
        public event Action<uint> Client_OnRemoteClientConnected;
        public event Action<uint> Client_OnRemoteClientDisconnected;
        public event Action<uint> Client_OnRemoteClientUpdated;

        private string _cachedUsername;
        private Color32 _cachedColour;
        
        private ELocalClientConnectionState _localClientConnectionState = ELocalClientConnectionState.Stopped;
        
        private void HandleTransportClientStateUpdate(ELocalConnectionState state)
        {
            switch (state)
            {
                case ELocalConnectionState.Starting:
                    Logger?.Log("Client is starting...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Started:
                    Logger?.Log("Client was started", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopping:
                    Logger?.Log("Client is stopping...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopped:
                    ClientInformation = null;
                    if (!IsServer) ServerInformation = null;
                    Logger?.Log("Client was stopped", EMessageSeverity.Log);
                    break;
            }
            _localClientConnectionState = (ELocalClientConnectionState)state;
            Client_OnLocalStateUpdated?.Invoke(_localClientConnectionState);
        }
        
        private void OnClientReceivedData(ClientReceivedData data)
        {
            try
            {
                Reader reader = new(data.Data, SerialiserConfiguration?.Settings);
                var packetType = (EPacketType)reader.ReadByte();
                Debug.Log($"Client Packet: {packetType}");

                switch (packetType)
                {
                    case EPacketType.ConnectionChallenge:
                        HandleConnectionChallengePacket(reader);
                        break;
                    case EPacketType.ConnectionAuthenticated:
                        HandleConnectionAuthenticatedPacket(reader);
                        break;
                    case EPacketType.Data:
                        HandleDataPacket(reader);
                        break;
                    case EPacketType.ClientUpdate:
                        HandleClientUpdatePacket(reader);
                        break;
                }
            }
            catch (Exception e)
            {
                Logger?.Log(e.Message, EMessageSeverity.Error);
            }
        }

        private void HandleConnectionChallengePacket(Reader reader)
        {
            if (_localClientConnectionState != ELocalClientConnectionState.Started)
                return;
            
            var packet = ConnectionChallengePacket.Read(reader);
            var hashedChallenge = SHA256.Create().ComputeHash(BitConverter.GetBytes(packet.Challenge));
            
            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(ChallengeAnswerPacket.PacketType);
            ChallengeAnswerPacket.Write(writer, new(hashedChallenge, _cachedUsername, _cachedColour));
            Transport?.SendDataToServer(writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
        }

        private void HandleConnectionAuthenticatedPacket(Reader reader)
        {
            if (_localClientConnectionState != ELocalClientConnectionState.Started)
                return;
            
            var packet = ConnectionAuthenticatedPacket.Read(reader);
            ClientInformation = new(packet.ClientID, _cachedUsername, _cachedColour);
            if (!IsServer)
                ServerInformation = new(packet.Servername, packet.MaxNumberConnectedClients);
            _localClientConnectionState = ELocalClientConnectionState.Authenticated;
            Client_OnLocalStateUpdated?.Invoke(_localClientConnectionState);
            Logger?.Log("Client was authenticated", EMessageSeverity.Log);
        }

        private void HandleDataPacket(Reader reader)
        {
            if (_localClientConnectionState != ELocalClientConnectionState.Authenticated)
                return;

            var packet = DataPacket.Read(reader);
            if (packet.DataType != DataPacket.DataPacketType.Forwarded)
                return;
            
            if (packet.IsStructData)
                // ReSharper disable once PossibleInvalidOperationException
                ReceiveStructData(packet.DataID, (uint)packet.SenderID, packet.Data);
            else
                // ReSharper disable once PossibleInvalidOperationException
                ReceiveByteData(packet.DataID, (byte)packet.SenderID, packet.Data);
        }

        private void HandleClientUpdatePacket(Reader reader)
        {
            if (_localClientConnectionState != ELocalClientConnectionState.Authenticated)
                return;

            var packet = ClientUpdatePacket.Read(reader);
            var clientID = packet.ClientID;
            switch (packet.Type)
            {
                case ClientUpdatePacket.UpdateType.Connected:
                    Client_ConnectedClients[clientID] = new(clientID, packet.Username, packet.Colour);
                    Client_OnRemoteClientConnected?.Invoke(clientID);
                    Logger?.Log($"Client: Remote client {clientID} was connected", EMessageSeverity.Log);
                    break;
                case ClientUpdatePacket.UpdateType.Disconnected:
                    if (Client_ConnectedClients.TryRemove(clientID, out _))
                    {
                        Client_OnRemoteClientDisconnected?.Invoke(clientID);
                        Logger?.Log($"Client: Remote client {clientID} was disconnected", EMessageSeverity.Log);
                    }
                    break;
                case ClientUpdatePacket.UpdateType.Updated:
                    Client_ConnectedClients[clientID].Username = packet.Username;
                    Client_ConnectedClients[clientID].Colour = packet.Colour;
                    Client_OnRemoteClientUpdated?.Invoke(clientID);
                    break;
            }
        }

        private void SendByteData(uint[] clientIDs, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            foreach (var id in clientIDs)
            {
                if (Client_ConnectedClients.ContainsKey(id)) continue;
                Logger?.Log("The byte data was send to a non-existing client. All client IDs must be valid!");
                return;
            }

            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(clientIDs, false, Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, dataPacket);
            Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }

        private void SendStructData<T>(uint[] clientIDs, T structData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
        {
            foreach (var id in clientIDs)
            {
                if (Client_ConnectedClients.ContainsKey(id)) continue;
                Logger?.Log("The struct data was send to a non-existing client. All client IDs must be valid!");
                return;
            }

            Writer writer = new(SerialiserConfiguration?.Settings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(clientIDs, true, Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, dataPacket);
            Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }
    }

    public enum ELocalClientConnectionState
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
        Stopped = 3,
        /// <summary>
        /// Signifies that an established local connection has been authenticated and is ready to send data
        /// </summary>
        Authenticated = 4,
    }
}
