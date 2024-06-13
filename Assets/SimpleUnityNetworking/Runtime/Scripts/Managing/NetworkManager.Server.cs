using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using jKnepel.SimpleUnityNetworking.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager
    {
        #region server logic
        
        public IPEndPoint Server_ServerEndpoint { get; private set; }
        public string Server_Servername { get; private set; }
        public uint Server_MaxNumberOfClients { get; private set; }
        public ELocalServerConnectionState Server_LocalState { get; private set; } = ELocalServerConnectionState.Stopped;
        public ConcurrentDictionary<uint, ClientInformation> Server_ConnectedClients { get; } = new();
        
        public event Action<ELocalServerConnectionState> Server_OnLocalStateUpdated;
        public event Action<uint> Server_OnRemoteClientConnected;
        public event Action<uint> Server_OnRemoteClientDisconnected;
        public event Action<uint> Server_OnRemoteClientUpdated;
        
        private readonly ConcurrentDictionary<uint, byte[]> _authenticatingClients = new();

        private string _cachedServername;
        
        private void HandleTransportServerStateUpdate(ELocalConnectionState state)
        {
            switch (state)
            {
                case ELocalConnectionState.Starting:
                    Logger?.Log("Server is starting...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Started:
                    Server_ServerEndpoint = Transport.ServerEndpoint;
                    Server_Servername = _cachedServername;
                    Server_MaxNumberOfClients = Transport.MaxNumberOfClients;
                    Logger?.Log("Server was started", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopping:
                    Logger?.Log("Server is stopping...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopped:
                    Server_ServerEndpoint = null;
                    Server_MaxNumberOfClients = 0;
                    Logger?.Log("Server was stopped", EMessageSeverity.Log);
                    break;
            }
            Server_LocalState = (ELocalServerConnectionState)state;
            Server_OnLocalStateUpdated?.Invoke(Server_LocalState);
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
            Random rnd = new();
            var challenge = (ulong)(rnd.NextDouble() * ulong.MaxValue);
            var hashedChallenge = SHA256.Create().ComputeHash(BitConverter.GetBytes(challenge));
            _authenticatingClients[clientID] = hashedChallenge;

            // send challenge to client
            Writer writer = new(SerialiserSettings);
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
            Writer writer = new(SerialiserSettings);
            writer.WriteByte(ClientUpdatePacket.PacketType);
            ClientUpdatePacket.Write(writer, new(clientID));
            foreach (var id in Server_ConnectedClients.Keys)
                Transport?.SendDataToClient(id, writer.GetBuffer(), ENetworkChannel.ReliableOrdered);

        }
        
        private void OnServerReceivedData(ServerReceivedData data)
        {
            try
            {
                Reader reader = new(data.Data, SerialiserSettings);
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
                Logger?.Log(e.Message);
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
            Writer writer = new(SerialiserSettings);
            writer.WriteByte(ConnectionAuthenticatedPacket.PacketType);
            ConnectionAuthenticatedPacket authentication = new(clientID, Server_Servername, Server_MaxNumberOfClients);
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
                    clientInfo.Username, clientInfo.UserColour);
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
            Server_ConnectedClients[clientID].UserColour = packet.Colour;
            Server_OnRemoteClientUpdated?.Invoke(clientID);

            // inform other clients of update
            Writer writer = new(SerialiserSettings);
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
                case DataPacket.DataPacketType.ToClient:
                    targetIDs = new[] { (uint)packet.TargetID };
                    break;
                case DataPacket.DataPacketType.ToClients:
                    targetIDs = packet.TargetIDs;
                    break;
                case DataPacket.DataPacketType.ToServer:
                    if (packet.IsStructData)
                        // ReSharper disable once PossibleInvalidOperationException
                        Server_ReceiveStructData(packet.DataID, clientID, packet.Data);
                    else
                        // ReSharper disable once PossibleInvalidOperationException
                        Server_ReceiveByteData(packet.DataID, clientID, packet.Data);
                    break;
            }
            
            // forward data to defined clients
            Writer writer = new(SerialiserSettings);
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
        
        #endregion
        
        #region byte data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, ByteDataCallback>> _registeredServerByteDataCallbacks = new();

        public void Server_RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredServerByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
            {
                callbacks = new();
                _registeredServerByteDataCallbacks.TryAdd(byteDataHash, callbacks);
            }

            var key = callback.GetHashCode();
            var del = CreateByteDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
        }

        public void Server_UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredServerByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        public void Server_SendByteDataToClient(uint clientID, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            Server_SendByteData(new [] { clientID }, byteID, byteData, channel);
        }

        public void Server_SendByteDataToAll(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            Server_SendByteData(Client_ConnectedClients.Keys.ToArray(), byteID, byteData, channel);
        }

        public void Server_SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            Server_SendByteData(clientIDs, byteID, byteData, channel);
        }
        
        private void Server_SendByteData(uint[] clientIDs, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsServer)
            {
                Logger?.Log("The local server must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (Server_ConnectedClients.ContainsKey(id)) continue;
                Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket packet = new(DataPacket.DataPacketType.Forwarded, 0, false,
                Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, packet);
            var data = writer.GetBuffer();
            foreach (var id in clientIDs)
            {
                Transport?.SendDataToClient(id, data, channel);
            }
        }

        private void Server_ReceiveByteData(uint byteID, uint clientID, byte[] data)
        {
            if (!_registeredServerByteDataCallbacks.TryGetValue(byteID, out var callbacks))
                return;

            foreach (var callback in callbacks.Values)
                callback?.Invoke(clientID, data);
        }
        
        #endregion
        
        #region struct data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredServerStructDataCallbacks = new();

        public void Server_RegisterStructData<T>(Action<uint, T> callback) where T : struct
        {
	        var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredServerStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
			{
                callbacks = new();
                _registeredServerStructDataCallbacks.TryAdd(structDataHash, callbacks);
			}

			var key = callback.GetHashCode();
			var del = CreateStructDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del); 
        }

        public void Server_UnregisterStructData<T>(Action<uint, T> callback) where T : struct
		{
			var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredServerStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
		public void Server_SendStructDataToClient<T>(uint clientID, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            Server_SendStructData(new [] { clientID }, structData, channel); 
        }

		public void Server_SendStructDataToAll<T>(T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            Server_SendStructData(Client_ConnectedClients.Keys.ToArray(), structData, channel); 
        }

		public void Server_SendStructDataToClients<T>(uint[] clientIDs, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            Server_SendStructData(clientIDs, structData, channel); 
        }
        
        private void Server_SendStructData<T>(uint[] clientIDs, T structData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            if (!IsServer)
            {
                Logger?.Log("The local server must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (Server_ConnectedClients.ContainsKey(id)) continue;
                Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(SerialiserSettings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket packet = new(DataPacket.DataPacketType.Forwarded, 0, true,
                Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, packet);
            var data = writer.GetBuffer();
            foreach (var id in clientIDs)
            {
                Transport?.SendDataToClient(id, data, channel);
            }
        }
		
		private void Server_ReceiveStructData(uint structHash, uint clientID, byte[] data)
		{
			if (!_registeredServerStructDataCallbacks.TryGetValue(structHash, out var callbacks))
				return;

			foreach (var callback in callbacks.Values)
			{
				callback?.Invoke(clientID, data);
			}
        }
        
        #endregion
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
