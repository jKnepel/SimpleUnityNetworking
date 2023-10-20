using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace jKnepel.SimpleUnityNetworking.Utilities
{
    internal static class NetworkUtilities
    {
        public static int FindNextAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        public static IPAddress[] GetLocalIPAddresses(bool allowVirtualIPs = false)
        {
            // taken from https://stackoverflow.com/a/24814027
            List<IPAddress> ipAddresses = new();
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus == OperationalStatus.Up)
                {   // Fetch the properties of this adapter
                    IPInterfaceProperties adapterProperties = item.GetIPProperties();
                    // Check if the gateway adress exist, if not its most likley a virtual network or smth
                    if (allowVirtualIPs || adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                    {   // Iterate over each available unicast adresses
                        foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                        {   // If the IP is a local IPv4 adress
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                ipAddresses.Add(ip.Address);
                                break;
                            }
                        }
                    }
                }
            }
            return ipAddresses.ToArray();
        }

        public static bool CheckLocalIPAddress(IPAddress address)
		{
            NetworkInterface inter = NetworkInterface.GetAllNetworkInterfaces().First(x => x.GetIPProperties().UnicastAddresses.Any(i => i.Address.Equals(address)));
            return inter != null && inter.OperationalStatus == OperationalStatus.Up;
		}
    }
}
