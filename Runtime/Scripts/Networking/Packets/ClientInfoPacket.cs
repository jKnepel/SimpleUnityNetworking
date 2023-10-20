using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct ClientInfoPacket : IDataPacket
	{
		public EPacketType PacketType => EPacketType.ClientInfo;
		public byte ClientID;
		public string Username;
		public Color32 Color;

		public ClientInfoPacket(byte clientID, string username, Color32 color)
		{
			ClientID = clientID;
			Username = username;
			Color = color;
		}

		public static ClientInfoPacket ReadClientInfoPacket(Reader reader)
		{
			byte clientID = reader.ReadByte();
			string username = reader.ReadString();
			Color32 color = reader.ReadColor32WithoutAlpha();
			return new(clientID, username, color);
		}

		public static void WriteClientInfoPacket(Writer writer, ClientInfoPacket packet)
		{
			writer.WriteByte(packet.ClientID);
			writer.WriteString(packet.Username);
			writer.WriteColor32WithoutAlpha(packet.Color);
		}
	}
}
