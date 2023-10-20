using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct DataPacket : IDataPacket
	{
		public EPacketType PacketType => EPacketType.Data;
		public bool IsStructData;
		public uint DataID;
		public byte ClientID;
		public byte[] Data;

		public DataPacket(bool isStructData, uint dataID, byte clientID, byte[] data)
		{
			IsStructData = isStructData;
			DataID = dataID;
			ClientID = clientID;
			Data = data;
		}

		public static DataPacket ReadDataPacket(Reader reader)
		{
			bool isStructData = reader.ReadBoolean();
			uint dataID = reader.ReadUInt32();
			byte clientID = reader.ReadByte();
			byte[] simpleData = reader.ReadRemainingBytes();
			return new(isStructData, dataID, clientID, simpleData);
		}

		public static void WriteDataPacket(Writer writer, DataPacket packet)
		{
			writer.WriteBoolean(packet.IsStructData);
			writer.WriteUInt32(packet.DataID);
			writer.WriteByte(packet.ClientID);
			writer.BlockCopy(ref packet.Data, 0, packet.Data.Length);
		}
	}
}
