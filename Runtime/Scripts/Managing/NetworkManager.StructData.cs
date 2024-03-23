using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public partial class NetworkManager
    {
        private delegate void StructDataCallback(uint senderID, byte[] data);
        private readonly ConcurrentDictionary<uint, Dictionary<int, StructDataCallback>> _registeredStructDataCallbacks = new();

        public void RegisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
        {
	        var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
			{
                callbacks = new();
                _registeredStructDataCallbacks.TryAdd(structDataHash, callbacks);
			}

			var key = callback.GetHashCode();
			var del = CreateStructDataDelegate(callback);
            if (!callbacks.ContainsKey(key))
                callbacks.TryAdd(key, del);
		}

        public void UnregisterStructData<T>(Action<uint, T> callback) where T : struct, IStructData
		{
			var structDataHash = Hashing.GetFNV1Hash32(typeof(T).Name);
            
            if (!_registeredStructDataCallbacks.TryGetValue(structDataHash, out var callbacks))
                return;

            callbacks.Remove(callback.GetHashCode(), out _);
        }
        
		public void SendStructDataToClient<T>(uint clientID, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
		{
			SendStructDataToClients(new [] { clientID }, structData, channel);
		}

		public void SendStructDataToAll<T>(T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
		{
			SendStructDataToClients(Client_ConnectedClients.Keys.ToArray(), structData, channel);
		}

		public void SendStructDataToClients<T>(uint[] clientIDs, T structData, 
			ENetworkChannel channel = ENetworkChannel.UnreliableUnordered) where T : struct, IStructData
		{
			if (!IsClient)
			{
				Messaging.DebugMessage("The local client must be started before data can be send!");
				return;
			}

			SendStructData(clientIDs, structData, channel);
		}
		
		private void ReceiveStructData(uint structHash, uint clientID, byte[] data)
		{
			if (!_registeredStructDataCallbacks.TryGetValue(structHash, out var callbacks))
				return;

			foreach (var callback in callbacks.Values)
			{
				callback?.Invoke(clientID, data);
			}
		}
		
		private StructDataCallback CreateStructDataDelegate<T>(Action<uint, T> callback)
		{
			return ParseDelegate;
			void ParseDelegate(uint senderID, byte[] data)
			{
				// TODO : somehow read data with generic before calling the delegate to prevent multiple reads
				Reader reader = new(data, _serialiserConfiguration);
				callback?.Invoke(senderID, reader.Read<T>());
			}
		}
    }
}
