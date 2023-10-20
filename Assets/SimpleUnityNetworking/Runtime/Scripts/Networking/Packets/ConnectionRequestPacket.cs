using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionRequestPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ConnectionRequest;

        public static ConnectionRequestPacket ReadConnectionRequestPacket(Reader reader)
		{
            reader.ReadByte();
            return new();
		}

        public static void WriteConnectionRequestPacket(Writer writer, ConnectionRequestPacket packet)
		{
            writer.WriteByte(0);
		}
    }
}
