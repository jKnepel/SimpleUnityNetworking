using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Modules;
using jKnepel.SimpleUnityNetworking.Networking.Transporting;
using jKnepel.SimpleUnityNetworking.Serialising;
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using Logger = jKnepel.SimpleUnityNetworking.Logging.Logger;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class MonoNetworkManager : MonoBehaviour, INetworkManager
    {
	    public Transport Transport => NetworkManager.Transport;
	    [SerializeField] private TransportConfiguration _cachedTransportConfiguration;
	    public TransportConfiguration TransportConfiguration
	    {
		    get => NetworkManager.TransportConfiguration;
		    set
		    {
			    if (NetworkManager.TransportConfiguration == value) return;
                NetworkManager.TransportConfiguration = _cachedTransportConfiguration = value;
			    
#if UNITY_EDITOR
				if (value != null)
				    EditorUtility.SetDirty(_cachedTransportConfiguration);
			    if (!EditorApplication.isPlaying)
					EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    [SerializeField] private SerialiserConfiguration _cachedSerialiserConfiguration;
	    public SerialiserConfiguration SerialiserConfiguration
	    {
		    get => NetworkManager.SerialiserConfiguration;
		    set
		    {
			    if (NetworkManager.SerialiserConfiguration == value) return;
			    NetworkManager.SerialiserConfiguration = _cachedSerialiserConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedSerialiserConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public Logger Logger => NetworkManager.Logger;
	    [SerializeField] private LoggerConfiguration _cachedLoggerConfiguration;
	    public LoggerConfiguration LoggerConfiguration
	    {
		    get => NetworkManager.LoggerConfiguration;
		    set
		    {
			    if (NetworkManager.LoggerConfiguration == value) return;
			    NetworkManager.LoggerConfiguration = _cachedLoggerConfiguration = value;

#if UNITY_EDITOR
                if (value != null)
                    EditorUtility.SetDirty(_cachedLoggerConfiguration);
                if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public Module Module => NetworkManager.Module;
	    [SerializeField] private ModuleConfiguration _cachedModuleConfiguration;
	    public ModuleConfiguration ModuleConfiguration
	    {
		    get => NetworkManager.ModuleConfiguration;
		    set
		    {
			    if (NetworkManager.ModuleConfiguration == value) return;
			    NetworkManager.ModuleConfiguration = _cachedModuleConfiguration = value;

#if UNITY_EDITOR
			    if (value != null)
				    EditorUtility.SetDirty(_cachedModuleConfiguration);
			    if (!EditorApplication.isPlaying)
				    EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
		    }
	    }

	    public Server Server => NetworkManager.Server;
	    public Client Client => NetworkManager.Client;

	    public bool IsServer => NetworkManager.IsServer;
	    public bool IsClient => NetworkManager.IsClient;
	    public bool IsOnline => NetworkManager.IsOnline;
	    public bool IsHost => NetworkManager.IsHost;
	    
	    public event Action OnTransportDisposed
	    {
		    add => NetworkManager.OnTransportDisposed += value;
		    remove => NetworkManager.OnTransportDisposed -= value;
	    }
	    public event Action<ServerReceivedData> OnServerReceivedData
	    {
		    add => NetworkManager.OnServerReceivedData += value;
		    remove => NetworkManager.OnServerReceivedData -= value;
	    }
	    public event Action<ClientReceivedData> OnClientReceivedData
	    {
		    add => NetworkManager.OnClientReceivedData += value;
		    remove => NetworkManager.OnClientReceivedData -= value;
	    }
	    public event Action<ELocalConnectionState> OnServerStateUpdated
	    {
		    add => NetworkManager.OnServerStateUpdated += value;
		    remove => NetworkManager.OnServerStateUpdated -= value;
	    }
	    public event Action<ELocalConnectionState> OnClientStateUpdated
	    {
		    add => NetworkManager.OnClientStateUpdated += value;
		    remove => NetworkManager.OnClientStateUpdated -= value;
	    }
	    public event Action<uint, ERemoteConnectionState> OnConnectionUpdated
	    {
		    add => NetworkManager.OnConnectionUpdated += value;
		    remove => NetworkManager.OnConnectionUpdated -= value;
	    }
	    public event Action<string, EMessageSeverity> OnTransportLogAdded
	    {
		    add => NetworkManager.OnTransportLogAdded += value;
		    remove => NetworkManager.OnTransportLogAdded -= value;
	    }
	    public event Action OnTickStarted
	    {
		    add => NetworkManager.OnTickStarted += value;
		    remove => NetworkManager.OnTickStarted -= value;
	    }
	    public event Action OnTickCompleted
	    {
		    add => NetworkManager.OnTickCompleted += value;
		    remove => NetworkManager.OnTickCompleted -= value;
	    }

	    private NetworkManager _networkManager;
	    /// <summary>
	    /// Instance of the internal network manager held by the scene context 
	    /// </summary>
	    public NetworkManager NetworkManager
	    {
		    get
		    {
			    if (_networkManager != null) return _networkManager;
			    _networkManager = new();
			    _networkManager.TransportConfiguration = _cachedTransportConfiguration;
			    _networkManager.SerialiserConfiguration = _cachedSerialiserConfiguration;
			    _networkManager.LoggerConfiguration = _cachedLoggerConfiguration;
			    _networkManager.ModuleConfiguration = _cachedModuleConfiguration;
			    return _networkManager;
		    }
		    private set => _networkManager = value;
	    }

	    public void Tick()
	    {
		    NetworkManager.Tick();
	    }

	    private void OnDestroy()
	    {
		    NetworkManager.Dispose();
		    NetworkManager = null;
	    }

	    public void StartServer()
	    {
#if UNITY_EDITOR
		    if (!EditorApplication.isPlaying) return;		    
#endif
		    NetworkManager.StartServer();
	    }

	    public void StopServer()
	    {
		    NetworkManager.StopServer();
	    }

	    public void StartClient()
	    {
#if UNITY_EDITOR
		    if (!EditorApplication.isPlaying) return;		    
#endif
		    NetworkManager.StartClient();
	    }

	    public void StopClient()
	    {
		    NetworkManager.StopClient();
	    }

	    public void StopNetwork()
	    {
		    NetworkManager.StopNetwork();
	    }
    }
}
