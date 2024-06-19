using System;
using System.Collections.Generic;

namespace jKnepel.SimpleUnityNetworking.Modules
{
    [Serializable]
    public class ModuleList : List<Module>
    {
        private readonly INetworkManager _networkManager;

        public ModuleList(INetworkManager networkManager)
        {
            _networkManager = networkManager;
        }
        
        public void Add(ModuleConfiguration moduleConfiguration)
        {
            Add(moduleConfiguration.GetModule(_networkManager));
        }
    }
}
