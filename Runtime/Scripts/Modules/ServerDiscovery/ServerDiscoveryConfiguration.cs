using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    [CreateAssetMenu(fileName = "ServerDiscoveryConfiguration", menuName = "SimpleUnityNetworking/Modules/ServerDiscoveryConfiguration")]
    public class ServerDiscoveryConfiguration : ModuleConfiguration
    {
        public ServerDiscoverySettings Settings = new();
        
        public override Module GetModule(INetworkManager networkManager)
        {
            return new ServerDiscoveryModule(networkManager, Settings);
        }
    }
}
