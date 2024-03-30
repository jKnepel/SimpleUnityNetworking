using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking.Transporting
{
    [CreateAssetMenu(fileName = "UnityTransportConfiguration", menuName = "SimpleUnityNetworking/UnityTransportConfiguration")]
    public class UnityTransportConfiguration : TransportConfiguration
    {
        public UnityTransportConfiguration()
        {
            _settings = new();
            _transport = new UnityTransport(_settings);
        }
    }
}
