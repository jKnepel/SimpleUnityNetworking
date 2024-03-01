using System;
using System.Collections;
using System.Collections.Generic;
using jKnepel.SimpleUnityNetworking.Networking;
using Unity.Networking.Transport;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    public abstract class Transport
    {
        public abstract bool IsOnline { get; }
        public abstract bool IsServer { get; }
        public abstract bool IsClient { get; }
        public abstract bool IsHost { get; }

        public abstract ELocalConnectionState LocalServerState { get; }
        public abstract ELocalConnectionState LocalClientState { get; }
        
        public abstract event Action<ServerReceivedData> OnServerReceivedData;
        public abstract event Action<ClientReceivedData> OnClientReceivedData;
        public abstract event Action<ELocalConnectionState> OnServerStateUpdated;
        public abstract event Action<ELocalConnectionState> OnClientStateUpdated;
        public abstract event Action<int, ERemoteConnectionState> OnConnectionUpdated;

        public abstract void StartServer();
        public abstract void StopServer();
        public abstract void StartClient();
        public abstract void StopClient();
        public abstract void StopNetwork();
        public abstract void IterateIncoming();
        public abstract void IterateOutgoing();
        public abstract void SendDataToServer(byte[] data, ENetworkChannel channel = ENetworkChannel.ReliableOrdered);
        public abstract void SendDataToClient(int clientID, byte[] data, ENetworkChannel channel = ENetworkChannel.ReliableOrdered);
        public abstract void DisconnectClient(int clientID);
    }
    
    [Serializable]
    public class ConnectionData
    {
        public string Address = "127.0.0.1";
        public ushort Port = 24856;
        public string ServerListenAddress = string.Empty;
        
        public static NetworkEndpoint ParseNetworkEndpoint(string ip, ushort port)
        {
            if (NetworkEndpoint.TryParse(ip, port, out var endpoint) ||
                NetworkEndpoint.TryParse(ip, port, out endpoint, NetworkFamily.Ipv6)) return endpoint;

            return endpoint;
        }

        public virtual NetworkEndpoint ParseServerListenEndpoint()
        {
            if (!string.IsNullOrEmpty(ServerListenAddress))
            {
                return ParseNetworkEndpoint(ServerListenAddress, Port);
            }
            
            var endpoint = !string.IsNullOrEmpty(Address) &&
                           ParseNetworkEndpoint(Address, Port).Family == NetworkFamily.Ipv6
                ? NetworkEndpoint.LoopbackIpv6
                : NetworkEndpoint.LoopbackIpv4;
            return endpoint.WithPort(Port);
        }
    }
    
    public struct ServerReceivedData
    {
        public int ClientID;
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
        Starting,
        Started,
        Stopping,
        Stopped
    }

    public enum ERemoteConnectionState
    {
        Connected,
        Disconnected
    }
}
