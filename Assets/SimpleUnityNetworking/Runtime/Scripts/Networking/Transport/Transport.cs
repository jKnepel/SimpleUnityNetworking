using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    public abstract class Transport
    {
        public abstract bool IsOnline { get; }
        public abstract bool IsServer { get; }
        public abstract bool IsClient { get; }
        public abstract bool IsHost { get; }
        
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
        public abstract void SendDataToServer(byte[] data);
        public abstract void SendDataToClient(int clientID, byte[] data);
        public abstract void DisconnectClient(int clientID);
    }
    
    [Serializable]
    public class ConnectionData
    {
        public string LocalAddress = "127.0.0.1";
        public ushort LocalPort = 0;
        public string Address = "127.0.0.1";
        public ushort Port = 0;
    }
    
    public class ServerReceivedData
    {
        public int ClientID;
        public byte[] Data;
        public DateTime Timestamp;
    }

    public class ClientReceivedData
    {
        public byte[] Data;
        public DateTime Timestamp;
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
