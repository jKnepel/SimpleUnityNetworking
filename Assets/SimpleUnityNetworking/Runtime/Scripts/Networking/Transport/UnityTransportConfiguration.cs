using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    [CreateAssetMenu(fileName = "UnityTransportConfiguration", menuName = "SimpleUnityNetworking/UnityTransportConfiguration")]
    public class UnityTransportConfiguration : TransportConfiguration
    {
        public UnityTransportConfiguration() 
            : base(new UnityTransport(), new()) { }
    }
}
