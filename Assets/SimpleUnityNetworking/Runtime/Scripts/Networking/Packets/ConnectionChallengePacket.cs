using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionChallengePacket : IConnectionPacket
    {
        public EPacketType PacketType => EPacketType.ConnectionChallenge;
        public ulong Challenge;

        public ConnectionChallengePacket(ulong challenge)
		{
            Challenge = challenge;
		}

        public static ConnectionChallengePacket ReadConnectionChallengePacket(Reader reader)
		{
            ulong challenge = reader.ReadUInt64();
            return new(challenge);
		}

        public static void WriteConnectionChallengePacket(Writer writer, ConnectionChallengePacket packet)
		{
            writer.WriteUInt64(packet.Challenge);
		}
    }
}
