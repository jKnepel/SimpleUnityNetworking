using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    [Serializable]
    [CreateAssetMenu(fileName = "UnityTransportConfiguration", menuName = "SimpleUnityNetworking/UnityTransportConfiguration")]
    public class UnityTransportConfiguration : TransportConfiguration
    {
        private UnityTransport _transport;
        public override Transport Transport => _transport;

        [SerializeField] private ConnectionData _connectionData;

        public UnityTransportConfiguration()
        {
            _connectionData = new();
            _transport = new UnityTransport(_connectionData);
        }
    }
}
