using jKnepel.SimpleUnityNetworking.Serialisation;
using System;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct DataPacket
	{
		public enum DataPacketType : byte
		{
			Forwarded,
			Target,
			Targets
		}
		
		public static byte PacketType => (byte)EPacketType.Data;
		public DataPacketType DataType;
		public uint? SenderID;
		public uint? TargetID;
		public uint[] TargetIDs;
		
		public bool IsStructData;
		public uint DataID;
		public byte[] Data;

		public DataPacket(DataPacketType dataType, uint id, bool isStructData, uint dataID, byte[] data)
		{
			DataType = dataType;
			if (dataType == DataPacketType.Forwarded)
			{
				SenderID = id;
				TargetID = null;
				TargetIDs = null;
			}
			else if (dataType == DataPacketType.Target)
			{
				SenderID = null;
				TargetID = id;
				TargetIDs = null;
			}
			else
			{
				throw new Exception("The constructed data packet is not valid!");
			}
			
			IsStructData = isStructData;
			DataID = dataID;
			Data = data;
		}
		
		public DataPacket(uint[] targetIDs, bool isStructData, uint dataID, byte[] data)
		{
			DataType = DataPacketType.Targets;
			SenderID = null;
			TargetID = null;
			TargetIDs = targetIDs;
			
			IsStructData = isStructData;
			DataID = dataID;
			Data = data;
		}

		public static DataPacket ReadDataPacket(Reader reader)
		{
			var dataType = (DataPacketType)reader.ReadByte();
			if (dataType == DataPacketType.Targets)
			{
				 var targetIDs = reader.ReadArray<uint>();
				 var isStructData = reader.ReadBoolean();
				 var dataID = reader.ReadUInt32();
				 var simpleData = reader.ReadRemainingBuffer();
				 return new(targetIDs, isStructData, dataID, simpleData);
			}
			else
			{
				var id = reader.ReadUInt32();
				var isStructData = reader.ReadBoolean();
				var dataID = reader.ReadUInt32();
				var simpleData = reader.ReadRemainingBuffer();
				return new(dataType, id, isStructData, dataID, simpleData);
			}
		}

		public static void WriteDataPacket(Writer writer, DataPacket packet)
		{
			writer.WriteByte((byte)packet.DataType);
			switch (packet.DataType)
			{
				case DataPacketType.Forwarded:
					// ReSharper disable once PossibleInvalidOperationException
					writer.WriteUInt32((uint)packet.SenderID);
					break;
				case DataPacketType.Target:
					// ReSharper disable once PossibleInvalidOperationException
					writer.WriteUInt32((uint)packet.TargetID);
					break;
				case DataPacketType.Targets:
					writer.WriteArray<uint>(packet.TargetIDs);
					break;
			}
			
			writer.WriteBoolean(packet.IsStructData);
			writer.WriteUInt32(packet.DataID);
			writer.BlockCopy(ref packet.Data, 0, packet.Data.Length);
		}
	}
}
