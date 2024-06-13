using jKnepel.SimpleUnityNetworking.Managing;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jKnepel.SimpleUnityNetworking.Modules.ServerDiscovery
{
    public class ServerDiscoveryModule : Module
    {
        #region fields

        public override string Name => "ServerDiscovery";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive == value) return;

                _isActive = value;
                if (_isActive)
                    OnServerDiscoveryActivated?.Invoke();
                else
                    OnServerDiscoveryDeactivated?.Invoke();
            }
        }
        
        public List<ActiveServer> ActiveServers => _openServers.Values.ToList();

        public event Action OnServerDiscoveryActivated;
        public event Action OnServerDiscoveryDeactivated;
        public event Action OnActiveServerListUpdated;

        private INetworkManager _networkManager;
        private ServerDiscoverySettings _settings;
		private IPAddress _discoveryIP;
        private UdpClient _announceClient;
        private UdpClient _discoveryClient;
        private Thread _announceThread;
        private Thread _discoveryThread;
        private byte[] _protocolBytes;

        private readonly ConcurrentDictionary<IPEndPoint, ActiveServer> _openServers = new();

		#endregion

		#region public methods

        public ServerDiscoveryModule(INetworkManager networkManager, ServerDiscoverySettings settings)
        {
            _networkManager = networkManager;
            _settings = settings;
            _networkManager.Server_OnLocalStateUpdated += OnServerStateUpdated;
        }

        protected override void Dispose(bool disposing)
        {
            EndServerDiscovery();
            EndServerAnnouncement();
        }
        
        #endregion
        
        #region server discovery

        public bool StartServerDiscovery()
        {
            if (IsActive)
                return true;

            try
            {
                _discoveryIP = IPAddress.Parse(_settings.DiscoveryIP);
                
                Writer writer = new();
                writer.WriteUInt32(_settings.ProtocolID);
                _protocolBytes = writer.GetBuffer();

                _discoveryClient = new();
                _discoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                _discoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _discoveryClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                _discoveryClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_discoveryIP));
                _discoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                _discoveryThread = new(DiscoveryThread) { IsBackground = true };
                _discoveryThread.Start();

                return IsActive = true;
            }
            catch (Exception ex)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
                throw;
                switch (ex)
                {
                    case FormatException:
                        Debug.LogError("The server discovery multicast IP is not a valid address!");
                        break;
                    case ObjectDisposedException:
                    case SocketException:
                        Debug.LogError("An error occurred when attempting to access the socket!");
                        break;
                    case ThreadStartException:
                        Debug.LogError("An error occurred when starting the threads. Please try again later!");
                        break;
                    case OutOfMemoryException:
                        Debug.LogError("Not enough memory available to start the threads!");
                        break;
                    default:
                        ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                }
                
                return IsActive = false;
            }
        }

        public void EndServerDiscovery()
		{
            if (!IsActive)
                return;

            if (_discoveryClient != null)
            {
                _discoveryClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, new MulticastOption(_discoveryIP));
                _discoveryClient.Close();
                _discoveryClient.Dispose();
            }
            if (_discoveryThread != null)
            {
                _discoveryThread.Abort();
                _discoveryThread.Join();
            }

            IsActive = false;
        }

        public bool RestartServerDiscovery()
		{
            EndServerDiscovery();
            return StartServerDiscovery(); 
        }
        
        private void DiscoveryThread()
        {
            while (true)
            {
                try
                {
                    IPEndPoint remoteEP = new(IPAddress.Any, 0);
                    var receivedBytes = _discoveryClient.Receive(ref remoteEP);
                    Debug.Log("received");
                    Reader reader = new(receivedBytes);

                    // check crc32
                    var crc32 = reader.ReadUInt32();
                    var typePosition = reader.Position;
                    var bytesToHash = new byte[reader.Length];
                    Buffer.BlockCopy(_protocolBytes, 0, bytesToHash, 0, 4);
                    reader.BlockCopy(ref bytesToHash, 4, reader.Remaining);
                    if (crc32 != Hashing.GetCRC32Hash(bytesToHash))
                        continue;
                    
                    // read and update server
                    reader.Position = typePosition;
                    var packet = ServerAnnouncePacket.Read(reader);
                    ActiveServer newServer = new(packet.EndPoint, packet.Servername, packet.MaxNumberOfClients, packet.NumberOfClients);
                    if (!_openServers.TryGetValue(packet.EndPoint, out _))
                        _ = TimeoutServer(packet.EndPoint);
                    _openServers[packet.EndPoint] = newServer;

                    MainThreadQueue.Enqueue(() => OnActiveServerListUpdated?.Invoke());
                }
                catch (Exception ex)
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                    throw;
                    switch (ex)
                    {
                        case IndexOutOfRangeException:
                        case ArgumentException:
                            continue;
                        case SocketException:
                        case ThreadAbortException:
                            IsActive = false;
                            return;
                        default:
                            Debug.LogError("An error occurred in the server discovery!");
                            IsActive = false;
                            return;
                    }
                }
            }
        }

        private async Task TimeoutServer(IPEndPoint serverEndpoint)
        {
            await Task.Delay(_settings.ServerDiscoveryTimeout);
            if (_openServers.TryGetValue(serverEndpoint, out var server))
            {   // timeout and remove servers that haven't been updated for longer than the timeout value
                if ((DateTime.Now - server.LastHeartbeat).TotalMilliseconds > _settings.ServerDiscoveryTimeout)
                {
                    _openServers.TryRemove(serverEndpoint, out _);
                    MainThreadQueue.Enqueue(() => OnActiveServerListUpdated?.Invoke());
                    return;
                }

                _ = TimeoutServer(serverEndpoint);
            }
        }
        
        public void JoinActiveServer(ActiveServer server)
        {
            
        }
        
        #endregion
        
        #region server announce
        
        private void OnServerStateUpdated(ELocalServerConnectionState state)
        {
            switch (state)
            {
                case ELocalServerConnectionState.Started:
                    StartServerAnnouncement();
                    break;
                case ELocalServerConnectionState.Stopping:
                    EndServerAnnouncement();
                    break;
            }
        }
        
        private void StartServerAnnouncement()
        {
            try
            {
                _discoveryIP = IPAddress.Parse(_settings.DiscoveryIP);

                Writer writer = new();
                writer.WriteUInt32(_settings.ProtocolID);
                _protocolBytes = writer.GetBuffer();
                
                _announceClient = new();
                _announceClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                _announceClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _announceClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                _announceClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_discoveryIP));
                _announceClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                _announceClient.Connect(new(_discoveryIP, _settings.DiscoveryPort));

                _announceThread = new(AnnounceThread) { IsBackground = true };
                _announceThread.Start();
            }
            catch (Exception ex)
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
                throw;
                switch (ex)
                {
                    case FormatException:
                        Debug.LogError("The server discovery multicast IP is not a valid address!");
                        break;
                    case ObjectDisposedException:
                    case SocketException:
                        Debug.LogError("An error occurred when attempting to access the socket!");
                        break;
                    case ThreadStartException:
                        Debug.LogError("An error occurred when starting the threads. Please try again later!");
                        break;
                    case OutOfMemoryException:
                        Debug.LogError("Not enough memory available to start the threads!");
                        break;
                    default:
                        ExceptionDispatchInfo.Capture(ex).Throw();
                        throw;
                }
            }
        }

        private void EndServerAnnouncement()
        {
            if (_announceClient != null)
            {
                _announceClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, new MulticastOption(_discoveryIP));
                _announceClient.Close();
                _announceClient.Dispose();
            }
            if (_announceThread != null)
            {
                _announceThread.Abort();
                _announceThread.Join();
            }
        }

        private void AnnounceThread()
        {
            while (true)
            {
                try
                {
                    // TODO : optimise this
                    Writer writer = new();
                    writer.Skip(4);
                    ServerAnnouncePacket.Write(writer, new(
                        _networkManager.Server_ServerEndpoint,
                        _networkManager.Server_Servername,
                        _networkManager.Server_MaxNumberOfClients, 
                        (uint)_networkManager.Server_ConnectedClients.Count
                    ));

                    var bytesToHash = new byte[writer.Length];
                    Buffer.BlockCopy(_protocolBytes, 0, bytesToHash, 0, 4);
                    Buffer.BlockCopy(writer.GetBuffer(), 4, bytesToHash, 4, bytesToHash.Length - 4);
                    writer.Position = 0;
                    writer.WriteUInt32(Hashing.GetCRC32Hash(bytesToHash));

                    Debug.Log(_announceClient.Send(writer.GetBuffer(), writer.Length));
                    Thread.Sleep(_settings.ServerHeartbeatDelay);
                }
                catch (Exception ex)
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                    throw;
                    switch (ex)
                    {
                        case IndexOutOfRangeException:
                        case ArgumentException:
                            continue;
                        case ThreadAbortException:
                            return;
                        default:
                            ExceptionDispatchInfo.Capture(ex).Throw();
                            throw;
                    }
                }
            }
        }
        
        #endregion

		#region utilities
        
#if UNITY_EDITOR
        private Texture2D _texture;
        private Texture2D Texture
        {
            get
            {
                if (_texture == null)
                    _texture = new(1, 1);
                return _texture;
            }
        }
        
        private readonly Color[] _scrollViewColors = { new(0.25f, 0.25f, 0.25f), new(0.23f, 0.23f, 0.23f) };
        private const float ROW_HEIGHT = 20;
        
        public override void ModuleGUI()
        {
            if (GUILayout.Button("Start Server Discovery"))
                StartServerDiscovery();
            if (GUILayout.Button("Stop Server Discovery"))
                EndServerDiscovery();
            EditorGUILayout.Toggle("Is Discovery Active:", IsActive);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Server Discovery", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Open Servers Count: {ActiveServers?.Count}");
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < ActiveServers?.Count; i++)
            {
                var server = ActiveServers[i];
                EditorGUILayout.BeginHorizontal(GetScrollviewRowStyle(_scrollViewColors[i % 2]));
                {
                    GUILayout.Label(server.Servername);
                    GUILayout.Label($"#{server.NumberConnectedClients}/{server.MaxNumberConnectedClients}");
                    if (GUILayout.Button(new GUIContent("Connect To Server"), GUILayout.ExpandWidth(false)))
                        JoinActiveServer(server);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private GUIStyle GetScrollviewRowStyle(Color color)
        {
            Texture.SetPixel(0, 0, color);
            Texture.Apply();
            GUIStyle style = new();
            style.normal.background = Texture;
            style.fixedHeight = ROW_HEIGHT;
            return style;
        }
#endif
        
        #endregion
    }
}
