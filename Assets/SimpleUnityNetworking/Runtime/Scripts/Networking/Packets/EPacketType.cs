namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    internal enum EPacketType : byte
    {   
        ConnectionChallenge = 1,
        ChallengeAnswer = 2,
        ConnectionAuthenticated = 3,
        ClientUpdate = 4,
        Data = 5,
    }
}