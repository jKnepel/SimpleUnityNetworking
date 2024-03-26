using System;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
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
}
