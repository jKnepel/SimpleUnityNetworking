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

        [SerializeField] private TransportSettings _settings;

        public UnityTransportConfiguration()
        {
            _settings = new();
            _transport = new UnityTransport(_settings);
        }
    }
}
