namespace jKnepel.SimpleUnityNetworking.Networking.Packets
{
    // each packet type can only go up to 16

    internal enum EPacketType : byte
    {   // connection packets
        ServerInformation = 0,
        ACK = 1,
        ChunkedACK = 2,
        ConnectionRequest = 3,
        ConnectionChallenge = 4,
        ChallengeAnswer = 5,
        ConnectionAccepted = 6,
        ConnectionDenied = 7,
        ConnectionClosed = 8,
        ClientDisconnected = 9,
        IsStillActive = 10,
        // sequenced packets
        ClientInfo = 16,
        Data = 17,
    }
}