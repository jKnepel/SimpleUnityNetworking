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
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager
    {
        #region client logic
        
        public IPEndPoint Client_ServerEndpoint { get; private set; }
        public string Client_Servername { get; private set; }
        public uint Client_MaxNumberOfClients { get; private set; }
        public uint Client_ClientID { get; private set; }
        public string Client_Username { get; private set; }
        public Color32 Client_UserColour { get; private set; }
        public ELocalClientConnectionState Client_LocalState { get; private set; } = ELocalClientConnectionState.Stopped;
        public ConcurrentDictionary<uint, ClientInformation> Client_ConnectedClients { get; } = new();
        
        public event Action<ELocalClientConnectionState> Client_OnLocalStateUpdated;
        public event Action<uint> Client_OnRemoteClientConnected;
        public event Action<uint> Client_OnRemoteClientDisconnected;
        public event Action<uint> Client_OnRemoteClientUpdated;

        private string _cachedUsername;
        private Color32 _cachedUserColour;
        
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
                    Client_ServerEndpoint = null;
                    Client_MaxNumberOfClients = 0;
                    Client_Servername = string.Empty;
                    Client_ClientID = 0;
                    Client_Username = string.Empty;
                    Client_UserColour = Color.white;
                    Logger?.Log("Client was stopped", EMessageSeverity.Log);
                    break;
            }
            Client_LocalState = (ELocalClientConnectionState)state;
            Client_OnLocalStateUpdated?.Invoke(Client_LocalState);
        }
        
        private void OnClientReceivedData(ClientReceivedData data)
        {
            try
            {
                Reader reader = new(data.Data, SerialiserSettings);
                var packetType = (EPacketType)reader.ReadByte();
                // Debug.Log($"Client Packet: {packetType}");

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
                    default:
                        return;
                }
            }
            catch (Exception e)
            {
                Logger?.Log(e.Message);
            }
        }

        private void HandleConnectionChallengePacket(Reader reader)
        {
            if (Client_LocalState != ELocalClientConnectionState.Started)
                return;
            
            var packet = ConnectionChallengePacket.Read(reader);
            var hashedChallenge = SHA256.Create().ComputeHash(BitConverter.GetBytes(packet.Challenge));
            
            Writer writer = new(SerialiserSettings);
            writer.WriteByte(ChallengeAnswerPacket.PacketType);
            ChallengeAnswerPacket.Write(writer, new(hashedChallenge, _cachedUsername, _cachedUserColour));
            Transport?.SendDataToServer(writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
        }

        private void HandleConnectionAuthenticatedPacket(Reader reader)
        {
            if (Client_LocalState != ELocalClientConnectionState.Started)
                return;
            
            var packet = ConnectionAuthenticatedPacket.Read(reader);
            Client_ServerEndpoint = Transport.ServerEndpoint;
            Client_MaxNumberOfClients = packet.MaxNumberConnectedClients;
            Client_Servername = packet.Servername;
            Client_ClientID = packet.ClientID;
            Client_Username = _cachedUsername;
            Client_UserColour = _cachedUserColour;
            Client_LocalState = ELocalClientConnectionState.Authenticated;
            Client_OnLocalStateUpdated?.Invoke(Client_LocalState);
            Logger?.Log("Client was authenticated", EMessageSeverity.Log);
        }

        private void HandleDataPacket(Reader reader)
        {
            if (Client_LocalState != ELocalClientConnectionState.Authenticated)
                return;

            var packet = DataPacket.Read(reader);
            if (packet.DataType != DataPacket.DataPacketType.Forwarded)
                return;
            
            if (packet.IsStructData)
                // ReSharper disable once PossibleInvalidOperationException
                Client_ReceiveStructData(packet.DataID, (uint)packet.SenderID, packet.Data);
            else
                // ReSharper disable once PossibleInvalidOperationException
                Client_ReceiveByteData(packet.DataID, (byte)packet.SenderID, packet.Data);
        }

        private void HandleClientUpdatePacket(Reader reader)
        {
            if (Client_LocalState != ELocalClientConnectionState.Authenticated)
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
                    Client_ConnectedClients[clientID].UserColour = packet.Colour;
                    Client_OnRemoteClientUpdated?.Invoke(clientID);
                    break;
            }
        }
        
        #endregion
        
        #region byte data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, ByteDataCallback>> _registeredClientByteDataCallbacks = new();

        public void Client_RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredClientByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
            {
                callbacks = new();
                _registeredClientByteDataCallbacks.TryAdd(byteDataHash, callbacks);
            }

            var key = callback.GetHashCode();
            var del = CreateByteDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
        }

        public void Client_UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredClientByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        public void Client_SendByteDataToServer(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsClient)
            {
                Logger?.Log("The local client must be started before data can be send!");
                return;
            }

            Writer writer = new(SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(false, Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, dataPacket);
            Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }
        
        public void Client_SendByteDataToClient(uint clientID, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            Client_SendByteData(new [] { clientID }, byteID, byteData, channel);
        }

        public void Client_SendByteDataToAll(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            Client_SendByteData(Client_ConnectedClients.Keys.ToArray(), byteID, byteData, channel);
        }

        public void Client_SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            Client_SendByteData(clientIDs, byteID, byteData, channel);
        }
        
        private void Client_SendByteData(uint[] clientIDs, string byteID, byte[] byteData,
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsClient)
            {
                Logger?.Log("The local client must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (Client_ConnectedClients.ContainsKey(id)) continue;
                Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(clientIDs, false, Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, dataPacket);
            Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }

        private void Client_ReceiveByteData(uint byteID, uint clientID, byte[] data)
        {
            if (!_registeredClientByteDataCallbacks.TryGetValue(byteID, out var callbacks))
                return;

            foreach (var callback in callbacks.Values)
                callback?.Invoke(clientID, data);
        }
        
        #endregion
        
        #region struct data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredClientStructDataCallbacks = new();

        public void Client_RegisterStructData<T>(Action<uint, T> callback) where T : struct
        {
	        var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredClientStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
			{
                callbacks = new();
                _registeredClientStructDataCallbacks.TryAdd(structDataHash, callbacks);
			}

			var key = callback.GetHashCode();
			var del = CreateStructDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del); 
        }

        public void Client_UnregisterStructData<T>(Action<uint, T> callback) where T : struct
		{
			var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredClientStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        public void Client_SendStructDataToServer<T>(T structData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            if (!IsClient)
            {
                Logger?.Log("The local client must be started before data can be send!");
                return;
            }

            Writer writer = new(SerialiserSettings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(true, Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, dataPacket);
            Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }
        
		public void Client_SendStructDataToClient<T>(uint clientID, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            Client_SendStructData(new [] { clientID }, structData, channel); 
        }

		public void Client_SendStructDataToAll<T>(T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            Client_SendStructData(Client_ConnectedClients.Keys.ToArray(), structData, channel); 
        }

		public void Client_SendStructDataToClients<T>(uint[] clientIDs, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            Client_SendStructData(clientIDs, structData, channel); 
        }
        
        private void Client_SendStructData<T>(uint[] clientIDs, T structData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct
        {
            if (!IsClient)
            {
                Logger?.Log("The local client must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (Client_ConnectedClients.ContainsKey(id)) continue;
                Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(SerialiserSettings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(clientIDs, true, Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, dataPacket);
            Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }
		
		private void Client_ReceiveStructData(uint structHash, uint clientID, byte[] data)
		{
			if (!_registeredClientStructDataCallbacks.TryGetValue(structHash, out var callbacks))
				return;

			foreach (var callback in callbacks.Values)
			{
				callback?.Invoke(clientID, data);
			}
        }
        
        #endregion
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
