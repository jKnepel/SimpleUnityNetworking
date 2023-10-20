using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct PacketHeader
	{
		public bool IsConnectionPacket;
		public bool IsChunkedPacket;
		public ENetworkChannel NetworkChannel;
		public EPacketType PacketType;

		/// <summary>
		/// Constructor for Connection Packet Header
		/// </summary>
		/// <param name="packetType"></param>
		public PacketHeader(EPacketType packetType)
		{
			IsConnectionPacket = true;
			IsChunkedPacket = false;
			NetworkChannel = 0;
			PacketType = packetType;
		}

		/// <summary>
		/// Constructor for any Packet Header
		/// </summary>
		/// <param name="isConnectionPacket"></param>
		/// <param name="isChunkedPacket"></param>
		/// <param name="networkChannel"></param>
		/// <param name="packetType"></param>
		public PacketHeader(bool isConnectionPacket, bool isChunkedPacket, ENetworkChannel networkChannel, EPacketType packetType)
		{
			IsConnectionPacket = isConnectionPacket;
			IsChunkedPacket = isChunkedPacket;
			NetworkChannel = networkChannel;
			PacketType = packetType;
		}

		public static PacketHeader ReadPacketHeader(Reader reader)
		{
			byte headerByte = reader.ReadByte();
			int isConnectionBit = (headerByte & 0x80) >> 7;
			bool isConnectionPacket = isConnectionBit == 0;
			int isChunkedBit = (headerByte & 0x40) >> 6;
			bool isChunkedPacket = isChunkedBit == 1;
			int channelBits = (headerByte & 0x30) >> 4;
			ENetworkChannel networkChannel = (ENetworkChannel)channelBits;
			int typeBits = (headerByte & 0x0F) >> 0;
			typeBits += isConnectionPacket ? 0 : 16;
			EPacketType packetType = (EPacketType) typeBits;

			return new(isConnectionPacket, isChunkedPacket, networkChannel, packetType);
		}

		public static void WritePacketHeader(Writer writer, PacketHeader packetHeader)
		{
			byte isConnectionBit = (byte)(packetHeader.IsConnectionPacket ? 0 : 1);
			byte headerByte = (byte)(isConnectionBit << 7);
			byte isChunkedBit = (byte)(packetHeader.IsChunkedPacket ? 1 : 0);
			headerByte |= (byte)(isChunkedBit << 6);
			byte channelBits = (byte)packetHeader.NetworkChannel;
			headerByte |= (byte)(channelBits << 4);
			byte typeBits = (byte)((byte)packetHeader.PacketType & 0x0F);
			headerByte |= (byte)(typeBits << 0);

			writer.WriteByte(headerByte);
		}
	}
}
