using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using jKnepel.SimpleUnityNetworking.Utilities;

namespace jKnepel.SimpleUnityNetworking.Networking.Sockets
{
    public abstract partial class ANetworkSocket
    {
        private delegate void ByteDataCallback(byte senderID, byte[] data);
        private readonly ConcurrentDictionary<uint, Dictionary<int, ByteDataCallback>> _registeredByteDataCallbacks = new();

        private ByteDataCallback CreateByteDataDelegate(Action<byte, byte[]> callback)
        {
            void ParseDelegate(byte senderID, byte[] data)
            {
                callback?.Invoke(senderID, data);
            }
            return ParseDelegate;
        }

        public void RegisterByteData(string id, Action<byte, byte[]> callback)
        {
            uint byteDataHash = Hashing.GetFNV1Hash32(id);

            if (!_registeredByteDataCallbacks.TryGetValue(byteDataHash, out Dictionary<int, ByteDataCallback> callbacks))
            {
                callbacks = new();
                _registeredByteDataCallbacks.TryAdd(byteDataHash, callbacks);
            }

            int key = callback.GetHashCode();
            ByteDataCallback del = CreateByteDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
        }

        public void UnregisterByteData(string id, Action<byte, byte[]> callback)
        {
            uint byteDataHash = Hashing.GetFNV1Hash32(id);

            if (!_registeredByteDataCallbacks.TryGetValue(byteDataHash, out Dictionary<int, ByteDataCallback> callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }

        internal void ReceiveByteData(uint byteID, byte clientID, byte[] data)
        {
            if (!_registeredByteDataCallbacks.TryGetValue(byteID, out Dictionary<int, ByteDataCallback> callbacks))
                return;

            foreach (ByteDataCallback callback in callbacks.Values)
                callback?.Invoke(clientID, data);
        }

        public abstract void SendByteData(byte receiverID, string id, byte[] data, ENetworkChannel networkChannel, Action<bool> onDataSend = null);
    }
}
