using jKnepel.SimpleUnityNetworking.Modules;
using jKnepel.SimpleUnityNetworking.Utilities;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    internal class INetworkManagerEditor
    {
        public enum EAllowStart
        {
            Anywhere,
            OnlyEditor,
            OnlyPlaymode
        }

        #region fields

        private readonly INetworkManager _manager;
        private readonly EAllowStart _allowStart;

        private readonly GUIStyle _style = new();
        
        private bool _showServerWindow = true;
        private Vector2 _serverClientsViewPos;

        private bool _showClientWindow = true;
        private Vector2 _clientClientsViewPos;

        #endregion

        #region lifecycle

        public INetworkManagerEditor(INetworkManager manager, EAllowStart allowStart)
        {
            _manager = manager;
            _allowStart = allowStart;
        }
        
        public void ManagerGUIs()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Managers:", EditorStyles.boldLabel);
            {
                ServerGUI();
                ClientGUI();
            }
        }

        public T ConfigurationGUI<T>(ScriptableObject configuration, string title, ref bool showSection) where T : ScriptableObject
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            UnityUtilities.DrawToggleFoldout(title, ref showSection);
            if (showSection)
            {
                configuration = (T)EditorGUILayout.ObjectField("Asset:", configuration, typeof(T), false);

                if (configuration)
                    Editor.CreateEditor(configuration).OnInspectorGUI();
            }
            GUILayout.EndVertical();

            return configuration as T;
        }

        #endregion

        #region guis
        
        private void ServerGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            UnityUtilities.DrawToggleFoldout("Server", ref _showServerWindow, _manager.IsServer, "Is Server:");
            if (_showServerWindow)
            {
                if (!_manager.IsServer)
                {
                    _manager.Server.Servername = EditorGUILayout.TextField(new GUIContent("Servername:"), _manager.Server.Servername);
                    if (GUILayout.Button(new GUIContent("Start Server")) && AllowStart())
                        _manager.StartServer();
                }
                else
                {
                    _manager.Server.Servername = EditorGUILayout.TextField("Servername:", _manager.Server.Servername);
                    EditorGUILayout.TextField("Connected Clients:", $"{_manager.Server.NumberOfConnectedClients}/{_manager.Server.MaxNumberOfClients}");
                    if (GUILayout.Button(new GUIContent("Stop Server")))
                        _manager.StopServer();

                    _serverClientsViewPos = EditorGUILayout.BeginScrollView(_serverClientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
                    if (_manager.Server.NumberOfConnectedClients == 0)
                    {
                        GUILayout.Label($"There are no clients connected to the local server!");
                    }
                    else
                    {
                        var defaultColour = _style.normal.textColor;
                        _style.alignment = TextAnchor.MiddleLeft;
                        for (var i = 0; i < _manager.Server.NumberOfConnectedClients; i++)
                        {
                            var client = _manager.Server.ConnectedClients.Values.ElementAt(i);
                            EditorGUILayout.BeginHorizontal();
                            {
                                _style.normal.textColor = client.UserColour;
                                GUILayout.Label($"#{client.ID} {client.Username}", _style);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        _style.normal.textColor = defaultColour;
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            GUILayout.EndVertical();
        }

        private void ClientGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            UnityUtilities.DrawToggleFoldout("Client", ref _showClientWindow, _manager.IsClient, "Is Client:");
            if (_showClientWindow)
            {
                if (!_manager.IsClient)
                {
                    _manager.Client.Username = EditorGUILayout.TextField(new GUIContent("Username:"), _manager.Client.Username);
                    _manager.Client.UserColour = EditorGUILayout.ColorField(new GUIContent("User colour:"), _manager.Client.UserColour);
                    if (GUILayout.Button(new GUIContent("Start Client")) && AllowStart())
                        _manager.StartClient();
                }
                else
                {
                    EditorGUILayout.TextField("ID:", $"{_manager.Client.ClientID}");
                    _manager.Client.Username = EditorGUILayout.TextField("Username:", _manager.Client.Username);
                    _manager.Client.UserColour = EditorGUILayout.ColorField("User colour:", _manager.Client.UserColour);
                    EditorGUILayout.TextField("Servername:", _manager.Client.Servername);
                    EditorGUILayout.TextField("Connected Clients:", $"{_manager.Client.NumberOfConnectedClients}/{_manager.Client.MaxNumberOfClients}");
                    if (GUILayout.Button(new GUIContent("Stop Client")))
                        _manager.StopClient();

                    _clientClientsViewPos = EditorGUILayout.BeginScrollView(_clientClientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
                    if (_manager.Client.ConnectedClients.Count == 0)
                    {
                        GUILayout.Label($"There are no other clients connected to the server!");
                    }
                    else
                    {
                        var defaultColour = _style.normal.textColor;
                        _style.alignment = TextAnchor.MiddleLeft;
                        for (var i = 0; i < _manager.Client.ConnectedClients.Count; i++)
                        {
                            var client = _manager.Client.ConnectedClients.Values.ElementAt(i);
                            EditorGUILayout.BeginHorizontal();
                            {
                                _style.normal.textColor = client.UserColour;
                                GUILayout.Label($"#{client.ID} {client.Username}", _style);
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        _style.normal.textColor = defaultColour;
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            GUILayout.EndVertical();
        }

        #endregion

        #region utilities

        private bool AllowStart()
        {
            return _allowStart switch
            {
                EAllowStart.Anywhere => true,
                EAllowStart.OnlyEditor => !EditorApplication.isPlaying,
                EAllowStart.OnlyPlaymode => EditorApplication.isPlaying,
                _ => false
            };
        }

        #endregion
    }
}
