using jKnepel.SimpleUnityNetworking.Serialising;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct ServerUpdatePacket
	{
		public static byte PacketType => (byte)EPacketType.ServerUpdate;
		public readonly string Servername;

		public ServerUpdatePacket(string servername)
		{
			Servername = servername;
		}

		public static ServerUpdatePacket Read(Reader reader)
		{
			var servername = reader.ReadString();
			
			return new(servername);
		}

		public static void Write(Writer writer, ServerUpdatePacket packet)
		{
			writer.WriteString(packet.Servername);
		}
	}
}
