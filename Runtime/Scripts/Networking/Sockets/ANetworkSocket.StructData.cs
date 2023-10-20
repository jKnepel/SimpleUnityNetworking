using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Utilities;

namespace jKnepel.SimpleUnityNetworking.Networking.Sockets
{
    public abstract partial class ANetworkSocket
    {
        private delegate void StructDataCallback(byte senderID, Reader reader);
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredStructDataCallbacks = new();

        private StructDataCallback CreateStructDataDelegate<T>(Action<byte, T> callback)
		{
            void ParseDelegate(byte senderID, Reader reader)
            {
                callback?.Invoke(senderID, reader.Read<T>());
            }
            return ParseDelegate;
        }

        public void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            uint structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredStructDataCallbacks.TryGetValue(structDataHash, out Dictionary<int, StructDataCallback> callbacks))
			{
                callbacks = new();
                _registeredStructDataCallbacks.TryAdd(structDataHash, callbacks);
			}

            int key = callback.GetHashCode();
            StructDataCallback del = CreateStructDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
		}

        public void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
		{
            uint structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredStructDataCallbacks.TryGetValue(structDataHash, out Dictionary<int, StructDataCallback> callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }

        internal protected void ReceiveStructData(uint structHash, byte clientID, byte[] data)
		{
            if (!_registeredStructDataCallbacks.TryGetValue(structHash, out Dictionary<int, StructDataCallback> callbacks))
                return;

            Reader reader = new(data);
            int position = reader.Position;
            foreach (StructDataCallback callback in callbacks.Values)
			{
                callback?.Invoke(clientID, reader);
                // TODO : somehow read data with generic before calling the delegate to prevent multiple reads
                reader.Position = position;
			}
		}

        public abstract void SendStructData<T>(byte receiverID, T StructData, ENetworkChannel networkChannel, Action<bool> onDataSend = null) where T : struct, IStructData;
    }
}
