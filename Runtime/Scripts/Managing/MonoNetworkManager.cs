using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
        #region public members

        [SerializeField] private NetworkConfiguration _cachedNetworkConfiguration = null;
        public NetworkConfiguration NetworkConfiguration
        {
            get => _cachedNetworkConfiguration;
            set
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"Can not change {nameof(NetworkConfiguration)} when in play mode.");
                    return;
                }

                if (_cachedNetworkConfiguration != value)
                {
                    _cachedNetworkConfiguration = value;
                    NetworkManager.NetworkConfiguration = _cachedNetworkConfiguration;

#if UNITY_EDITOR
                    // This is needed for changes inside prefabs
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    EditorUtility.SetDirty(_cachedNetworkConfiguration);
#endif
                }
            }
        }
        public NetworkEvents Events => NetworkManager.Events;

        public bool IsConnected => NetworkManager.IsConnected;
        public bool IsHost => NetworkManager.IsHost;
        public EConnectionStatus ConnectionStatus => NetworkManager.ConnectionStatus;
        public ServerInformation ServerInformation => NetworkManager.ServerInformation;
        public ClientInformation ClientInformation => NetworkManager.ClientInformation;
        public ConcurrentDictionary<byte, ClientInformation> ConnectedClients => NetworkManager.ConnectedClients;
        public byte NumberConnectedClients => NetworkManager.NumberConnectedClients;

        public bool IsServerDiscoveryActive => NetworkManager.IsServerDiscoveryActive;        
        public List<OpenServer> OpenServers => NetworkManager.OpenServers;

        #endregion

        #region private members

        [SerializeField] private NetworkManager _networkManager;

        public NetworkManager NetworkManager
        {
            get
            {
                if (_networkManager == null)
                {
                    _networkManager = new(false);
                    _networkManager.NetworkConfiguration = NetworkConfiguration;
                }
                return _networkManager;
            }
        }

        #endregion

        #region lifecycle

        private void Start()
        {
            StartServerDiscovery();
        }

        private void OnDestroy()
        {
            NetworkManager.Dispose();
        }

        #endregion

        #region public methods

        public void CreateServer(string servername, byte maxNumberClients, Action<bool> onConnectionEstablished = null)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Can not create server with mono network manager while in edit mode.");
                return;
            }
            
            NetworkManager.CreateServer(servername, maxNumberClients, onConnectionEstablished);
        }

        public void JoinServer(IPAddress serverIP, int serverPort, Action<bool> onConnectionEstablished = null)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Can not join server with mono network manager while in edit mode.");
                return;
            }
            
            NetworkManager.JoinServer(serverIP, serverPort, onConnectionEstablished);
        }
        
        public void DisconnectFromServer()
        {
            NetworkManager.DisconnectFromServer();
        }

        public void StartServerDiscovery()
        {
            if (!Application.isPlaying) return;

            NetworkManager.StartServerDiscovery();
        }

        public void EndServerDiscovery()
        {
            NetworkManager.EndServerDiscovery();
        }

        public void RestartServerDiscovery()
        {
            NetworkManager.RestartServerDiscovery();
        }

        public void RegisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            NetworkManager.RegisterStructData(callback);
        }

        public void UnregisterStructData<T>(Action<byte, T> callback) where T : struct, IStructData
        {
            NetworkManager.UnregisterStructData(callback);
        }

        public void RegisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            NetworkManager.RegisterByteData(dataID, callback);
        }

        public void UnregisterByteData(string dataID, Action<byte, byte[]> callback)
        {
            NetworkManager.UnregisterByteData(dataID, callback);
        }

        public void SendStructDataToAll<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            NetworkManager.SendStructDataToAll(structData, networkChannel, onDataSend);
        }

        public void SendStructDataToServer<T>(T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            NetworkManager.SendStructDataToServer(structData, networkChannel, onDataSend);
        }

        public void SendStructData<T>(byte receiverID, T structData, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null) where T : struct, IStructData
        {
            NetworkManager.SendStructData(receiverID, structData, networkChannel, onDataSend);
        }

        public void SendByteDataToAll(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            NetworkManager.SendByteDataToAll(dataID, data, networkChannel, onDataSend);
        }

        public void SendByteDataToServer(string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            NetworkManager.SendByteDataToServer(dataID, data, networkChannel, onDataSend);
        }

        public void SendByteData(byte receiverID, string dataID, byte[] data, ENetworkChannel networkChannel = ENetworkChannel.ReliableOrdered,
            Action<bool> onDataSend = null)
        {
            NetworkManager.SendByteData(receiverID, dataID, data, networkChannel, onDataSend);
        }

		#endregion
	}
}
