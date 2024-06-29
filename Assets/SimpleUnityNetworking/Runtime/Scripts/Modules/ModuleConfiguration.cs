using jKnepel.SimpleUnityNetworking.Managing;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Modules
{
    public abstract class ModuleConfiguration : ScriptableObject
    {
        public abstract Module GetModule(INetworkManager networkManager);
    }
}
