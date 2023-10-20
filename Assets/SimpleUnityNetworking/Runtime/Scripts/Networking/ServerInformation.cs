
using System;
using System.Net;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking
{
    public class ServerInformation
    {
        public IPEndPoint Endpoint { get; private set; }
        public string Servername { get; private set; }
        public byte MaxNumberConnectedClients { get; private set; }
        public DateTime LastHeartbeat { get; set; }

        public ServerInformation(IPEndPoint endpoint, string servername, byte maxNumberConnectedClients)
        {
            Endpoint = endpoint;
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
            LastHeartbeat = DateTime.Now;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                ServerInformation server = (ServerInformation)obj;
                return server.Endpoint.Equals(Endpoint);
            }
        }

        public override int GetHashCode()
        {
            return Endpoint.GetHashCode();
        }

        [Serializable]
        private class StringRepresentation
        {
            [SerializeField] private string ip;
            [SerializeField] private int port;
            [SerializeField] private string servername;
            [SerializeField] private byte maxNumberConnectedClients;
            [SerializeField] private string lastHeartbeat;

            public IPEndPoint Endpoint => new(IPAddress.Parse(ip), port);
            public string Servername => servername;
            public byte MaxNumberConnectedClients => maxNumberConnectedClients;
            public DateTime LastHeartbeat => DateTime.Parse(lastHeartbeat);

            public StringRepresentation(IPEndPoint endpoint, string servername, byte maxNumberConnectedClients, DateTime lastHeartbeat)
            {
                this.ip = endpoint.Address.ToString();
                this.port = endpoint.Port;
                this.servername = servername;
                this.maxNumberConnectedClients = maxNumberConnectedClients;
                this.lastHeartbeat = lastHeartbeat.ToString();
            }

            public StringRepresentation(string ip, int port, string servername, byte maxNumberConnectedClients, string lastHeartbeat)
            {
                this.ip = ip;
                this.port = port;
                this.servername = servername;
                this.maxNumberConnectedClients = maxNumberConnectedClients;
                this.lastHeartbeat = lastHeartbeat;
            }
        }

        public string ToJson()
        {
            var jsonObject = new StringRepresentation(Endpoint, Servername, MaxNumberConnectedClients, LastHeartbeat);
            return JsonUtility.ToJson(jsonObject);
        }

        public static ServerInformation FromJson(string json)
        {
            var jsonObject = JsonUtility.FromJson<StringRepresentation>(json);
            return new ServerInformation(jsonObject.Endpoint, jsonObject.Servername, jsonObject.MaxNumberConnectedClients);
        }
    }
}
