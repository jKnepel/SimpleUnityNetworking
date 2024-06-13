using System.Net;
using jKnepel.SimpleUnityNetworking.Serialising;

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    internal struct ServerAnnouncePacket
    {
        public IPEndPoint EndPoint;
        public string Servername;
        public uint MaxNumberOfClients;
        public uint NumberOfClients;

        public ServerAnnouncePacket(IPEndPoint endpoint, string servername, uint maxNumberOfClients, uint numberOfClients)
        {
            EndPoint = endpoint;
            Servername = servername;
            MaxNumberOfClients = maxNumberOfClients;
            NumberOfClients = numberOfClients;
        }

        public static ServerAnnouncePacket Read(Reader reader)
        {
            var endpoint = reader.ReadIPEndpoint();
            var servername = reader.ReadString();
            var maxNumberOfClients = reader.ReadUInt32();
            var numberOfClients = reader.ReadUInt32();
            return new(endpoint, servername, maxNumberOfClients, numberOfClients);
        }

        public static void Write(Writer writer, ServerAnnouncePacket packet)
        {
            writer.WriteIPEndpoint(packet.EndPoint);
            writer.WriteString(packet.Servername);
            writer.WriteUInt32(packet.MaxNumberOfClients);
            writer.WriteUInt32(packet.NumberOfClients);
        }
    }
}
