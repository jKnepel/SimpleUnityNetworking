using System;
using jKnepel.SimpleUnityNetworking.Networking;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    public abstract class Transport : IDisposable
    {
        /// <summary>
        /// Whether a local server or client is started
        /// </summary>
        public abstract bool IsOnline { get; }
        /// <summary>
        /// Whether a local server is started
        /// </summary>
        public abstract bool IsServer { get; }
        /// <summary>
        /// Whether a local client is started
        /// </summary>
        public abstract bool IsClient { get; }
        /// <summary>
        /// Whether a local server and client is started
        /// </summary>
        public abstract bool IsHost { get; }

        /// <summary>
        /// The current connection state of the local server
        /// </summary>
        public abstract ELocalConnectionState LocalServerState { get; }
        /// <summary>
        /// The current connection state of the local client
        /// </summary>
        public abstract ELocalConnectionState LocalClientState { get; }
        
        /// <summary>
        /// Called when the local server has received data
        /// </summary>
        public abstract event Action<ServerReceivedData> OnServerReceivedData;
        /// <summary>
        /// Called when the local client has received data
        /// </summary>
        public abstract event Action<ClientReceivedData> OnClientReceivedData;
        /// <summary>
        /// Called when the local server's connection state has been updated
        /// </summary>
        public abstract event Action<ELocalConnectionState> OnServerStateUpdated;
        /// <summary>
        /// Called when the local client's connection state has been updated
        /// </summary>
        public abstract event Action<ELocalConnectionState> OnClientStateUpdated;
        /// <summary>
        /// Called when a remote client's connection state has been updated
        /// </summary>
        public abstract event Action<uint, ERemoteConnectionState> OnConnectionUpdated;
        
        ~Transport()
        {
            Dispose(false);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);

        public abstract void SetTransportSettings(TransportSettings settings);
        public abstract void StartServer();
        public abstract void StopServer();
        public abstract void StartClient();
        public abstract void StopClient();
        public abstract void StopNetwork();
        public abstract void IterateIncoming();
        public abstract void IterateOutgoing();
        public abstract void SendDataToServer(byte[] data, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered);
        public abstract void SendDataToClient(uint clientID, byte[] data, ENetworkChannel channel = ENetworkChannel.UnreliableUnordered);
        public abstract void DisconnectClient(uint clientID);
    }
    
    [Serializable]
    public class TransportSettings
    {
        /// <summary>
        /// The address to which the local client will attempt to connect with.
        /// </summary>
        public string Address = "127.0.0.1";
        /// <summary>
        /// The port to which the local client will attempt to connect with or the server will bind to locally.
        /// </summary>
        public ushort Port = 24856;
        /// <summary>
        /// Address to which the local server will be bound. If no address is provided, the IPv4 Loopback
        /// address will be used instead.
        /// </summary>
        public string ServerListenAddress = string.Empty;
        /// <summary>
        /// The maximum number of connections allowed by the local server. 
        /// </summary>
        public int MaxNumberOfClients = 100;
        /// <summary>
        /// Time between connection attempts.
        /// </summary>
        public int ConnectTimeoutMS = 1000;
        /// <summary>
        /// Maximum number of connection attempts to try. If no answer is received from the server
        /// after this number of attempts, a disconnect event is generated for the connection.
        /// </summary>
        public int MaxConnectAttempts = 60;
        /// <summary>
        /// Inactivity timeout for a connection. If nothing is received on a connection for this
        /// amount of time, it is disconnected. To prevent this from happening when the game session is simply
        /// quiet, set <c>HeartbeatTimeoutMS</c> to a positive non-zero value.
        /// </summary>
        public int DisconnectTimeoutMS = 30000;
        /// <summary>
        /// Time after which if nothing from a peer is received, a heartbeat message will be sent
        /// to keep the connection alive. Prevents the <c>DisconnectTimeoutMS</c> mechanism from
        /// kicking when nothing happens on a connection. A value of 0 will disable heartbeats.
        /// </summary>
        public int HeartbeatTimeoutMS = 500;
        /// <summary>
        /// Maximum size that can be fragmented. Attempting to send a message larger than that will
        /// result in the send operation failing. Maximum value is ~20MB for unreliable packets,
        /// and ~88KB for reliable ones.
        /// </summary>
        public int PayloadCapacity = 4096;
        /// <summary>
        /// Maximum number in-flight packets per pipeline/connection combination. Default value
        /// is 32 but can be increased to 64 at the cost of slightly larger packet headers.
        /// </summary>
        public int WindowSize = 32;
        /// <summary>
        /// Minimum amount of time to wait before a reliable packet is resent if it's not been
        /// acknowledged.
        /// </summary>
        public int MinimumResendTime = 64;
        /// <summary>
        /// Maximum amount of time to wait before a reliable packet is resent if it's not been
        /// acknowledged. That is, even with a high RTT the reliable pipeline will never wait
        /// longer than this value to resend a packet.
        /// </summary>
        public int MaximumResendTime = 200;
    }
    
    public struct ServerReceivedData
    {
        public uint ClientID;
        public byte[] Data;
        public DateTime Timestamp;
        public ENetworkChannel Channel;
    }

    public struct ClientReceivedData
    {
        public byte[] Data;
        public DateTime Timestamp;
        public ENetworkChannel Channel;
    }

    public enum ELocalConnectionState
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

    public enum ERemoteConnectionState
    {
        /// <summary>
        /// Signifies that a remote connection has been established
        /// </summary>
        Connected,
        /// <summary>
        /// Signifies that an established remote connection was closed
        /// </summary>
        Disconnected
    }
}
