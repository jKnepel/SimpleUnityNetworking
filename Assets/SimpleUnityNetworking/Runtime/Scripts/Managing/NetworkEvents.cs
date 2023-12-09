using System;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class NetworkEvents
    {
        /// <summary>
        /// Action for when connection to or creation of a server is being started.
        /// </summary>
        public event Action OnConnecting;
        /// <summary>
        /// Action for when successfully connecting to or creating a Server.
        /// </summary>
        public event Action OnConnected;
        /// <summary>
        /// Action for when disconnecting from or closing the Server.
        /// </summary>
        public event Action OnDisconnected;
        /// <summary>
        /// Action for when the connection status of the local client was updated.
        /// </summary>
        public event Action OnConnectionStatusUpdated;
        /// <summary>
        /// Action for when the server the local client was connected to was closed.
        /// </summary>
        public event Action OnServerWasClosed;
        /// <summary>
        /// Action for when a remote Client connected to the current Server and can now receive Messages.
        /// </summary>
        public event Action<byte> OnClientConnected;
        /// <summary>
        /// Action for when a remote Client disconnected from the current Server and can no longer receive any Messages.
        /// </summary>
        public event Action<byte> OnClientDisconnected;
        /// <summary>
        /// Action for when a Client was added or removed from ConnectedClients.
        /// </summary>
        public event Action OnConnectedClientListUpdated;
        /// <summary>
        /// Action for when the Server Discovery was activated.
        /// </summary>
        public event Action OnServerDiscoveryActivated;
        /// <summary>
        /// Action for when the Server Discovery was deactivated.
        /// </summary>
        public event Action OnServerDiscoveryDeactivated;
        /// <summary>
        /// Action for when a Server was added or removed from the OpenServers.
        /// </summary>
        public event Action OnOpenServerListUpdated;
        /// <summary>
        /// Action for when a new Network Message was added.
        /// </summary>
        public event Action OnNetworkMessageAdded;

        public void FireOnConnecting() => OnConnecting?.Invoke();
        public void FireOnConnected() => OnConnected?.Invoke();
        public void FireOnDisconnected() => OnDisconnected?.Invoke();
        public void FireOnConnectionStatusUpdated() => OnConnectionStatusUpdated?.Invoke();
        public void FireOnServerWasClosed() => OnServerWasClosed?.Invoke();
        public void FireOnClientConnected(byte clientID) => OnClientConnected?.Invoke(clientID);
        public void FireOnClientDisconnected(byte clientID) => OnClientDisconnected?.Invoke(clientID);
        public void FireOnConnectedClientListUpdated() => OnConnectedClientListUpdated?.Invoke();
        public void FireOnServerDiscoveryActivated() => OnServerDiscoveryActivated?.Invoke();
        public void FireOnServerDiscoveryDeactivated() => OnServerDiscoveryDeactivated?.Invoke();
        public void FireOnOpenServerListUpdated() => OnOpenServerListUpdated?.Invoke();
        public void FireOnNetworkMessageAdded() => OnNetworkMessageAdded?.Invoke();
    }
}
