using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct ServerInformationPacket : IConnectionPacket
	{
		public EPacketType PacketType => EPacketType.ServerInformation;
		public string Servername;
		public byte MaxNumberOfClients;
		public byte NumberOfClients;

		public ServerInformationPacket(string servername, byte maxNumberOfClients, byte numberOfClients)
		{
			Servername = servername;
			MaxNumberOfClients = maxNumberOfClients;
			NumberOfClients = numberOfClients;
		}

		public static ServerInformationPacket ReadServerInformationPacket(Reader reader)
		{
			string servername = reader.ReadString();
			byte maxNumberOfClients = reader.ReadByte();
			byte numberOfClients = reader.ReadByte();
			return new(servername, maxNumberOfClients, numberOfClients);
		}

		public static void WriteServerInformationPacket(Writer writer, ServerInformationPacket packet)
		{
			writer.WriteString(packet.Servername);
			writer.WriteByte(packet.MaxNumberOfClients);
			writer.WriteByte(packet.NumberOfClients);
		}
	}
}
