using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct ClientUpdatePacket
	{
		public enum UpdateType : byte
		{
			Connected,
			Disconnected,
			Updated
		}
		
		public static byte PacketType => (byte)EPacketType.ClientUpdate;
		public uint ClientID;
		public UpdateType Type;
		public string Username;
		public Color32 Color;

		public ClientUpdatePacket(uint clientID, UpdateType type, string username, Color32 color)
		{
			ClientID = clientID;
			Type = type;
			Username = username;
			Color = color;
		}

		public ClientUpdatePacket(uint clientID)
		{
			ClientID = clientID;
			Type = UpdateType.Disconnected;
			Username = null;
			Color = default;
		}

		public static ClientUpdatePacket Deserialise(Reader reader)
		{
			uint clientID = reader.ReadUInt32();
			UpdateType type = (UpdateType)reader.ReadByte();
			string username = reader.ReadString();
			Color32 color = reader.ReadColor32WithoutAlpha();
			return new(clientID, type, username, color);
		}

		public static void Serialise(Writer writer, ClientUpdatePacket packet)
		{
			writer.WriteUInt32(packet.ClientID);
			writer.WriteByte((byte)packet.Type);
			writer.WriteString(packet.Username);
			writer.WriteColor32WithoutAlpha(packet.Color);
		}
	}
}
