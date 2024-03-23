using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionAuthenticatedPacket
    {
        public static byte PacketType => (byte)EPacketType.ConnectionAuthenticated;
        public uint ClientID;
        public string Servername;
        public uint MaxNumberConnectedClients;

        public ConnectionAuthenticatedPacket(uint clientID, string servername, uint maxNumberConnectedClients)
        {
            ClientID = clientID;
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
        }

        public static ConnectionAuthenticatedPacket Read(Reader reader)
        {
            var clientID = reader.ReadUInt32();
            var servername = reader.ReadString();
            var maxNumberConnectedClients = reader.ReadUInt32();
            return new(clientID, servername, maxNumberConnectedClients);
        }

        public static void Write(Writer writer, ConnectionAuthenticatedPacket packet)
        {
            writer.WriteUInt32(packet.ClientID);
            writer.WriteString(packet.Servername);
            writer.WriteUInt32(packet.MaxNumberConnectedClients);
        }
    }
}
