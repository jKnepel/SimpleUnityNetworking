using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionDeniedPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ConnectionDenied;
        public DeniedReason Reason;

        public ConnectionDeniedPacket(DeniedReason reason)
		{
            Reason = reason;
		}

        public static ConnectionDeniedPacket ReadConnectionDeniedPacket(Reader reader)
		{
            DeniedReason reason = (DeniedReason)reader.ReadByte();
            return new(reason);
		}

        public static void WriteConnectionDeniedPacket(Writer writer, ConnectionDeniedPacket packet)
		{
            writer.WriteByte((byte)packet.Reason);
		}
    }

    internal enum DeniedReason : byte
	{
        Unknown,
        InvalidChallengeAnswer,
        NoSpace
	}
}
