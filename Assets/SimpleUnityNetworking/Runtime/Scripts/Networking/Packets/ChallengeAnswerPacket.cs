using jKnepel.SimpleUnityNetworking.Serialisation;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
	internal struct ChallengeAnswerPacket : IConnectionPacket
	{
		public EPacketType PacketType => EPacketType.ChallengeAnswer;
		public byte[] ChallengeAnswer;
		public string Username;
		public Color32 Color;

		public const int CHALLENGE_ANSWER_LENGTH = 32;

		public ChallengeAnswerPacket(byte[] challengeAnswer, string username, Color32 color)
		{
			ChallengeAnswer = challengeAnswer;
			Username = username;
			Color = color;
		}

		public static ChallengeAnswerPacket ReadChallengeAnswerPacket(Reader reader)
		{
			byte[] challengeAnswer = reader.ReadByteArray(CHALLENGE_ANSWER_LENGTH);
			string username = reader.ReadString();
			Color32 color = reader.ReadColor32WithoutAlpha();
			return new(challengeAnswer, username, color);
		}

		public static void WriteChallengeAnswerPacket(Writer writer, ChallengeAnswerPacket packet)
		{
			writer.BlockCopy(ref packet.ChallengeAnswer, 0, CHALLENGE_ANSWER_LENGTH);
			writer.WriteString(packet.Username);
			writer.WriteColor32WithoutAlpha(packet.Color);
		}
	}
}
