using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal struct ConnectionChallengePacket
    {
        public static byte PacketType => (byte)EPacketType.ConnectionChallenge;
        public ulong Challenge;

        public ConnectionChallengePacket(ulong challenge)
		{
            Challenge = challenge;
		}

        public static ConnectionChallengePacket Deserialise(Reader reader)
		{
            ulong challenge = reader.ReadUInt64();
            return new(challenge);
		}

        public static void Serialise(Writer writer, ConnectionChallengePacket packet)
		{
            writer.WriteUInt64(packet.Challenge);
		}
    }
}
