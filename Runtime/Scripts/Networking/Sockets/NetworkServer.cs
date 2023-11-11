using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.Utilities;

namespace jKnepel.SimpleUnityNetworking.Networking.Sockets
{
    public sealed class NetworkServer : ANetworkSocket
    {
        #region public properties

        /// <summary>
        /// Action for when a remote Client connected to the current Server and can now receive Messages.
        /// </summary>
        public Action<byte> OnClientConnected;

        /// <summary>
        /// Action for when a remote Client disconnected from the current Server and can no longer receive any Messages.
        /// </summary>
        public Action<byte> OnClientDisconnected;

        /// <summary>
        /// Action for when a Client was added or removed from ConnectedClients.
        /// </summary>
        public Action OnConnectedClientListUpdated;

        /// <summary>
		/// Action for when the server was closed.
		/// </summary>
        public Action OnServerWasClosed;

        public override ConcurrentDictionary<byte, ClientInformation> ConnectedClients => new(_connectedClients.ToDictionary(k => k.Value.ID, v => (ClientInformation)v.Value));

        #endregion

        #region private fields

        private NetworkConfiguration _config;
        private IPEndPoint _localEndpoint;
        private IPEndPoint _discoveryEndpoint;

        private Thread _heartbeatThread;

        private readonly ConcurrentDictionary<IPEndPoint, byte[]> _pendingConnections = new();

        private readonly ConcurrentDictionary<byte, IPEndPoint> _idIpTable = new();
        private readonly ConcurrentDictionary<IPEndPoint, ClientInformationSocket> _connectedClients = new();

        private readonly ConcurrentQueue<SequencedPacketContainer> _packetsToSend = new();

        #endregion

        #region lifecycle

        public NetworkServer() { }

        public void StartServer(NetworkConfiguration config, string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null)
        {
            ConnectionStatus = EConnectionStatus.IsConnecting;

            if (string.IsNullOrEmpty(servername))
            {
                Messaging.DebugMessage("The Servername can't be empty or null!");
                return;
            }

            if (servername.Length > 100)
            {
                Messaging.DebugMessage("The Servername can't be longer than 100 Characters!");
            }

            if (Encoding.UTF8.GetByteCount(servername) != servername.Length)
            {
                Messaging.DebugMessage("The Servername must be in ASCII Encoding!");
                return;
            }

            if (maxNumberClients <= 1 || maxNumberClients >= byte.MaxValue)
            {
                Messaging.DebugMessage($"The Max Client Number can't be bigger than {byte.MaxValue - 1} or smaller than 2!");
            }

            config.LocalPort = NetworkUtilities.FindNextAvailablePort();

            try { _localEndpoint = new(config.LocalIPAddresses[config.LocalIPAddressIndex], config.LocalPort); }
            catch (Exception ex)
			{
                switch (ex)
				{
                    case ArgumentNullException:
                        Messaging.DebugMessage("The Local IP Address is null!");
                        break;
                    case FormatException:
                        Messaging.DebugMessage("The Local IP Address is not valid!");
                        break;
                    case ArgumentOutOfRangeException:
                        Messaging.DebugMessage("The Local Port is outside the expected range!");
                        break;
                }
                ConnectionStatus = EConnectionStatus.IsDisconnected;
                onConnectionEstablished?.Invoke(false);
                Dispose();
                return;
            }

            try { _discoveryEndpoint = new(IPAddress.Parse(config.DiscoveryIP), config.DiscoveryPort); }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case ArgumentNullException:
                        Messaging.DebugMessage("The Discovery IP Address is null!");
                        break;
                    case FormatException:
                        Messaging.DebugMessage("The Discovery IP Address is not valid!");
                        break;
                    case ArgumentOutOfRangeException:
                        Messaging.DebugMessage("The Discovery Port is outside the expected range!");
                        break;
                }
                ConnectionStatus = EConnectionStatus.IsDisconnected;
                onConnectionEstablished?.Invoke(false);
                Dispose();
                return;
            }

            try
            {
                _udpClient = new();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(_localEndpoint);

                _config = config;
                ServerInformation = new(_localEndpoint, servername, maxNumberClients);
                ClientInformation = new(1, config.Username, config.Color);

                _listenerThread = new(() => ListenerThread()) { IsBackground = true };
                _listenerThread.Start();
                _heartbeatThread = new(() => HeartbeatThread()) { IsBackground = true };
                _heartbeatThread.Start();
                _senderThread = new(() => SenderThread()) { IsBackground = true };
                _senderThread.Start();
            }
            catch (Exception ex)
			{
                switch (ex)
				{
                    case ObjectDisposedException:
                    case SocketException:
                        Messaging.DebugMessage("An error ocurred when attempting to access the socket!");
                        break;
                    case ThreadStartException:
                        Messaging.DebugMessage("An error ocurred when starting the threads. Please try again later!");
                        break;
                    case OutOfMemoryException:
                        Messaging.DebugMessage("Not enough memory available to start the threads!");
                        break;
                    default:
                        Dispose();
                        ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                }
                onConnectionEstablished?.Invoke(false);
                Dispose();
                return;
            }

            Messaging.SystemMessage("Server has been opened!");
            ConnectionStatus = EConnectionStatus.IsConnected;
            onConnectionEstablished?.Invoke(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Increment(ref _disposeCount) == 1)
            {
                if (_listenerThread != null)
                {
                    _listenerThread.Abort();
                    _listenerThread.Join();
                }
                if (_heartbeatThread != null)
                {
                    _heartbeatThread.Abort();
                    _heartbeatThread.Join();
                }
                if (_senderThread != null)
                {
                    _senderThread.Abort();
                    _senderThread.Join();
                }

                if (_udpClient != null)
                {
                    _udpClient.Close();
                    _udpClient.Dispose();
                }

                if (_config != null)
                {
                    _config.LocalPort = 0;
                }

                ConnectionStatus = EConnectionStatus.IsDisconnected;
                ServerInformation = null;
                ClientInformation = null;
            }
        }

        #endregion

        #region public methods

        public override void DisconnectFromServer()
        {
            if (!IsConnected)
                return;

            List<IPEndPoint> connectedClients = new();
            foreach (ClientInformationSocket client in _connectedClients.Values)
                connectedClients.Add(client.Endpoint);
            Writer writer = new();
            writer.Write(new ConnectionClosedPacket(ClosedReason.ServerWasClosed));
            SendConnectionPacket(connectedClients, EPacketType.ConnectionClosed, writer.GetBuffer());

            Messaging.SystemMessage("Closed the server!");
            OnServerWasClosed?.Invoke();
            Dispose();
        }

		public override void SendStructData<T>(byte receiverID, T StructData, ENetworkChannel networkChannel, Action<bool> onDataSend = null)
        {
            if (receiverID == ClientInformation.ID)
			{
                Messaging.DebugMessage("The Receiver ID is the same as the local Client's ID!");
                onDataSend?.Invoke(false);
                return;
            }

            if (!GetClientById(receiverID, out ClientInformationSocket client) && receiverID != 0)
			{
                Messaging.DebugMessage("The given Receiver ID is not valid!");
                onDataSend?.Invoke(false);
                return;
			}

            Writer writer = new();
            writer.Write(StructData);
            DataPacket DataPacket = new(true, Hashing.GetFNV1Hash32(typeof(T).Name), ClientInformation.ID, writer.GetBuffer());
            writer.Clear();
            writer.Write(DataPacket);
            _packetsToSend.Enqueue(new(receiverID, networkChannel, EPacketType.Data, writer.GetBuffer(), onDataSend));
        }

        public override void SendByteData(byte receiverID, string id, byte[] data, ENetworkChannel networkChannel, Action<bool> onDataSend = null)
        {
            if (receiverID == ClientInformation.ID)
            {
                Messaging.DebugMessage("The Receiver ID is the same as the local Client's ID!");
                onDataSend?.Invoke(false);
                return;
            }

            if (!GetClientById(receiverID, out ClientInformationSocket client) && receiverID != 0)
            {
                Messaging.DebugMessage("The given Receiver ID is not valid!");
                onDataSend?.Invoke(false);
                return;
            }

            DataPacket DataPacket = new(false, Hashing.GetFNV1Hash32(id), ClientInformation.ID, data);
            Writer writer = new();
            writer.Write(DataPacket);
            _packetsToSend.Enqueue(new(receiverID, networkChannel, EPacketType.Data, writer.GetBuffer(), onDataSend));
        }

        #endregion

        #region listener logic

        private void ListenerThread()
        {
            while (_disposeCount == 0)
            {
                try
                {   // get packet ip headers
                    IPEndPoint receiveEndpoint = new(1, 1);
                    byte[] receivedBytes = _udpClient.Receive(ref receiveEndpoint);
                    if (receiveEndpoint.Equals(_localEndpoint))
                        continue;

                    Reader reader = new(receivedBytes);

                    // check crc32
                    uint crc32 = reader.ReadUInt32();
                    int typePosition = reader.Position;
                    byte[] bytesToHash = new byte[reader.Length];
                    Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                    reader.BlockCopy(ref bytesToHash, 4, reader.Remaining);
                    if (crc32 != Hashing.GetCRC32Hash(bytesToHash))
                        continue;

                    // check packet type
                    reader.Position = typePosition;
                    PacketHeader header = reader.Read<PacketHeader>();

                    // TODO : send connectionclosed when forcefully disconnected client sends packet
                    // TODO : otherwise refresh sequence numbers

                    // handle individual packet types
                    switch (header.PacketType)
                    {
                        case EPacketType.ConnectionRequest:
							{
                                ConnectionRequestPacket packet = reader.Read<ConnectionRequestPacket>();
                                HandleConnectionRequestPacket(receiveEndpoint, packet);
                                break;
							}
                        case EPacketType.ChallengeAnswer:
							{
                                ChallengeAnswerPacket packet = reader.Read<ChallengeAnswerPacket>();
                                HandleChallengeAnswerPacket(receiveEndpoint, packet);
                                break;
							}
                        case EPacketType.ConnectionClosed:
							{
                                ConnectionClosedPacket packet = reader.Read<ConnectionClosedPacket>();
                                HandleConnectionClosedPacket(receiveEndpoint, packet);
                                break;
							}
                        case EPacketType.ACK:
                            {
                                if (!_connectedClients.TryGetValue(receiveEndpoint, out ClientInformationSocket client))
                                    break;
                                client.LastHeartbeat = DateTime.Now;
                                ACKPacket packet = reader.Read<ACKPacket>();
                                client.SendPacketsBuffer.TryRemove(packet.Sequence, out _);
                                break;
                            }
                        case EPacketType.ChunkedACK:
                            {
                                if (!_connectedClients.TryGetValue(receiveEndpoint, out ClientInformationSocket client))
                                    break;
                                client.LastHeartbeat = DateTime.Now;
                                ChunkedACKPacket packet = reader.Read<ChunkedACKPacket>();
                                client.SendChunksBuffer.TryRemove((packet.Sequence, packet.SliceNumber), out _);
                                break;
                            }
                        case EPacketType.Data:
                        case EPacketType.ClientInfo:
                            {
                                if (!_connectedClients.TryGetValue(receiveEndpoint, out ClientInformationSocket client))
                                    break;
                                client.LastHeartbeat = DateTime.Now;

                                if (header.IsChunkedPacket)
                                    HandleChunkedSequencedPacket(client, header, reader);
                                else
                                    HandleSequencedPacket(client, header, reader);
                                break;
                            }
                        default: break;
                    }
                }
                catch (Exception ex)
                {
                    switch (ex)
                    {
                        case IndexOutOfRangeException:
                        case ArgumentException:
                            continue;
                        case ThreadAbortException:
                            return;
                        case SocketException:
                        case ObjectDisposedException:
                            Messaging.DebugMessage(ex.ToString());
                            MainThreadQueue.Enqueue(() => Dispose());
                            return;
                        default:
                            MainThreadQueue.Enqueue(() => Dispose());
                            ExceptionDispatchInfo.Capture(ex).Throw();
                            throw;
                    }
                }
            }
        }

        private void HandleConnectionRequestPacket(IPEndPoint sender, ConnectionRequestPacket packet)
        {
            Writer writer = new();

            if (_connectedClients.TryGetValue(sender, out ClientInformationSocket client))
            {   // resend client id in case of client not receiving theirs
                writer.Write(new ConnectionAcceptedPacket(client.ID, ServerInformation.Servername, ServerInformation.MaxNumberConnectedClients));
                SendConnectionPacket(sender, EPacketType.ConnectionAccepted, writer.GetBuffer());
                return;
            }

            if (_connectedClients.Count + 1 >= ServerInformation.MaxNumberConnectedClients)
            {   // send connection denied packet if no space available
                writer.Write(new ConnectionDeniedPacket(DeniedReason.NoSpace));
                SendConnectionPacket(sender, EPacketType.ConnectionDenied, writer.GetBuffer());
                return;
            }

            // create challenge packet and send it to requesting client
            System.Random rnd = new();
            ulong challenge = (ulong)(rnd.NextDouble() * ulong.MaxValue);
            writer.Write(new ConnectionChallengePacket(challenge));
            SendConnectionPacket(sender, EPacketType.ConnectionChallenge, writer.GetBuffer());

            // save sha256 hash of challenge nonce for comparing answer from client
            writer.Clear();
            writer.WriteUInt64(challenge);
            byte[] hashedChallenge = SHA256.Create().ComputeHash(writer.GetBuffer());
            _pendingConnections.AddOrUpdate(sender, hashedChallenge, (key, oldValue) => oldValue = hashedChallenge);
        }

        private void HandleChallengeAnswerPacket(IPEndPoint sender, ChallengeAnswerPacket packet)
        {
            if (!_pendingConnections.TryGetValue(sender, out byte[] value))
                return;

            if (!CompareByteArrays(value, packet.ChallengeAnswer))
            {   // send connection denied packet if challenge answer is incorrect
                Writer writer = new();
                writer.Write(new ConnectionDeniedPacket(DeniedReason.InvalidChallengeAnswer));
                SendConnectionPacket(sender, EPacketType.ConnectionDenied, writer.GetBuffer());
                return;
            }

            if (_connectedClients.Count + 1 >= ServerInformation.MaxNumberConnectedClients)
            {   // send connection denied packet if no space available
                Writer writer = new();
                writer.Write(new ConnectionDeniedPacket(DeniedReason.NoSpace));
                SendConnectionPacket(sender, EPacketType.ConnectionDenied, writer.GetBuffer());
                return;
            }

            AddClient(sender, packet.Username, packet.Color);
        }

        private void HandleConnectionClosedPacket(IPEndPoint sender, ConnectionClosedPacket packet)
        {
            if (!_connectedClients.TryGetValue(sender, out ClientInformationSocket client))
                return;

            _connectedClients.TryRemove(sender, out _);
            _idIpTable.TryRemove(client.ID, out _);

            // TODO : handle different closed types

            // send disconnection notification to remaining clients
            List<IPEndPoint> remainingClients = new();
            foreach (ClientInformationSocket remainingClient in _connectedClients.Values)
                remainingClients.Add(remainingClient.Endpoint);
            Writer writer = new();
            writer.Write(new ClientDisconnectedPacket(client.ID));
            SendConnectionPacket(remainingClients, EPacketType.ClientDisconnected, writer.GetBuffer());

            MainThreadQueue.Enqueue(() => OnClientDisconnected?.Invoke(client.ID));
            MainThreadQueue.Enqueue(() => OnConnectedClientListUpdated?.Invoke());
            Messaging.SystemMessage($"Client {client} disconnected!");
        }

        private void HandleSequencedPacket(ClientInformationSocket sender, PacketHeader header, Reader reader)
        {
            ushort sequence = reader.ReadUInt16();

            // unreliable packet sequence
            if (!IsReliableChannel(header.NetworkChannel))
            {   // ignore old packets unless they are unordered
                if (!IsNewPacket(sequence, sender.UnreliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                    return;

                // update sequence and consume packet
                sender.UnreliableRemoteSequence = sequence;
                ConsumeSequencedPacket(sender, header, reader.ReadRemainingBytes());
                return;
            }

            // reliable packet sequence
            // send ACK for reliable sequence
            Writer writer = new();
            writer.Write(new ACKPacket(sequence));
            SendConnectionPacket(sender.Endpoint, EPacketType.ACK, writer.GetBuffer());

            // ignore old packets unless they are unordered
            if (!IsNewPacket(sequence, sender.ReliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                return;

            if (!IsNextPacket(sequence, sender.ReliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
            {   // if a packet is missing in the sequence keep it in the buffer
                sender.ReceivedPacketsBuffer.TryAdd(sequence, (header, reader.ReadRemainingBytes()));
                return;
            }

            // update sequence and consume packet
            sender.ReliableRemoteSequence = sequence;
            ConsumeSequencedPacket(sender, header, reader.ReadRemainingBytes());

            // apply all packets from that sender's buffer that are now next in the sequence
            while (sender.ReceivedPacketsBuffer.Count > 0)
            {
                sequence++;
                if (!sender.ReceivedPacketsBuffer.TryRemove(sequence, out (PacketHeader, byte[]) bufferedPacket))
                    break;

                // update sequence and consume packet
                sender.ReliableRemoteSequence = sequence;
                ConsumeSequencedPacket(sender, bufferedPacket.Item1, bufferedPacket.Item2);
            }
        }

        private void HandleChunkedSequencedPacket(ClientInformationSocket sender, PacketHeader header, Reader reader)
        {
            if (!IsReliableChannel(header.NetworkChannel))
            {
                Messaging.DebugMessage("An unreliable chunked packet has been received. Make sure the client is legit!");
                return;
            }

            ushort sequence = reader.ReadUInt16();
            ushort numberOfSlices = reader.ReadUInt16();
            ushort sliceNumber = reader.ReadUInt16();
            byte[] sliceData = reader.ReadRemainingBytes();

            // send ACK
            Writer writer = new();
            writer.Write(new ChunkedACKPacket(sequence, sliceNumber));
            SendConnectionPacket(sender.Endpoint, EPacketType.ChunkedACK, writer.GetBuffer());

            // ignore old packets unless they are unordered
            if (!IsNewPacket(sequence, sender.ReliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                return;

            if (!sender.ReceivedChunksBuffer.TryGetValue(sequence, out ConcurrentDictionary<ushort, byte[]> bufferedChunk))
            {   // create chunked packet if it doesn't exist yet
                bufferedChunk = new();
                sender.ReceivedChunksBuffer.TryAdd(sequence, bufferedChunk);
            }

            // add slice to chunk and return if chunk is not complete
            bufferedChunk.AddOrUpdate(sliceNumber, sliceData, (key, oldValue) => oldValue = sliceData);
            if (bufferedChunk.Count != numberOfSlices)
                return;

            // concatenate slices to complete packet and remove it from chunk buffer
            List<byte> dataBytes = new();
            for (ushort i = 0; i < numberOfSlices; i++)
            {
                if (!bufferedChunk.TryGetValue(i, out byte[] currentSliceData))
                    return;
                dataBytes.AddRange(currentSliceData);
            }
            byte[] chunkData = dataBytes.ToArray();
            sender.ReceivedChunksBuffer.TryRemove(sequence, out _);

            if (!IsNextPacket(sequence, sender.ReliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
            {   // if a packet is missing in the sequence keep it in the buffer
                sender.ReceivedPacketsBuffer.TryAdd(sequence, (header, chunkData));
                return;
            }

            // update sequence and consume packet
            sender.ReliableRemoteSequence = sequence;
            ConsumeSequencedPacket(sender, header, chunkData);

			// apply all packets from that sender's buffer that are now next in the sequence
			while (sender.ReceivedPacketsBuffer.Count > 0)
			{
				sequence++;
				if (!sender.ReceivedPacketsBuffer.TryRemove(sequence, out (PacketHeader, byte[]) bufferedPacket))
					break;

				// update sequence and consume packet
				sender.ReliableRemoteSequence = sequence;
				ConsumeSequencedPacket(sender, bufferedPacket.Item1, bufferedPacket.Item2);
			}
		}

        private void ConsumeSequencedPacket(ClientInformationSocket sender, PacketHeader packetHeader, byte[] data)
        {
            switch (packetHeader.PacketType)
            {
                case EPacketType.ClientInfo:
					{   // update clients info
                        Reader reader = new(data);
                        ClientInfoPacket packet = reader.Read<ClientInfoPacket>();
                        sender.Username = packet.Username;
                        sender.Color = packet.Color;

                        // send updated client info to all clients
                        ClientInfoPacket info = new(sender.ID, sender.Username, sender.Color);
                        Writer writer = new();
                        writer.Write(info);
                        _packetsToSend.Enqueue(new(0, packetHeader.NetworkChannel, EPacketType.ClientInfo, writer.GetBuffer(), sender.ID));

                        MainThreadQueue.Enqueue(() => OnConnectedClientListUpdated?.Invoke());
                        break;
					}
                case EPacketType.Data:
					{   // accept or forward clients simple data
                        Reader reader = new(data);
                        DataPacket packet = reader.Read<DataPacket>();
                        if (packet.ClientID > 1)
                        {
                            if (GetClientById(packet.ClientID, out ClientInformationSocket targetClient))
                            {   // forward packet to specified client
                                DataPacket forwardedPacket = new(packet.IsStructData, packet.DataID, sender.ID, packet.Data);
                                Writer writer = new();
                                writer.Write(forwardedPacket);
                                SequencedPacketContainer forwardedPacketContainer = new(packet.ClientID, packetHeader.NetworkChannel, packetHeader.PacketType, writer.GetBuffer());
                                _packetsToSend.Enqueue(forwardedPacketContainer);
							}
                            else
							{   // notify sender that specified client is disconnected
                                Writer writer = new();
                                writer.Write(new ClientDisconnectedPacket(packet.ClientID));
                                SendConnectionPacket(sender.Endpoint, EPacketType.ClientDisconnected, writer.GetBuffer());
							}
                            return;
                        }

                        // forward packet to all other clients before consuming
                        if (packet.ClientID == 0)
                        {
                            DataPacket forwardedPacket = new(packet.IsStructData, packet.DataID, sender.ID, packet.Data);
                            Writer writer = new();
                            writer.Write(forwardedPacket);
                            _packetsToSend.Enqueue(new(0, packetHeader.NetworkChannel, EPacketType.Data, writer.GetBuffer(), sender.ID));
                        }

                        // receive simple data data, consuming the packet
                        if (packet.IsStructData)
                            MainThreadQueue.Enqueue(() => ReceiveStructData(packet.DataID, sender.ID, packet.Data));
                        else
                            MainThreadQueue.Enqueue(() => ReceiveByteData(packet.DataID, sender.ID, packet.Data));
                        break;
					}
                default: break;
            }
        }

        #endregion

        #region sender logic

        private void HeartbeatThread()
        {
            try
            {
                UdpClient heartbeatClient = new();
                heartbeatClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                heartbeatClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                heartbeatClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                heartbeatClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_discoveryEndpoint.Address, _localEndpoint.Address));
                heartbeatClient.Client.Bind(_localEndpoint);
                heartbeatClient.Connect(_discoveryEndpoint);

                byte[] heartbeatBytes;
                void CreateHeartbeatBytes()
				{
                    ServerInformationPacket heartbeat = new(ServerInformation.Servername, ServerInformation.MaxNumberConnectedClients, (byte)(_connectedClients.Count + 1));
                    Writer writer = new();
                    writer.Skip(EPrimitiveLength.Int);
                    writer.Write<PacketHeader>(new(EPacketType.ServerInformation));
                    writer.Write(heartbeat);

                    // set crc32
                    byte[] bytesToHash = new byte[writer.Length];
                    Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                    Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, bytesToHash.Length - 4);
                    writer.Position = 0;
                    writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                    heartbeatBytes = writer.GetBuffer();
                }
                CreateHeartbeatBytes();
                OnConnectedClientListUpdated += CreateHeartbeatBytes;

                while (_disposeCount == 0)
                {   // send heartbeat used for discovery until server is closed
                    heartbeatClient.Send(heartbeatBytes, heartbeatBytes.Length);
                    Thread.Sleep(_config.ServerHeartbeatDelay);
                }
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case ThreadAbortException:
                        return;
                    default:
                        MainThreadQueue.Enqueue(() => Dispose());
                        ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                }
            }
        }

        private void SenderThread()
        {
            while (_disposeCount == 0)
            {
                try
                {
                    if (_packetsToSend.Count == 0 || !_packetsToSend.TryDequeue(out SequencedPacketContainer packet))
                        continue;

                    if (IsReliableChannel(packet.NetworkChannel))
                        SendReliablePacket(packet);
                    else
                        SendUnreliablePacket(packet);
                }
                catch (Exception ex)
                {
                    MainThreadQueue.Enqueue(() => Dispose());
                    ExceptionDispatchInfo.Capture(ex).Throw();
                    throw;
                }
            }
        }

        private void SendUnreliablePacket(SequencedPacketContainer packet)
		{
            try
			{
                // write header, sequence and body to buffer 
                Writer writer = new();
                writer.Skip(EPrimitiveLength.Int); // skip crc32 
                writer.Write(new PacketHeader(false, false, packet.NetworkChannel, packet.PacketType));
                int sequencePos = writer.Position;
                writer.Skip(EPrimitiveLength.Short); // skip sequence
                writer.BlockCopy(ref packet.Body, 0, packet.Body.Length);

                if (writer.Length > _config.MTU)
                {   // only allow unreliable packets smaller than the mtu
                    Messaging.DebugMessage($"No unreliable packet can be larger than the MTU of {_config.MTU} bytes!");
                    MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                    return;
                }

                List<ClientInformationSocket> targetClients = new();
                if (packet.ReceiverID > 1)
                {   // only target specific client
                    if (!GetClientById(packet.ReceiverID, out ClientInformationSocket client))
                    {   // client doesnt exist
                        Messaging.DebugMessage($"The client {packet.ReceiverID} is no longer connected to the server!");
                        MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                        return;
                    }

                    targetClients.Add(client);
                }
                else if (packet.ReceiverID == 0)
                {   // target all clients
					targetClients = (List<ClientInformationSocket>)_connectedClients.Values;
                }

                foreach (ClientInformationSocket client in _connectedClients.Values)
                {
                    if (client.ID == packet.ExemptIDs)
                        continue;

                    // write client's sequence number
                    writer.Position = sequencePos;
                    writer.WriteUInt16((ushort)(client.UnreliableLocalSequence + 1));

					// and crc32 to the buffer
					writer.Position = 0;
                    byte[] bytesToHash = new byte[writer.Length];
                    Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                    Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, writer.Length - 4);
                    writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                    _udpClient.Send(writer.GetBuffer(), writer.Length, client.Endpoint);
                    client.UnreliableLocalSequence++;
                }

                MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(true));
            }
            catch (Exception ex)
			{
                MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                switch (ex)
				{
                    case IndexOutOfRangeException:
                    case ArgumentException:
                        Messaging.DebugMessage("Something went wrong serialising a reliable packet!");
                        return;
                    case ThreadAbortException:
                        return;
                    case SocketException:
                    case ObjectDisposedException:
                        Messaging.DebugMessage(ex.ToString());
                        MainThreadQueue.Enqueue(() => Dispose());
                        return;
                }
			}
        }

        private void SendReliablePacket(SequencedPacketContainer packet)
		{
            try
			{
                List<ClientInformationSocket> targetClients = new();
                if (packet.ReceiverID > 1)
                {   // only target specific client
                    if (!GetClientById(packet.ReceiverID, out ClientInformationSocket client))
                    {   // client doesnt exist
                        Messaging.DebugMessage($"The client {packet.ReceiverID} is no longer connected to the server!");
                        MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                        return;
                    }

                    targetClients.Add(client);
                }
                else if (packet.ReceiverID == 0)
                {   // target all clients
                    targetClients.AddRange(_connectedClients.Values);
                }

                if (packet.Body.Length < _config.MTU)
                {   // send as complete packet
                    // write header and body to buffer
                    Writer writer = new();
                    writer.Skip(EPrimitiveLength.Int); // skip crc32
                    writer.Write(new PacketHeader(false, false, packet.NetworkChannel, packet.PacketType));
                    int sequencePos = writer.Position;
                    writer.Skip(EPrimitiveLength.Short); // skip sequence
                    writer.BlockCopy(ref packet.Body, 0, packet.Body.Length);

                    foreach (ClientInformationSocket client in _connectedClients.Values)
                    {
                        if (client.ID == packet.ExemptIDs)
                            continue;

                        ushort localSequence = (ushort)(client.ReliableLocalSequence + 1);

                        // calculate crc32 from buffer and write to it
                        writer.Position = sequencePos;
                        writer.WriteUInt16(localSequence);
                        writer.Position = 0;
                        byte[] bytesToHash = new byte[writer.Length];
                        Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                        Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, writer.Length - 4);
                        writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                        // send buffer to client and save it in case of resends
                        byte[] data = writer.GetBuffer();
                        _udpClient.Send(data, data.Length, client.Endpoint);
                        client.SendPacketsBuffer.AddOrUpdate(localSequence, data, (key, oldValue) => oldValue = data);
                        _ = ResendPacketData(client.Endpoint, localSequence);
                        client.ReliableLocalSequence++;
                    }

                    MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(true));
                }
                else
                {   // send as chunked packet
                    if (packet.Body.Length > _config.MTU * ushort.MaxValue)
					{
                        Messaging.DebugMessage($"No packet can be larger than {_config.MTU * ushort.MaxValue} bytes!");
                        MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                        return;
					}

                    ushort numberOfSlices = (ushort)(packet.Body.Length % _config.MTU == 0
                            ? packet.Body.Length / _config.MTU
                            : packet.Body.Length / _config.MTU + 1);

                    // write header and number of slices to buffer
                    Writer writer = new();
                    writer.Skip(EPrimitiveLength.Int); // skip crc32
                    writer.Write(new PacketHeader(false, true, packet.NetworkChannel, packet.PacketType));
                    int sequencePos = writer.Position;
                    writer.Skip(EPrimitiveLength.Short); // skip sequence
                    writer.WriteUInt16(numberOfSlices);

                    foreach (ClientInformationSocket client in _connectedClients.Values)
                    {
                        if (client.ID == packet.ExemptIDs)
                            continue;

                        ushort localSequence = (ushort)(client.ReliableLocalSequence + 1);

                        writer.Position = sequencePos;
                        writer.WriteUInt16(localSequence);
                        int startPosition = writer.Position;

                        // send slices individually to client
                        for (ushort i = 0; i < numberOfSlices; i++)
                        {   // reset body in writer buffer and fill with new slice
                            writer.Position = startPosition;
                            writer.WriteUInt16(i);

                            int length = i < numberOfSlices - 1 ? _config.MTU : packet.Body.Length % _config.MTU;
                            writer.BlockCopy(ref packet.Body, i * _config.MTU, length);

                            // calculate crc32 from buffer and write to it
                            writer.Position = 0;
                            byte[] bytesToHash = new byte[writer.Length];
                            Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                            Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, writer.Length - 4);
                            writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                            // send buffer to client and save it in case of resends
                            byte[] slice = writer.GetBuffer();
                            _udpClient.Send(slice, slice.Length, client.Endpoint);
                            client.SendChunksBuffer.AddOrUpdate((localSequence, i), slice, (key, oldValue) => oldValue = slice);
                            _ = ResendSliceData(client.Endpoint, (localSequence, i));

                            writer.Clear();
                        }

                        client.ReliableLocalSequence++;
                    }

                    MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(true));
                }
            }
            catch (Exception ex)
			{
                MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                switch (ex)
                {
                    case IndexOutOfRangeException:
                    case ArgumentException:
                        Messaging.DebugMessage("Something went wrong serialising a reliable packet!");
                        return;
                    case ThreadAbortException:
                        return;
                    case SocketException:
                    case ObjectDisposedException:
                        Messaging.DebugMessage(ex.ToString());
                        MainThreadQueue.Enqueue(() => Dispose());
                        return;
                }
            }
        }

        /// <summary>
        /// Retry sending a Packet Slice after a Delay and within a maximum number of retries
        /// </summary>
        /// <param name="client"></param>
        /// <param name="sequence"></param>
        /// <param name="retries"></param>
        /// <returns></returns>
        private async Task ResendSliceData(IPEndPoint clientEndpoint, (ushort, ushort) sequence, int retries = 0)
        {
            await Task.Delay((int)(_config.RTT * 1.25f));
            if (_connectedClients.TryGetValue(clientEndpoint, out ClientInformationSocket client)
                && client.SendChunksBuffer.TryGetValue(sequence, out byte[] data))
            {
                _udpClient.Send(data, data.Length, clientEndpoint);
                if (retries < _config.MaxNumberResendReliablePackets)
                    _ = ResendSliceData(clientEndpoint, sequence, retries + 1);
                else
                    RemoveClient(client.ID, true, ClosedReason.FailedACK);
            }
        }


        /// <summary>
        /// Retry sending a Packet after a Delay and within a maximum number of retries
        /// </summary>
        /// <param name="client"></param>
        /// <param name="sequence"></param>
        /// <param name="retries"></param>
        /// <returns></returns>
        private async Task ResendPacketData(IPEndPoint clientEndpoint, ushort sequence, int retries = 0)
        {
            await Task.Delay((int)(_config.RTT * 1.25f));
            if (_connectedClients.TryGetValue(clientEndpoint, out ClientInformationSocket client)
                && client.SendPacketsBuffer.TryGetValue(sequence, out byte[] data))
            {
                _udpClient.Send(data, data.Length, clientEndpoint);
                if (retries < _config.MaxNumberResendReliablePackets)
                    _ = ResendPacketData(clientEndpoint, sequence, retries + 1);
                else
                    RemoveClient(client.ID, true, ClosedReason.FailedACK);
            }
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Translates Client ID into Client Information, if it exists.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        private bool GetClientById(byte clientID, out ClientInformationSocket client)
        {
            if (!_idIpTable.TryGetValue(clientID, out IPEndPoint clientEndpoint)
                || !_connectedClients.TryGetValue(clientEndpoint, out ClientInformationSocket foundClient))
            {
                client = null;
                return false;
            }

            client = foundClient;
            return true;
        }

        /// <summary>
        /// Creates and adds new Client to all relevant collections and notifies the Manager and other connected Clients.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="username"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        private ClientInformationSocket AddClient(IPEndPoint endpoint, string username, Color32 color)
        {
            // find next available client id
            byte newID = 0;
            for (byte i = 2; i <= ServerInformation.MaxNumberConnectedClients; i++)
            {
                if (!_idIpTable.TryGetValue(i, out _))
                {
                    newID = i;
                    break;
                }
            }

            if (newID == 0)
                throw new Exception("Something went wrong assigning the Client ID!");

            // create client
            ClientInformationSocket newClient = new(newID, endpoint, username, color);
            if (!_connectedClients.TryAdd(endpoint, newClient) || !_idIpTable.TryAdd(newID, endpoint) || !_pendingConnections.TryRemove(endpoint, out _))
                throw new Exception("Something went wrong creating the Client!");

            // accept client
            Writer writer = new();
            writer.Write(new ConnectionAcceptedPacket(newID, ServerInformation.Servername, ServerInformation.MaxNumberConnectedClients));
            SendConnectionPacket(endpoint, EPacketType.ConnectionAccepted, writer.GetBuffer());

            // send server info to client
            ClientInfoPacket serverInfo = new(ClientInformation.ID, ClientInformation.Username, ClientInformation.Color);
            writer.Clear();
            writer.Write(serverInfo);
            _packetsToSend.Enqueue(new(newID, ENetworkChannel.ReliableUnordered, EPacketType.ClientInfo, writer.GetBuffer()));

            // send new client's info to other clients
            ClientInfoPacket newClientInfo = new(newID, newClient.Username, newClient.Color);
            writer.Clear();
            writer.Write(newClientInfo);
            _packetsToSend.Enqueue(new(0, ENetworkChannel.ReliableUnordered, EPacketType.ClientInfo, writer.GetBuffer(), newID));
            
            // TODO : make sure to buffer these on receiving client if accept is late
            foreach (ClientInformationSocket client in _connectedClients.Values)
            {   // send data of all other clients to new client
                if (client.ID == newID)
                    continue;

                ClientInfoPacket connectedClientInfo = new(client.ID, client.Username, client.Color);
                writer.Clear();
                writer.Write(connectedClientInfo);
                _packetsToSend.Enqueue(new(client.ID, ENetworkChannel.ReliableUnordered, EPacketType.ClientInfo, writer.GetBuffer()));
            }

            MainThreadQueue.Enqueue(() => OnClientConnected?.Invoke(newID));
            MainThreadQueue.Enqueue(() => OnConnectedClientListUpdated?.Invoke());

            Messaging.SystemMessage($"Client {newClient} connected!");
            return newClient;
        }

        /// <summary>
        /// Removes Client from all relevant collections and notifies the Manager and other connected Clients.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="saveClient"></param>
        /// <returns></returns>
        private bool RemoveClient(byte clientID, bool saveClient, ClosedReason reason = ClosedReason.Unknown)
        {
            if (!GetClientById(clientID, out ClientInformationSocket client))
                return false;

            _connectedClients.TryRemove(client.Endpoint, out _);

            List<IPEndPoint> remainingClients = _connectedClients.Values.Select(client => client.Endpoint).ToList();
            Writer writer = new();
            writer.Write(new ClientDisconnectedPacket(clientID));
            SendConnectionPacket(remainingClients, EPacketType.ClientDisconnected, writer.GetBuffer());
            writer.Clear();
            writer.Write(new ConnectionClosedPacket(reason));
            SendConnectionPacket(client.Endpoint, EPacketType.ConnectionClosed, writer.GetBuffer());

            MainThreadQueue.Enqueue(() => OnClientDisconnected?.Invoke(clientID));
            MainThreadQueue.Enqueue(() => OnConnectedClientListUpdated?.Invoke());

            Messaging.SystemMessage($"Client {client} disconnected!");

            if (saveClient)
            {
                // TODO : save disconnected clients in buffer unless forcefully disconnected
            }
            return true;
        }

        private void SendConnectionPacket(IPEndPoint target, EPacketType packetType, byte[] data)
        {
			List<IPEndPoint> targets = new() { target };
			SendConnectionPacket(targets, packetType, data);
        }

        private void SendConnectionPacket(List<IPEndPoint> targets, EPacketType packetType, byte[] data)
		{   // set packet type and packet bytes
            Writer writer = new();
            writer.Skip(EPrimitiveLength.Int);
            writer.Write<PacketHeader>(new(packetType));
            writer.BlockCopy(ref data, 0, data.Length);

            // set crc32
            byte[] bytesToHash = new byte[writer.Length];
            Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
            Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, bytesToHash.Length - 4);
            writer.Position = 0;
            writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

            // send to clients
            foreach (IPEndPoint endpoint in targets)
                _udpClient.Send(writer.GetBuffer(), writer.Length, endpoint);
        }

        private static bool CompareByteArrays(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            return a.SequenceEqual(b);
        }

		#endregion
	}
}
