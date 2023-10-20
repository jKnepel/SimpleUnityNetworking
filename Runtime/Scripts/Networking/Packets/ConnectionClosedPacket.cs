using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionClosedPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ConnectionClosed;
        public ClosedReason Reason;

        public ConnectionClosedPacket(ClosedReason reason)
		{
            Reason = reason;
		}

        public static ConnectionClosedPacket ReadConnectionClosedPacket(Reader reader)
		{
            ClosedReason reason = (ClosedReason)reader.ReadByte();
            return new(reason);
		}

        public static void WriteConnectionClosedPacket(Writer writer, ConnectionClosedPacket packet)
		{
            writer.WriteByte((byte)packet.Reason);
		}
    }

    internal enum ClosedReason : byte
	{
        Unknown,
        ServerWasClosed,
        ClientDisconnected,
        FailedACK
        // TooManyPackets
	}
}
