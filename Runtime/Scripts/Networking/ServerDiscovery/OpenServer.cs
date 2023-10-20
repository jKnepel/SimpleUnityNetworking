using System;
using System.Net;

namespace jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery
{
    public class OpenServer
    {
        public IPEndPoint Endpoint { get; private set; }
        public string Servername { get; private set; }
        public byte MaxNumberConnectedClients { get; private set; }
        public byte NumberConnectedClients { get; private set; }
        public bool IsServerFull => NumberConnectedClients >= MaxNumberConnectedClients;
        public DateTime LastHeartbeat { get; set; }

        public OpenServer(IPEndPoint endpoint, string servername, byte maxNumberConnectedClients, byte numberConnectedClients)
        {
            Endpoint = endpoint;
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
            NumberConnectedClients = numberConnectedClients;
            LastHeartbeat = DateTime.Now;
        }
    }
}
