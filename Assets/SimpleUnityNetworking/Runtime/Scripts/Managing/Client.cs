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
    public class Client
    {
        #region fields

        /// <summary>
        /// Whether the local server has been started or not
        /// </summary>
        public bool IsActive => LocalState == ELocalClientConnectionState.Authenticated;
        
        /// <summary>
        /// Endpoint of the server to which the local client is connected
        /// </summary>
        public IPEndPoint ServerEndpoint { get; private set; }
        /// <summary>
        /// Name of the server to which the local client is connected
        /// </summary>
        public string Servername { get; private set; }
        /// <summary>
        /// Max number of connected clients of the server to which the local client is connected
        /// </summary>
        public uint MaxNumberOfClients { get; private set; }
        
        /// <summary>
        /// Identifier of the local client
        /// </summary>
        public uint ClientID { get; private set; }
        /// <summary>
        /// Username of the local client
        /// </summary>
        /// <remarks>
        /// TODO : synchronise updated set
        /// </remarks>
        public string Username { get; set; } = "Username";
        /// <summary>
        /// UserColour of the local client
        /// </summary>
        /// <remarks>
        /// TODO : synchronise updated set
        /// </remarks>
        public Color32 UserColour { get; set; } = new(153, 191, 97, 255);
        /// <summary>
        /// The current connection state of the local client
        /// </summary>
        public ELocalClientConnectionState LocalState { get; private set; } = ELocalClientConnectionState.Stopped;
        /// <summary>
        /// The remote clients that are connected to the same server
        /// </summary>
        public ConcurrentDictionary<uint, ClientInformation> ConnectedClients { get; } = new();
        /// <summary>
        /// The number of client connected to the same server
        /// </summary>
        public uint NumberOfConnectedClients => (uint)(IsActive ? ConnectedClients.Count + 1 : 0);
        
        /// <summary>
        /// Called when the local client's connection state has been updated
        /// </summary>
        public event Action<ELocalClientConnectionState> OnLocalStateUpdated;
        /// <summary>
        /// Called by the local client when a new remote client has been authenticated
        /// </summary>
        public event Action<uint> OnRemoteClientConnected;
        /// <summary>
        /// Called by the local client when a remote client disconnected
        /// </summary>
        public event Action<uint> OnRemoteClientDisconnected;
        /// <summary>
        /// Called by the local client when a remote client updated its information
        /// </summary>
        public event Action<uint> OnRemoteClientUpdated;

        private readonly NetworkManager _networkManager;
        
        #endregion

        public Client(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _networkManager.OnTransportDisposed += OnTransportDisposed;
            _networkManager.OnClientStateUpdated += OnClientStateUpdated;
            _networkManager.OnClientReceivedData += OnClientReceivedData;
        }

        #region private methods

        private void OnTransportDisposed()
        {
            ConnectedClients.Clear();
            LocalState = ELocalClientConnectionState.Stopped;
        }
        
        private void OnClientStateUpdated(ELocalConnectionState state)
        {
            switch (state)
            {
                case ELocalConnectionState.Starting:
                    _networkManager.Logger?.Log("Client is starting...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Started:
                    _networkManager.Logger?.Log("Client was started", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopping:
                    _networkManager.Logger?.Log("Client is stopping...", EMessageSeverity.Log);
                    break;
                case ELocalConnectionState.Stopped:
                    ServerEndpoint = null;
                    MaxNumberOfClients = 0;
                    Servername = string.Empty;
                    ClientID = 0;
                    _networkManager.Logger?.Log("Client was stopped", EMessageSeverity.Log);
                    break;
            }
            LocalState = (ELocalClientConnectionState)state;
            OnLocalStateUpdated?.Invoke(LocalState);
        }
        
        private void OnClientReceivedData(ClientReceivedData data)
        {
            try
            {
                Reader reader = new(data.Data, _networkManager.SerialiserSettings);
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
                _networkManager.Logger?.Log(e.Message);
            }
        }

        private void HandleConnectionChallengePacket(Reader reader)
        {
            if (LocalState != ELocalClientConnectionState.Started)
                return;
            
            var packet = ConnectionChallengePacket.Read(reader);
            var hashedChallenge = SHA256.Create().ComputeHash(BitConverter.GetBytes(packet.Challenge));
            
            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(ChallengeAnswerPacket.PacketType);
            ChallengeAnswerPacket.Write(writer, new(hashedChallenge, Username, UserColour));
            _networkManager.Transport?.SendDataToServer(writer.GetBuffer(), ENetworkChannel.ReliableOrdered);
        }

        private void HandleConnectionAuthenticatedPacket(Reader reader)
        {
            if (LocalState != ELocalClientConnectionState.Started)
                return;
            
            var packet = ConnectionAuthenticatedPacket.Read(reader);
            ServerEndpoint = _networkManager.Transport.ServerEndpoint;
            MaxNumberOfClients = packet.MaxNumberConnectedClients;
            Servername = packet.Servername;
            ClientID = packet.ClientID;
            LocalState = ELocalClientConnectionState.Authenticated;
            OnLocalStateUpdated?.Invoke(LocalState);
            _networkManager.Logger?.Log("Client was authenticated", EMessageSeverity.Log);
        }

        private void HandleDataPacket(Reader reader)
        {
            if (LocalState != ELocalClientConnectionState.Authenticated)
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
            if (LocalState != ELocalClientConnectionState.Authenticated)
                return;

            var packet = ClientUpdatePacket.Read(reader);
            var clientID = packet.ClientID;
            switch (packet.Type)
            {
                case ClientUpdatePacket.UpdateType.Connected:
                    ConnectedClients[clientID] = new(clientID, packet.Username, packet.Colour);
                    OnRemoteClientConnected?.Invoke(clientID);
                    _networkManager.Logger?.Log($"Client: Remote client {clientID} was connected", EMessageSeverity.Log);
                    break;
                case ClientUpdatePacket.UpdateType.Disconnected:
                    if (ConnectedClients.TryRemove(clientID, out _))
                    {
                        OnRemoteClientDisconnected?.Invoke(clientID);
                        _networkManager.Logger?.Log($"Client: Remote client {clientID} was disconnected", EMessageSeverity.Log);
                    }
                    break;
                case ClientUpdatePacket.UpdateType.Updated:
                    ConnectedClients[clientID].Username = packet.Username;
                    ConnectedClients[clientID].UserColour = packet.Colour;
                    OnRemoteClientUpdated?.Invoke(clientID);
                    break;
            }
        }
        
        #endregion
        
        #region byte data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, ByteDataCallback>> _registeredClientByteDataCallbacks = new();

        /// <summary>
        /// Registers a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public void RegisterByteData(string byteID, Action<uint, byte[]> callback)
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

        /// <summary>
        /// Unregisters a callback for a sent byte array with the defined id
        /// </summary>
        /// <param name="byteID">Id of the data that should invoke the callback</param>
        /// <param name="callback">
        ///     Callback which will be invoked after byte data with the given id has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received byte data</param>
        /// </param>
        public void UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredClientByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        /// <summary>
        /// Sends byte data with a given id from the local client to the server.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToServer(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsActive)
            {
                _networkManager.Logger?.Log("The local client must be started before data can be send!");
                return;
            }

            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(false, Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, dataPacket);
            _networkManager.Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }
        
        /// <summary>
        /// Sends byte data with a given id from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToClient(uint clientID, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            SendByteDataToClients(new [] { clientID }, byteID, byteData, channel);
        }

        /// <summary>
        /// Sends byte data with a given id from the local client to all other remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToAll(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            SendByteDataToClients(ConnectedClients.Keys.ToArray(), byteID, byteData, channel);
        }

        /// <summary>
        /// Sends byte data with a given id from the local client to a list of remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="byteID"></param>
        /// <param name="byteData"></param>
        /// <param name="channel"></param>
        public void SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsActive)
            {
                _networkManager.Logger?.Log("The local client must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (ConnectedClients.ContainsKey(id)) continue;
                _networkManager.Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(_networkManager.SerialiserSettings);
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(clientIDs, false, Hashing.GetFNV1Hash32(byteID), byteData);
            DataPacket.Write(writer, dataPacket);
            _networkManager.Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }

        private void ReceiveByteData(uint byteID, uint clientID, byte[] data)
        {
            if (!_registeredClientByteDataCallbacks.TryGetValue(byteID, out var callbacks))
                return;

            foreach (var callback in callbacks.Values)
                callback?.Invoke(clientID, data);
        }
        
        #endregion
        
        #region struct data
        
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredClientStructDataCallbacks = new();

        /// <summary>
        /// Registers a callback for a sent struct
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public void RegisterStructData<T>(Action<uint, T> callback) where T : struct
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

        /// <summary>
        /// Unregisters a callback for a sent struct
        /// </summary>
        /// <param name="callback">
        ///     Callback which will be invoked after a struct of the same type has been received
        ///     <param name="callback arg1">The ID of the sender. The ID will be 0 if the struct data was sent by the server</param>
        ///     <param name="callback arg2">The received struct data</param>
        /// </param>
        public void UnregisterStructData<T>(Action<uint, T> callback) where T : struct
		{
			var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredClientStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        /// <summary>
        /// Sends a struct from the local client to the server.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
        public void SendStructDataToServer<T>(T structData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            if (!IsActive)
            {
                _networkManager.Logger?.Log("The local client must be started before data can be send!");
                return;
            }

            Writer writer = new(_networkManager.SerialiserSettings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(true, Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, dataPacket);
            _networkManager.Transport?.SendDataToServer(writer.GetBuffer(), channel);
        }
        
        /// <summary>
        /// Sends a struct from the local client to a given remote client.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
		public void SendStructDataToClient<T>(uint clientID, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            SendStructDataToClients(new [] { clientID }, structData, channel); 
        }

        /// <summary>
        /// Sends a struct from the local client to all other remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
		public void SendStructDataToAll<T>(T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            SendStructDataToClients(ConnectedClients.Keys.ToArray(), structData, channel); 
        }

        /// <summary>
        /// Sends a struct from the local client to a list of other remote clients.
        /// Can only be called after the local client has been authenticated
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="structData"></param>
        /// <param name="channel"></param>
		public void SendStructDataToClients<T>(uint[] clientIDs, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct 
        {
            if (!IsActive)
            {
                _networkManager.Logger?.Log("The local client must be started before data can be send!");
                return;
            }
            
            foreach (var id in clientIDs)
            {
                if (ConnectedClients.ContainsKey(id)) continue;
                _networkManager.Logger?.Log("Client IDs contained a non-existing ID. All client IDs must be valid!");
                return;
            }

            Writer writer = new(_networkManager.SerialiserSettings);
            writer.Write(structData);
            var structBuffer = writer.GetBuffer();
            writer.Clear();
            
            writer.WriteByte(DataPacket.PacketType);
            DataPacket dataPacket = new(clientIDs, true, Hashing.GetFNV1Hash32(typeof(T).Name), structBuffer);
            DataPacket.Write(writer, dataPacket);
            _networkManager.Transport?.SendDataToServer(writer.GetBuffer(), channel); 
        }
		
		private void ReceiveStructData(uint structHash, uint clientID, byte[] data)
		{
			if (!_registeredClientStructDataCallbacks.TryGetValue(structHash, out var callbacks))
				return;

			foreach (var callback in callbacks.Values)
			{
				callback?.Invoke(clientID, data);
			}
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
                Reader reader = new(data, _networkManager.SerialiserSettings);
                callback?.Invoke(senderID, reader.Read<T>());
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
