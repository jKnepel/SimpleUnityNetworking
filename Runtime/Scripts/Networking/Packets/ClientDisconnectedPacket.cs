using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ClientDisconnectedPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ClientDisconnected;
        public byte ClientID;

        public ClientDisconnectedPacket(byte clientID)
		{
            ClientID = clientID;
		}

        public static ClientDisconnectedPacket ReadClientDisconnectedPacket(Reader reader)
		{
            byte clientID = reader.ReadByte();
            return new(clientID);
		}

        public static void WriteClientDisconnectedPacket(Writer writer, ClientDisconnectedPacket packet)
		{
            writer.WriteByte(packet.ClientID);
		}
    }
}
