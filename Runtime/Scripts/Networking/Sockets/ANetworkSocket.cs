using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace jKnepel.SimpleUnityNetworking.Networking.Sockets
{
    public abstract partial class ANetworkSocket : IDisposable
    {
        #region protected fields

        protected UdpClient _udpClient;

        protected Thread _listenerThread;
        protected Thread _senderThread;

        protected int _disposeCount;

        #endregion

        #region public properties

        /// <summary>
        /// Action for when connection to or creation of a server is being started.
        /// </summary>
        public Action OnConnecting;

        /// <summary>
        /// Action for when successfully connecting to or creating a Server.
        /// </summary>
        public Action OnConnected;

        /// <summary>
        /// Action for when disconnecting from or closing the Server.
        /// </summary>
        public Action OnDisconnected;

        public Action OnConnectionStatusUpdated;

        private EConnectionStatus _connectionStatus = EConnectionStatus.IsDisconnected;
        public EConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            protected set
            {
                if (value == _connectionStatus)
                    return;

                _connectionStatus = value;
                switch (value)
                {
                    case EConnectionStatus.IsConnecting:
                        OnConnecting?.Invoke();
                        break;
                    case EConnectionStatus.IsConnected:
                        OnConnected?.Invoke();
                        break;
                    case EConnectionStatus.IsDisconnected:
                        OnDisconnected?.Invoke();
                        break;
                }
                OnConnectionStatusUpdated?.Invoke();
            }
        }

        public bool IsConnected => ConnectionStatus == EConnectionStatus.IsConnected;

        /// <summary>
        /// Information about the Local Server/the Server that you are connected to.
        /// </summary>
        public ServerInformation ServerInformation { get; protected set; }

        public ClientInformation ClientInformation { get; protected set; }

        public abstract ConcurrentDictionary<byte, ClientInformation> ConnectedClients { get; }

        #endregion

        #region lifecycle

        ~ANetworkSocket()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected abstract void Dispose(bool disposing);

        #endregion

        #region public methods

        public abstract void DisconnectFromServer();

        #endregion

        #region helper methods

        private const ushort HALF_USHORT = ushort.MaxValue / 2;

        /// <summary>
        /// Checks if a packet is new by comparing the packets sequence number and the corresponding remote sequence number.
        /// Also handles ushort wrap-arounds by allowing packets that are smaller by half of the maximum value.
        /// </summary>
        /// <param name="packetSequence"></param>
        /// <param name="remoteSequence"></param>
        /// <returns>if the packet is new</returns>
        protected static bool IsNewPacket(ushort packetSequence, ushort remoteSequence)
        {
            return ((packetSequence > remoteSequence) && (packetSequence - remoteSequence <= HALF_USHORT))
                || ((packetSequence < remoteSequence) && (remoteSequence - packetSequence > HALF_USHORT));
        }

        /// <summary>
        /// Checks if a packet is next after remote sequence number.
        /// </summary>
        /// <param name="packetSequence"></param>
        /// <param name="remoteSequence"></param>
        /// <returns>if the packet is new</returns>
        protected static bool IsNextPacket(ushort packetSequence, ushort remoteSequence)
        {
            return (packetSequence == (ushort)(remoteSequence + 1))
                || (packetSequence == 0 && remoteSequence == ushort.MaxValue);
        }

        protected static bool IsReliableChannel(ENetworkChannel channel)
		{
            return channel == ENetworkChannel.ReliableOrdered || channel == ENetworkChannel.ReliableUnordered;
		}

        protected static bool IsOrderedChannel(ENetworkChannel channel)
		{
            return channel == ENetworkChannel.ReliableOrdered || channel == ENetworkChannel.UnreliableOrdered;
		}

        #endregion
    }
}
