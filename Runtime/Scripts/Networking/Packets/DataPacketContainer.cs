using System;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct SequencedPacketContainer
	{
		public byte ReceiverID;
		public byte ExemptIDs; // TODO : change to list
		public ENetworkChannel NetworkChannel;
		public EPacketType PacketType;
		public byte[] Body;
		public Action<bool> OnPacketSend;

		public SequencedPacketContainer(byte receiverID, ENetworkChannel networkChannel, EPacketType packetType, 
			byte[] body, byte exemptIDs, Action<bool> onPacketSend = null)
		{
			ReceiverID = receiverID;
			ExemptIDs = exemptIDs;
			NetworkChannel = networkChannel;
			PacketType = packetType;
			Body = body;
			OnPacketSend = onPacketSend;
		}

		public SequencedPacketContainer(byte receiverID, ENetworkChannel networkChannel, EPacketType packetType,
			byte[] body, Action<bool> onPacketSend = null)
			: this(receiverID, networkChannel, packetType, body, 0, onPacketSend) { }
	}
}
