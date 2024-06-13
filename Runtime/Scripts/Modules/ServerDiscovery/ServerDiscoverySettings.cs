using System;

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    [Serializable]
    public class ServerDiscoverySettings
    {
        public uint ProtocolID = 876237843;
        public string DiscoveryIP = "239.240.240.149";
        public ushort DiscoveryPort = 24857;
        public int ServerDiscoveryTimeout = 3000;
        public int ServerHeartbeatDelay = 500;
    }
}
