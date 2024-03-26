using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager
    {
        private delegate void ByteDataCallback(uint senderID, byte[] data);
        private readonly ConcurrentDictionary<uint, Dictionary<int, ByteDataCallback>> _registeredByteDataCallbacks = new();

        public void RegisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
            {
                callbacks = new();
                _registeredByteDataCallbacks.TryAdd(byteDataHash, callbacks);
            }

            var key = callback.GetHashCode();
            var del = CreateByteDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
        }

        public void UnregisterByteData(string byteID, Action<uint, byte[]> callback)
        {
            var byteDataHash = Hashing.GetFNV1Hash32(byteID);

            if (!_registeredByteDataCallbacks.TryGetValue(byteDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
        public void SendByteDataToClient(uint clientID, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            SendByteDataToClients(new [] { clientID }, byteID, byteData, channel);
        }

        public void SendByteDataToAll(string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            SendByteDataToClients(Client_ConnectedClients.Keys.ToArray(), byteID, byteData, channel);
        }

        public void SendByteDataToClients(uint[] clientIDs, string byteID, byte[] byteData, 
            ENetworkChannel channel = ENetworkChannel.UnreliableUnordered)
        {
            if (!IsClient)
            {
                Logger?.Log("The local client must be started before data can be send!");
                return;
            }

            SendByteData(clientIDs, byteID, byteData, channel);
        }

        private void ReceiveByteData(uint byteID, uint clientID, byte[] data)
        {
            if (!_registeredByteDataCallbacks.TryGetValue(byteID, out var callbacks))
                return;

            foreach (var callback in callbacks.Values)
                callback?.Invoke(clientID, data);
        }
        
        private static ByteDataCallback CreateByteDataDelegate(Action<uint, byte[]> callback)
        {
            return ParseDelegate;
            void ParseDelegate(uint senderID, byte[] data)
            {
                callback?.Invoke(senderID, data);
            }
        }
    }
}
