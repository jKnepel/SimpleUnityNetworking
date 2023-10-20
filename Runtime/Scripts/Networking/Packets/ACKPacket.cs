using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ACKPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ACK;
        public ushort Sequence;

        public ACKPacket(ushort sequence)
		{
            Sequence = sequence;
        }

        public static ACKPacket ReadACKPacket(Reader reader)
		{
            ushort sequence = reader.ReadUInt16();
            return new(sequence);
		}

        public static void WriteACKPacket(Writer writer, ACKPacket packet)
		{
            writer.WriteUInt16(packet.Sequence);
		}
    }
}
