namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal interface INetworkPacket
	{
		public EPacketType PacketType { get; }
	}
}