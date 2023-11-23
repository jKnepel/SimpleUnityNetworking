using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery
{
    public class ServerDiscoveryManager
    {
		#region properties

		public bool IsActive { get; private set; }
        public List<OpenServer> OpenServers => _openServers.Values.ToList();

        public Action OnServerDiscoveryActivated;
        public Action OnServerDiscoveryDeactivated;
        public Action OnOpenServerListUpdated;

        #endregion

        #region fields

        private NetworkConfiguration _config;
		private IPAddress _localIP;
		private IPAddress _discoveryIP;
        private UdpClient _udpClient;
        private Thread _discoveryThread;

        private readonly ConcurrentDictionary<IPEndPoint, OpenServer> _openServers = new();

		#endregion

		#region public methods

		public bool StartServerDiscovery(NetworkConfiguration config)
        {
            if (IsActive)
                return true;

            try { _discoveryIP = IPAddress.Parse(config.DiscoveryIP); }
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
                }
                OnServerDiscoveryDeactivated?.Invoke();
                return IsActive = false;
            }

            try
            {
                _config = config;
                _localIP = config.LocalIPAddresses[config.LocalIPAddressIndex];

                _udpClient = new();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_discoveryIP, _localIP));
                _udpClient.Client.Bind(new IPEndPoint(_localIP, config.DiscoveryPort));

                _discoveryThread = new(() => DiscoveryThread()) { IsBackground = true };
                _discoveryThread.Start();

                OnServerDiscoveryActivated?.Invoke();
                return IsActive = true;
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case FormatException:
                        Messaging.DebugMessage("The Server Discovery Multicast IP is not a valid Address!");
                        break;
                    case ArgumentOutOfRangeException:
                        Messaging.DebugMessage("The Discovery Port is outside the possible range!");
                        break;
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
                        ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                }
                OnServerDiscoveryDeactivated?.Invoke();
                return IsActive = false;
            }
        }

        public void EndServerDiscovery()
		{
            if (!IsActive)
                return;

            if (_udpClient != null)
            {
                _udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, new MulticastOption(_discoveryIP, _localIP));
                _udpClient.Close();
                _udpClient.Dispose();
            }
            if (_discoveryThread != null)
            {
                _discoveryThread.Abort();
                _discoveryThread.Join();
            }

            IsActive = false;
            OnServerDiscoveryDeactivated?.Invoke();
        }

        public bool RestartServerDiscovery(NetworkConfiguration config)
		{
            EndServerDiscovery();
            return StartServerDiscovery(config);
		}

		#endregion

		#region private methods

		private void DiscoveryThread()
        {
            while (true)
            {
                try
                {
                    // get packet ip headers
                    IPEndPoint receiveEndpoint = new(1, 1);
                    byte[] receivedBytes = _udpClient.Receive(ref receiveEndpoint);
                    if (!ShouldAcceptConnection(receiveEndpoint))
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
                    if (header.PacketType != EPacketType.ServerInformation)
                        continue;

                    ServerInformationPacket packet = reader.Read<ServerInformationPacket>();
                    OpenServer newServer = new(receiveEndpoint, packet.Servername, packet.MaxNumberOfClients, packet.NumberOfClients);
                    if (!_openServers.TryGetValue(receiveEndpoint, out OpenServer _))
                        _ = TimeoutServer(receiveEndpoint);

                    // add new values or update server with new values
                    _openServers.AddOrUpdate(receiveEndpoint, newServer, (key, value) => value = newServer);

                    MainThreadQueue.Enqueue(() => OnOpenServerListUpdated?.Invoke());
                }
                catch (Exception ex)
                {
                    switch (ex)
                    {
                        case IndexOutOfRangeException:
                        case ArgumentException:
                            continue;
                        case ThreadAbortException:
                            IsActive = false;
                            return;
                        default:
                            Messaging.DebugMessage("An Error occurred in the Server Discovery!");
                            IsActive = false;
                            return;
                    }
                }
            }
        }

        private async Task TimeoutServer(IPEndPoint serverEndpoint)
        {
            await Task.Delay(_config.ServerDiscoveryTimeout);
            if (_openServers.TryGetValue(serverEndpoint, out OpenServer server))
            {   // timeout and remove servers that haven't been updated for longer than the timeout value
                if ((DateTime.Now - server.LastHeartbeat).TotalMilliseconds > _config.ServerDiscoveryTimeout)
                {
                    _openServers.TryRemove(serverEndpoint, out _);
                    MainThreadQueue.Enqueue(() => OnOpenServerListUpdated?.Invoke());
                    return;
                }

                _ = TimeoutServer(serverEndpoint);
            }
        }

        private bool ShouldAcceptConnection(IPEndPoint endpoint)
		{
            if (endpoint.Address.Equals(_localIP) && endpoint.Port == _config.LocalPort)
                return false;
            return !endpoint.Address.Equals(_localIP) || _config.AllowLocalConnections;
		}

        #endregion
    }
}
