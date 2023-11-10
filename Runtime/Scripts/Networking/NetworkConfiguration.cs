using System;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking
{
    [CreateAssetMenu(fileName = "NetworkConfiguration", menuName = "SimpleUnityNetworking/NetworkConfiguration")]
    public class NetworkConfiguration : ScriptableObject
    {
        private void OnEnable()
        {
            LocalIPAddresses = NetworkUtilities.GetLocalIPAddresses(_allowVirtualIPs);
            Messaging.ShowDebugMessages = _showDebugMessages;
        }

        internal const uint PROTOCOL_ID = 876237843;
        private static byte[] _protocolBytes;
        internal static byte[] ProtocolBytes
        {
            get
            {
                if (_protocolBytes != null)
                    return _protocolBytes;
                Writer writer = new();
                writer.WriteUInt32(PROTOCOL_ID);
                return _protocolBytes = writer.GetBuffer();
            }
        }

        #region user settings

        [SerializeField] private string _username = "Username";
        public string Username
        {
            get => _username;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Messaging.DebugMessage("The Username can't be empty or null!");
                    return;
                }

                if (value.Length > 100)
                {
                    Messaging.DebugMessage("The Username can't be longer than 100 Characters!");
                }

                if (Encoding.UTF8.GetByteCount(value) != value.Length)
                {
                    Messaging.DebugMessage("The Username must be in ASCII Encoding!");
                    return;
                }

                _username = value;
            }
        }

        [SerializeField] private Color32 _color = new(255, 255, 255, 255);
        public Color32 Color { get => _color; set => _color = value; }

        #endregion

        #region network configuration settings

        [SerializeField] private bool _allowVirtualIPs = false;
        public bool AllowVirtualIPs
        {
            get => _allowVirtualIPs;
            set
            {
                if (_allowVirtualIPs == value)
                    return;

                _allowVirtualIPs = value;
                LocalIPAddresses = NetworkUtilities.GetLocalIPAddresses(value);
            }
        }

        private string[] _localStringIPAddresses = new string[0];
        public string[] LocalStringIPAddresses { get => _localStringIPAddresses; set => _localStringIPAddresses = value; }

        private IPAddress[] _localIPAddresses = new IPAddress[0];
        public IPAddress[] LocalIPAddresses
        {
            get => _localIPAddresses;
            internal set
            {
                _localIPAddresses = value;
                LocalIPAddressIndex = Math.Min(LocalIPAddressIndex, LocalIPAddresses.Length - 1);
                LocalStringIPAddresses = value.Select(x => x.ToString()).ToArray();
            }
        }

        private int _localIPAddressIndex = 0;
        public int LocalIPAddressIndex
        {
            get => _localIPAddressIndex;
            set
            {
                if (_localIPAddressIndex != value && value < LocalIPAddresses.Length)
                    _localIPAddressIndex = value;
            }
        }

        private int _localPort = 0;
        public int LocalPort { get => _localPort; internal set => _localPort = value; }

        [SerializeField] private int _mtu = 1200;
        public int MTU { get => _mtu; set => _mtu = value; }

        [SerializeField] private int _rtt = 200;
        public int RTT { get => _rtt; set => _rtt = value; }

        [SerializeField] private int _serverConnectionTimeout = 5000;
        public int ServerConnectionTimeout { get => _serverConnectionTimeout; set => _serverConnectionTimeout = value; }

        [SerializeField] private int _serverHeartbeatDelay = 1000;
        public int ServerHeartbeatDelay { get => _serverHeartbeatDelay; set => _serverHeartbeatDelay = value; }

        [SerializeField] private int _serverDiscoveryTimeout = 3000;
        public int ServerDiscoveryTimeout { get => _serverDiscoveryTimeout; set => _serverDiscoveryTimeout = value; }

        [SerializeField] private int _maxNumberResendReliablePackets = 5;
        public int MaxNumberResendReliablePackets { get => _maxNumberResendReliablePackets; set => _maxNumberResendReliablePackets = value; }

        #endregion

        #region server discovery settings

        [SerializeField] private string _discoveryIP = "239.240.240.149";
        public string DiscoveryIP { get => _discoveryIP; set => _discoveryIP = value; }

        [SerializeField] private int _discoveryPort = 26824;
        public int DiscoveryPort
        {
            get => _discoveryPort;
            set
            {
                if (_discoveryPort == value)
                    return;

                if (value < IPEndPoint.MinPort || value > IPEndPoint.MaxPort)
                {
                    Messaging.DebugMessage("The given Port number is outside the accepted Range!");
                    return;
                }

                _discoveryPort = value;
            }
        }

        #endregion

        #region debug settings

        [SerializeField] private bool _showDebugMessages = true;
        public bool ShowDebugMessages
        {
            get => _showDebugMessages;
            set => Messaging.ShowDebugMessages = _showDebugMessages = value;
        }

        [SerializeField] private bool _allowLocalConnections = false;
        public bool AllowLocalConnections { get => _allowLocalConnections; set => _allowLocalConnections = value; }

        #endregion
    }
}
