using jKnepel.SimpleUnityNetworking.Managing;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    [CreateAssetMenu(fileName = "ServerDiscoveryConfiguration", menuName = "SimpleUnityNetworking/Modules/ServerDiscoveryConfiguration")]
    public class ServerDiscoveryConfiguration : ModuleConfiguration
    {
        public override Module GetModule(INetworkManager networkManager) 
            => new ServerDiscoveryModule(networkManager, this, Settings);
        
        public ServerDiscoverySettings Settings = new();
    }
}
