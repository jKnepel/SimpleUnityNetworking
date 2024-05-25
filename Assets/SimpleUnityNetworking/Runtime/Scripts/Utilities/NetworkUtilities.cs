using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace jKnepel.SimpleUnityNetworking.Utilities
{
    internal static class NetworkUtilities
    {
        public static ushort FindNextAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        }

        /// <summary>
        /// Fetch all local IPv4 addresses.
        /// See for <a href="https://stackoverflow.com/a/24814027">reference</a>. 
        /// </summary>
        /// <param name="allowVirtualIPs"></param>
        /// <returns></returns>
        public static IPAddress[] GetLocalIPAddresses(bool allowVirtualIPs = false)
        {   
            List<IPAddress> ipAddresses = new();
            foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus != OperationalStatus.Up) continue; 
                // Fetch the properties of this adapter
                var adapterProperties = item.GetIPProperties();
                // Check if the gateway address exist, if not its most likely a virtual network or smth
                if (!allowVirtualIPs && adapterProperties.GatewayAddresses.FirstOrDefault() == null) continue; 
                // Iterate over each available unicast addresses
                foreach (var ip in adapterProperties.UnicastAddresses)
                {
                    // If the IP is a local IPv4 address
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    ipAddresses.Add(ip.Address);
                    break;
                }
            }
            return ipAddresses.ToArray();
        }

        public static bool CheckLocalIPAddress(IPAddress address)
		{
            var inter = NetworkInterface
                .GetAllNetworkInterfaces()
                .First(x => x
                    .GetIPProperties()
                    .UnicastAddresses
                    .Any(i => i.Address.Equals(address))
                );
            return inter.OperationalStatus == OperationalStatus.Up;
		}
    }
}
