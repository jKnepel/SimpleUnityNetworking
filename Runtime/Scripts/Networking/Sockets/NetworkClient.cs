using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Sockets
{
    public sealed class NetworkClient : ANetworkSocket
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
		/// Action for when the server the local client was connected to was closed.
		/// </summary>
		public Action OnServerWasClosed;

        public override ConcurrentDictionary<byte, ClientInformation> ConnectedClients => _connectedClients;

        #endregion

        #region private fields

        private IPEndPoint _localEndpoint;
        private IPEndPoint _serverEndpoint;
        private Action<bool> _onConnectionEstablished;

        private readonly ConcurrentDictionary<byte, ClientInformation> _connectedClients = new();

        private readonly ConcurrentQueue<SequencedPacketContainer> _packetsToSend = new();

        private readonly ConcurrentDictionary<ushort, (PacketHeader, byte[])> _receivedPacketsBuffer = new();
        private readonly ConcurrentDictionary<ushort, ConcurrentDictionary<ushort, byte[]>> _receivedChunksBuffer = new();

        private readonly ConcurrentDictionary<ushort, byte[]> _sendPacketsBuffer = new();
        private readonly ConcurrentDictionary<(ushort, ushort), byte[]> _sendChunksBuffer = new();

        private ushort _unreliableLocalSequence = 0;
        private ushort _unreliableRemoteSequence = 0;
        private ushort _reliableLocalSequence = 0;
        private ushort _reliableRemoteSequence = 0;

        #endregion

        #region lifecycle

        public NetworkClient() { }

        public void ConnectToServer(NetworkConfiguration config, IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
            ConnectionStatus = EConnectionStatus.IsConnecting;

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

            try { _serverEndpoint = new(serverIP, serverPort); }
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

            try
            {
                _udpClient = new();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(_localEndpoint);
                _udpClient.Connect(_serverEndpoint);

                NetworkConfiguration = config;
                _onConnectionEstablished = onConnectionEstablished;

                _listenerThread = new(() => ListenerThread()) { IsBackground = true };
                _listenerThread.Start();
                _senderThread = new(() => SenderThread()) { IsBackground = true };
                _senderThread.Start();

                Messaging.SystemMessage("Connecting to Server...");
                Writer writer = new();
                writer.Write(new ConnectionRequestPacket());
                SendConnectionPacket(EPacketType.ConnectionRequest, writer.GetBuffer());
                _ = TimeoutEstablishConnection(onConnectionEstablished);
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
            }
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

                if (NetworkConfiguration != null)
				{
                    NetworkConfiguration.LocalPort = 0;
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

            Writer writer = new();
            writer.Write(new ConnectionClosedPacket(ClosedReason.ClientDisconnected));
            SendConnectionPacket(EPacketType.ConnectionClosed, writer.GetBuffer());

            Messaging.SystemMessage("Disconnected from the server!");
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

            byte[] structBuffer;
			if (NetworkConfiguration.SerialiserConfiguration.CompressFloats
				|| NetworkConfiguration.SerialiserConfiguration.CompressQuaternions)
            {
                BitWriter structWriter = new(NetworkConfiguration.SerialiserConfiguration);
				structWriter.Write(StructData);
				structBuffer = structWriter.GetBuffer();
			}
            else
            {
				Writer structWriter = new(NetworkConfiguration.SerialiserConfiguration);
				structWriter.Write(StructData);
				structBuffer = structWriter.GetBuffer();
			}

            Writer writer = new();
            DataPacket DataPacket = new(true, Hashing.GetFNV1Hash32(typeof(T).Name), receiverID, structBuffer);
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

            DataPacket DataPacket = new(false, Hashing.GetFNV1Hash32(id), receiverID, data);
            Writer writer = new(NetworkConfiguration.SerialiserConfiguration);
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
                    if (!receiveEndpoint.Equals(_serverEndpoint))
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

                    // handle individual packet types
                    switch (header.PacketType)
                    {
                        case EPacketType.ServerInformation:
							{
                                ServerInformationPacket packet = reader.Read<ServerInformationPacket>();
                                HandleServerInformationPacket(packet);
                                break;
							}
                        case EPacketType.ACK:
							{
                                ACKPacket packet = reader.Read<ACKPacket>();
                                HandleACKPacket(packet);
                                break;
							}
                        case EPacketType.ChunkedACK:
                            {
                                ChunkedACKPacket packet = reader.Read<ChunkedACKPacket>();
                                HandleChunkedACKPacket(packet);
                                break;
                            }
                        case EPacketType.ConnectionChallenge:
							{
                                ConnectionChallengePacket packet = reader.Read<ConnectionChallengePacket>();
                                HandleConnectionChallengePacket(packet);
                                break;
							}
                        case EPacketType.ConnectionAccepted:
							{
                                ConnectionAcceptedPacket packet = reader.Read<ConnectionAcceptedPacket>();
                                HandleConnectionAcceptedPacket(packet);
                                break;
							}
                        case EPacketType.ConnectionDenied:
							{
                                ConnectionDeniedPacket packet = reader.Read<ConnectionDeniedPacket>();
                                HandleConnectionDeniedPacket(packet);
                                break;
							}
                        case EPacketType.ConnectionClosed:
							{
                                ConnectionClosedPacket packet = reader.Read<ConnectionClosedPacket>();
                                HandleConnectionClosedPacket(packet);
                                break;
							}
                        case EPacketType.ClientDisconnected:
							{
                                ClientDisconnectedPacket packet = reader.Read<ClientDisconnectedPacket>();
                                HandleClientDisconnectedPacket(packet);
                                break;
							}
                        case EPacketType.Data:
                        case EPacketType.ClientInfo:
                            {
                                if (header.IsChunkedPacket)
                                    HandleChunkedSequencedPacket(header, reader);
                                else
									HandleSequencedPacket(header, reader);
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

        private void HandleServerInformationPacket(ServerInformationPacket packet)
        {
            ServerInformation = new(_serverEndpoint, packet.Servername, packet.MaxNumberOfClients);
        }

        private void HandleACKPacket(ACKPacket packet)
        {
            if (!IsConnected)
                return;

            ServerInformation.LastHeartbeat = DateTime.Now;
            _sendPacketsBuffer.TryRemove(packet.Sequence, out _);
        }

        private void HandleChunkedACKPacket(ChunkedACKPacket packet)
        {
            if (!IsConnected)
                return;

            ServerInformation.LastHeartbeat = DateTime.Now;
            _sendChunksBuffer.TryRemove((packet.Sequence, packet.SliceNumber), out _);
        }

        private void HandleConnectionChallengePacket(ConnectionChallengePacket packet)
        {
            if (ConnectionStatus != EConnectionStatus.IsConnecting)
                return;

            SHA256 h = SHA256.Create();
            Writer writer = new();
            writer.WriteUInt64(packet.Challenge);
            byte[] hashedChallenge = h.ComputeHash(writer.GetBuffer());
            writer.Clear();
            writer.Write(new ChallengeAnswerPacket(hashedChallenge, NetworkConfiguration.Username, NetworkConfiguration.Color));
            SendConnectionPacket(EPacketType.ChallengeAnswer, writer.GetBuffer());
        }

        private void HandleConnectionAcceptedPacket(ConnectionAcceptedPacket packet)
        {
            if (ConnectionStatus != EConnectionStatus.IsConnecting)
                return;

            ClientInformation = new(packet.ClientID, NetworkConfiguration.Username, NetworkConfiguration.Color);
            ServerInformation = new(_serverEndpoint, packet.Servername, packet.MaxNumberConnectedClients);

            MainThreadQueue.Enqueue(() => _onConnectionEstablished?.Invoke(true));
            MainThreadQueue.Enqueue(() => ConnectionStatus = EConnectionStatus.IsConnected);

            Messaging.SystemMessage("Connected to Server!");
        }

        private void HandleConnectionDeniedPacket(ConnectionDeniedPacket packet)
        {
            if (ConnectionStatus != EConnectionStatus.IsConnecting)
                return;

            MainThreadQueue.Enqueue(() => _onConnectionEstablished?.Invoke(false));
            MainThreadQueue.Enqueue(() => Dispose());

            switch (packet.Reason)
			{
                case DeniedReason.Unknown:
                    Messaging.SystemMessage("Connection has been denied!");
                    break;
                case DeniedReason.InvalidChallengeAnswer:
                    Messaging.SystemMessage("Connection has been denied because the challenge answer was incorrect!");
                    break;
                case DeniedReason.NoSpace:
                    Messaging.SystemMessage("Connection has been denied because server has no space available!");
                    break;
				default:
					Messaging.SystemMessage("Something went wrong. Try again later!");
					break;
			}
        }

        private void HandleConnectionClosedPacket(ConnectionClosedPacket packet)
        {
            if (!IsConnected)
                return;

            MainThreadQueue.Enqueue(() => Dispose());

            switch (packet.Reason)
			{
                case ClosedReason.Unknown:
                    Messaging.SystemMessage("Client was disconnected from the host!");
                    break;
                case ClosedReason.ServerWasClosed:
                    Messaging.SystemMessage("Server was closed by the host!");
                    OnServerWasClosed?.Invoke();
                    break;
                case ClosedReason.FailedACK:
                    Messaging.SystemMessage("Client dropped too many packets!");
                    break;
                default:
                    Messaging.SystemMessage("Something went wrong. Try again later!");
                    break;
            }
        }

        private void HandleClientDisconnectedPacket(ClientDisconnectedPacket packet)
        {
            if (!IsConnected)
                return;

            if (!_connectedClients.TryRemove(packet.ClientID, out ClientInformation client))
                return;

            MainThreadQueue.Enqueue(() => OnClientDisconnected?.Invoke(packet.ClientID));
            MainThreadQueue.Enqueue(() => OnConnectedClientListUpdated.Invoke());

            Messaging.SystemMessage($"Client {client} disconnected!");
        }

        private void HandleSequencedPacket(PacketHeader header, Reader reader)
        {
            ushort sequence = reader.ReadUInt16();

            // unreliable packet sequence
            if (!IsReliableChannel(header.NetworkChannel))
            {   // ignore old packets unless they are unordered
                if (!IsNewPacket(sequence, _unreliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                    return;

                // update sequence and consume packet
                _unreliableRemoteSequence = sequence;
                ConsumeSequencedPacket(header, reader.ReadRemainingBuffer());
                return;
            }

            // reliable packet sequence
            {
                // send ACK for reliable sequence
                Writer writer = new();
                writer.Write(new ACKPacket(sequence));
                SendConnectionPacket(EPacketType.ACK, writer.GetBuffer());

                // ignore old packets unless they are unordered
                if (!IsNewPacket(sequence, _reliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                    return;

                if (!IsNextPacket(sequence, _reliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                {   // if a packet is missing in the sequence keep it in the buffer
                    _receivedPacketsBuffer.TryAdd(sequence, (header, reader.ReadRemainingBuffer()));
                    return;
                }

                // update sequence and consume packet
                _reliableRemoteSequence = sequence;
                ConsumeSequencedPacket(header, reader.ReadRemainingBuffer());

                // apply all packets from that sender's buffer that are now next in the sequence
                while (_receivedPacketsBuffer.Count > 0)
                {
                    sequence++;
                    if (!_receivedPacketsBuffer.TryRemove(sequence, out (PacketHeader, byte[]) bufferedPacket))
                        break;

                    // update sequence and consume packet
                    _reliableRemoteSequence = sequence;
                    ConsumeSequencedPacket(bufferedPacket.Item1, bufferedPacket.Item2);
                }
            }
        }

        private void HandleChunkedSequencedPacket(PacketHeader header, Reader reader)
        {
            if (!IsReliableChannel(header.NetworkChannel))
            {
                Messaging.DebugMessage("A unreliable chunked packet has been received. Make sure the host is legit!");
                return;
            }

            ushort sequence = reader.ReadUInt16();
            ushort numberOfSlices = reader.ReadUInt16();
            ushort sliceNumber = reader.ReadUInt16();
            byte[] sliceData = reader.ReadRemainingBuffer();

            // send ACK
            Writer writer = new();
            writer.Write(new ChunkedACKPacket(sequence, sliceNumber));
            SendConnectionPacket(EPacketType.ChunkedACK, writer.GetBuffer());

            // ignore old packets unless they are unordered
            if (!IsNewPacket(sequence, _reliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
                return;

            if (!_receivedChunksBuffer.TryGetValue(sequence, out ConcurrentDictionary<ushort, byte[]> bufferedChunk))
            {   // create chunked packet if it doesn't exist yet
                bufferedChunk = new();
                _receivedChunksBuffer.TryAdd(sequence, bufferedChunk);
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
            _receivedChunksBuffer.TryRemove(sequence, out _);

            if (!IsNextPacket(sequence, _reliableRemoteSequence) && IsOrderedChannel(header.NetworkChannel))
            {   // if a packet is missing in the sequence keep it in the buffer
                _receivedPacketsBuffer.TryAdd(sequence, (header, chunkData));
                return;
            }

            // update sequence and consume packet
            _reliableRemoteSequence = sequence;
            ConsumeSequencedPacket(header, chunkData);

			// apply all packets from that sender's buffer that are now next in the sequence
			while (_receivedPacketsBuffer.Count > 0)
			{
				sequence++;
				if (!_receivedPacketsBuffer.TryRemove(sequence, out (PacketHeader, byte[]) bufferedPacket))
					break;

				// update sequence and consume packet
				_reliableRemoteSequence = sequence;
				ConsumeSequencedPacket(bufferedPacket.Item1, bufferedPacket.Item2);
			}
		}

        private void ConsumeSequencedPacket(PacketHeader header, byte[] data)
        {
            switch (header.PacketType)
            {
                case EPacketType.ClientInfo:
					{
                        Reader reader = new(data);
                        ClientInfoPacket packet = reader.Read<ClientInfoPacket>();

                        // add or update connected client
                        ClientInformation newClient = new(packet.ClientID, packet.Username, packet.Color);

                        if (_connectedClients.TryGetValue(packet.ClientID, out ClientInformation oldClient))
                        {
                            _connectedClients.TryUpdate(packet.ClientID, oldClient, newClient);
                        }
                        else
                        {
                            _connectedClients.TryAdd(packet.ClientID, newClient);
                            MainThreadQueue.Enqueue(() => OnClientConnected?.Invoke(packet.ClientID));
                            Messaging.SystemMessage($"Client {newClient} connected!");
                        }

                        MainThreadQueue.Enqueue(() => OnConnectedClientListUpdated?.Invoke());
                        break;
                    }
                case EPacketType.Data:
					{
                        // notify manager of received data, consuming the packet
                        Reader reader = new(data);
                        DataPacket packet = reader.Read<DataPacket>();
                        if (packet.IsStructData)
                            MainThreadQueue.Enqueue(() => ReceiveStructData(packet.DataID, packet.ClientID, packet.Data));
                        else
                            MainThreadQueue.Enqueue(() => ReceiveByteData(packet.DataID, packet.ClientID, packet.Data));
                        break;
					}
                default: break;
            }
        }

        #endregion

        #region sender logic

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
                writer.Skip(writer.Int32); // skip crc32 
                writer.Write(new PacketHeader(false, false, packet.NetworkChannel, packet.PacketType));
                writer.WriteUInt16((ushort)(_unreliableLocalSequence + 1));
                writer.BlockCopy(ref packet.Body, 0, packet.Body.Length);

                if (writer.Length > NetworkConfiguration.MTU)
                {   // only allow unreliable packets smaller than the mtu
                    Messaging.DebugMessage($"No unreliable packet can be larger than the MTU of {NetworkConfiguration.MTU} bytes!");
                    MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                    return;
                }

                // write crc32 to the buffer
                writer.Position = 0;
                byte[] bytesToHash = new byte[writer.Length];
                Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, writer.Length - 4);
                writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                _udpClient.Send(writer.GetBuffer(), writer.Length);
                _unreliableLocalSequence++;

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
                if (packet.Body.Length < NetworkConfiguration.MTU)
                {   // send as complete packet
                    // write header and body to buffer
                    Writer writer = new();
                    writer.Skip(writer.Int32); // skip crc32
                    writer.Write(new PacketHeader(false, false, packet.NetworkChannel, packet.PacketType));
                    writer.WriteUInt16((ushort)(_reliableLocalSequence + 1));
                    writer.BlockCopy(ref packet.Body, 0, packet.Body.Length);

                    // write crc32 to the buffer
                    writer.Position = 0;
                    byte[] bytesToHash = new byte[writer.Length];
                    Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                    Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, writer.Length - 4);
                    writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                    // send buffer to client and save it in case of resends
                    byte[] data = writer.GetBuffer();
                    _udpClient.Send(data, data.Length);
                    _sendPacketsBuffer.AddOrUpdate((ushort)(_reliableLocalSequence + 1), data, (key, oldValue) => oldValue = data);
                    _ = ResendPacketData((ushort)(_reliableLocalSequence + 1));
                    _reliableLocalSequence++;

                    MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(true));
                }
                else
                {   // send as chunked packet
                    if (packet.Body.Length > NetworkConfiguration.MTU * ushort.MaxValue)
                    {
                        Messaging.DebugMessage($"No packet can be larger than {NetworkConfiguration.MTU * ushort.MaxValue} bytes!");
                        MainThreadQueue.Enqueue(() => packet.OnPacketSend?.Invoke(false));
                        return;
                    }

                    ushort numberOfSlices = (ushort)(packet.Body.Length % NetworkConfiguration.MTU == 0
                            ? packet.Body.Length / NetworkConfiguration.MTU
                            : packet.Body.Length / NetworkConfiguration.MTU + 1);

                    // write header, sequence and number of slices to buffer
                    Writer writer = new();
                    writer.Skip(writer.Int32); // skip crc32
                    writer.Write(new PacketHeader(false, true, packet.NetworkChannel, packet.PacketType));
                    writer.WriteUInt16((ushort)(_reliableLocalSequence + 1));
                    writer.WriteUInt16(numberOfSlices);
                    int slicePosition = writer.Position;

                    // send slices individually to client
                    for (ushort i = 0; i < numberOfSlices; i++)
                    {   // reset body in writer buffer and fill with new slice
                        writer.Position = slicePosition;
                        writer.WriteUInt16(i);

                        int length = i < numberOfSlices - 1 ? NetworkConfiguration.MTU : packet.Body.Length % NetworkConfiguration.MTU;
                        writer.BlockCopy(ref packet.Body, i * NetworkConfiguration.MTU, length);

                        // calculate crc32 from buffer and write to it
                        writer.Position = 0;
                        byte[] bytesToHash = new byte[writer.Length];
                        Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
                        Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, writer.Length - 4);
                        writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                        // send buffer to client and save it in case of resends
                        byte[] slice = writer.GetBuffer();
                        _udpClient.Send(slice, slice.Length);
                        _sendChunksBuffer.AddOrUpdate(((ushort)(_reliableLocalSequence + 1), i), slice, (key, oldValue) => oldValue = slice);
                        _ = ResendSliceData(((ushort)(_reliableLocalSequence + 1), i));

                        writer.Clear();
                    }

                    _reliableLocalSequence++;
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
        private async Task ResendSliceData((ushort, ushort) sequence, int retries = 0)
        {
            await Task.Delay((int)(NetworkConfiguration.RTT * 1.25f));
            if (_sendChunksBuffer.TryGetValue(sequence, out byte[] data))
            {
                _udpClient.Send(data, data.Length);
                if (retries < NetworkConfiguration.MaxNumberResendReliablePackets)
                    _ = ResendSliceData(sequence, retries + 1);
                else
                    DisconnectFromServer();
            }
        }


        /// <summary>
        /// Retry sending a Packet after a Delay and within a maximum number of retries
        /// </summary>
        /// <param name="client"></param>
        /// <param name="sequence"></param>
        /// <param name="retries"></param>
        /// <returns></returns>
        private async Task ResendPacketData(ushort sequence, int retries = 0)
        {
            await Task.Delay((int)(NetworkConfiguration.RTT * 1.25f));
            if (_sendPacketsBuffer.TryGetValue(sequence, out byte[] data))
            {
                _udpClient.Send(data, data.Length);
                if (retries < NetworkConfiguration.MaxNumberResendReliablePackets)
                    _ = ResendPacketData(sequence, retries + 1);
                else
                    DisconnectFromServer();
            }
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Stops the process of establishing a Connection, if it did not success within a given timeout.
        /// </summary>
        /// <returns></returns>
        private async Task TimeoutEstablishConnection(Action<bool> onConnectionEstablished)
        {
            await Task.Delay(NetworkConfiguration.ServerConnectionTimeout);
            if (!IsConnected)
            {
                Dispose();
                onConnectionEstablished?.Invoke(false);
            }
        }

        private void SendConnectionPacket(EPacketType packetType, byte[] data)
        {   // set packet type and packet bytes
            Writer writer = new();
            writer.Skip(writer.Int32);
            writer.Write<PacketHeader>(new(packetType));
            writer.BlockCopy(ref data, 0, data.Length);

            // set crc32
            byte[] bytesToHash = new byte[writer.Length];
            Buffer.BlockCopy(NetworkConfiguration.ProtocolBytes, 0, bytesToHash, 0, 4);
            Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, bytesToHash.Length - 4);
            writer.Position = 0;
			writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

            // send to server
            _udpClient.Send(writer.GetBuffer(), writer.Length);
        }

        #endregion
    }
}
