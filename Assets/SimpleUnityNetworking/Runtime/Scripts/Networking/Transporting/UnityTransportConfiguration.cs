using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking.Transporting
{
    [CreateAssetMenu(fileName = "UnityTransportConfiguration", menuName = "SimpleUnityNetworking/UnityTransportConfiguration")]
    public class UnityTransportConfiguration : TransportConfiguration
    {
        public UnityTransportConfiguration()
        {
            Settings = new();
        }
        
        public override string TransportName => "UnityTransport";
        public override Transport GetTransport()
        {
            return new UnityTransport(Settings);
        }
    }
}
