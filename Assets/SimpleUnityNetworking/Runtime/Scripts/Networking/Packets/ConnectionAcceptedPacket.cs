using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionAcceptedPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ConnectionAccepted;
        public byte ClientID;
        public string Servername;
        public byte MaxNumberConnectedClients;

        public ConnectionAcceptedPacket(byte clientID, string servername, byte maxNumberConnectedClients)
        {
            ClientID = clientID;
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
        }

        public static ConnectionAcceptedPacket ReadConnectionAcceptedPacket(Reader reader)
        {
            byte clientID = reader.ReadByte();
            string servername = reader.ReadString();
            byte maxNumberConnectedClients = reader.ReadByte();
            return new(clientID, servername, maxNumberConnectedClients);
        }

        public static void WriteConnectionAcceptedPacket(Writer writer, ConnectionAcceptedPacket packet)
        {
            writer.WriteByte(packet.ClientID);
            writer.WriteString(packet.Servername);
            writer.WriteByte(packet.MaxNumberConnectedClients);
        }
    }
}
