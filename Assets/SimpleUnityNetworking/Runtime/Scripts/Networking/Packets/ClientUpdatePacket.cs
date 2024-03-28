using jKnepel.SimpleUnityNetworking.Serialising;
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
		public Color32 Colour;

		public ClientUpdatePacket(uint clientID, UpdateType type, string username, Color32 colour)
		{
			ClientID = clientID;
			Type = type;
			Username = username;
			Colour = colour;
		}

		public ClientUpdatePacket(uint clientID)
		{
			ClientID = clientID;
			Type = UpdateType.Disconnected;
			Username = null;
			Colour = default;
		}

		public static ClientUpdatePacket Read(Reader reader)
		{
			uint clientID = reader.ReadUInt32();
			UpdateType type = (UpdateType)reader.ReadByte();
			string username = reader.ReadString();
			Color32 colour = reader.ReadColor32WithoutAlpha();
			return new(clientID, type, username, colour);
		}

		public static void Write(Writer writer, ClientUpdatePacket packet)
		{
			writer.WriteUInt32(packet.ClientID);
			writer.WriteByte((byte)packet.Type);
			writer.WriteString(packet.Username);
			writer.WriteColor32WithoutAlpha(packet.Colour);
		}
	}
}
