using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ChunkedACKPacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ChunkedACK;
        public ushort Sequence;
        public ushort SliceNumber;

        public ChunkedACKPacket(ushort sequence, ushort sliceNumber)
        {
            Sequence = sequence;
            SliceNumber = sliceNumber;
        }

        public static ChunkedACKPacket ReadChunkedACKPacket(Reader reader)
        {
            ushort sequence = reader.ReadUInt16();
            ushort sliceNumber = reader.ReadUInt16();
            return new(sequence, sliceNumber);
        }

        public static void WriteChunkedACKPacket(Writer writer, ChunkedACKPacket packet)
        {
            writer.WriteUInt16(packet.Sequence);
            writer.WriteUInt16(packet.SliceNumber);
        }
    }
}
