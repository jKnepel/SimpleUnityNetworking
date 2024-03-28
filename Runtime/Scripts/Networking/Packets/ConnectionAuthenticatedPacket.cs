using jKnepel.SimpleUnityNetworking.Serialising;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionAuthenticatedPacket
    {
        public static byte PacketType => (byte)EPacketType.ConnectionAuthenticated;
        public uint ClientID;
        public string Servername;
        public int MaxNumberConnectedClients;

        public ConnectionAuthenticatedPacket(uint clientID, string servername, int maxNumberConnectedClients)
        {
            ClientID = clientID;
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
        }

        public static ConnectionAuthenticatedPacket Read(Reader reader)
        {
            var clientID = reader.ReadUInt32();
            var servername = reader.ReadString();
            var maxNumberConnectedClients = reader.ReadInt32();
            return new(clientID, servername, maxNumberConnectedClients);
        }

        public static void Write(Writer writer, ConnectionAuthenticatedPacket packet)
        {
            writer.WriteUInt32(packet.ClientID);
            writer.WriteString(packet.Servername);
            writer.WriteInt32(packet.MaxNumberConnectedClients);
        }
    }
}
