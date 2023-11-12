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
        private delegate void StructDataCallback(byte senderID, byte[] data);
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredStructDataCallbacks = new();

        private StructDataCallback CreateStructDataDelegate<T>(Action<byte, T> callback)
		{
            void ParseDelegate(byte senderID, byte[] data)
            {
				// TODO : somehow read data with generic before calling the delegate to prevent multiple reads
				if (NetworkConfiguration.SerialiserConfiguration.CompressFloats
				|| NetworkConfiguration.SerialiserConfiguration.CompressQuaternions)
                {
                    BitReader reader = new(data, NetworkConfiguration.SerialiserConfiguration);
					callback?.Invoke(senderID, reader.Read<T>());
                }
				else
                {
                    Reader reader = new(data, NetworkConfiguration.SerialiserConfiguration);
					callback?.Invoke(senderID, reader.Read<T>());
                }
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

            foreach (StructDataCallback callback in callbacks.Values)
			{
                callback?.Invoke(clientID, data);
			}
		}

        public abstract void SendStructData<T>(byte receiverID, T StructData, ENetworkChannel networkChannel, Action<bool> onDataSend = null) where T : struct, IStructData;
    }
}
