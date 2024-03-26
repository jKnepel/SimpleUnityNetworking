using jKnepel.SimpleUnityNetworking.Logging;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.Transporting;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    internal class INetworkManagerEditor
    {
        #region fields
        
        private readonly INetworkManager _manager;

        private readonly GUIStyle _style = new();
        
        private bool _showTransportWindow;
        private bool _showSerialiserWindow;
        private bool _showLoggerWindow;
        
        private bool _showServerWindow;
        private string _servername = "New Server";
        private Vector2 _serverClientsViewPos;

        private bool _showClientWindow;
        private string _username = "Username";
        private Color32 _userColour = new(153, 191, 97, 255);
        private Vector2 _clientClientsViewPos;
        
        private bool _isAutoscroll = true;
        private Vector2 _messagesViewPos;
        
        private readonly Color[] _scrollViewColours = { new(0.25f, 0.25f, 0.25f), new(0.23f, 0.23f, 0.23f) };

        private Texture2D _texture;
        private Texture2D Texture => _texture ? _texture : _texture = new(1, 1);

        #endregion
        
        #region lifecycle

        public INetworkManagerEditor(INetworkManager manager)
        {
            _manager = manager;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Configurations:", EditorStyles.boldLabel);
            {
                TransportGUI();
                SerialiserGUI();
                LoggerGUI();
            }
            
            EditorGUILayout.Space();
            GUILayout.Label("Managers:", EditorStyles.boldLabel);
            {
                ServerGUI();
                ClientGUI();
            }
        }
        
        #endregion
        
        #region configs
        
        private void TransportGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout("Transport:", ref _showTransportWindow);
            if (_showTransportWindow)
            {
                _manager.TransportConfiguration = (TransportConfiguration)EditorGUILayout.ObjectField(
                    "Transport Configuration:",
                    _manager.TransportConfiguration,
                    typeof(TransportConfiguration),
                    false
                );
                if (_manager.TransportConfiguration)
                    Editor.CreateEditor(_manager.TransportConfiguration).OnInspectorGUI();
            }
            GUILayout.EndVertical();
        }

        private void SerialiserGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout("Serialiser:", ref _showSerialiserWindow);
            if (_showSerialiserWindow && _manager.SerialiserConfiguration != null)
            {
                _manager.SerialiserConfiguration.UseCompression = (EUseCompression)EditorGUILayout.EnumPopup(
                    new GUIContent("Use Compression:", "If, and what kind of compression should be used for all serialisation in the framework."),
                    _manager.SerialiserConfiguration.UseCompression
                );
                _manager.SerialiserConfiguration.NumberOfDecimalPlaces = EditorGUILayout.IntField(
                    new GUIContent("Number of Decimal Places:", "If compression is active, this will define the number of decimal places to which floating point numbers will be compressed."),
                    _manager.SerialiserConfiguration.NumberOfDecimalPlaces
                );
                _manager.SerialiserConfiguration.BitsPerComponent = EditorGUILayout.IntField(
                    new GUIContent("Bits Per Quaternion Component:", "If compression is active, this will define the number of bits used by the three compressed Quaternion components in addition to the two flag bits."),
                    _manager.SerialiserConfiguration.BitsPerComponent
                );
            }
            GUILayout.EndVertical();
        }
        
        private void LoggerGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout("Logger:", ref _showLoggerWindow);
            if (_showLoggerWindow)
            {
                _manager.LoggerConfiguration = (LoggerConfiguration)EditorGUILayout.ObjectField(
                    "Logger Configuration:",
                    _manager.LoggerConfiguration,
                    typeof(LoggerConfiguration),
                    false
                );

                if (_manager.LoggerConfiguration)
                {
                    Editor.CreateEditor(_manager.LoggerConfiguration).OnInspectorGUI();
                    EditorGUILayout.Space();
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Messages:");
                    GUILayout.FlexibleSpace();
                    _isAutoscroll = EditorGUILayout.Toggle(new GUIContent(" ", "Is Autoscrolling Messages"), _isAutoscroll);
                    EditorGUILayout.EndHorizontal();
                    _messagesViewPos = EditorGUILayout.BeginScrollView(_messagesViewPos,
                        EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MinHeight(100), GUILayout.MaxHeight(200));
                    {
                        var defaultColour = _style.normal.textColor;
                        for (int i = 0; i < _manager.LoggerConfiguration.Logger.Messages.Count; i++)
                        {
                            var message = _manager.LoggerConfiguration.Logger.Messages.ElementAt(i);
                            EditorGUILayout.BeginHorizontal(GetScrollviewRowStyle(_scrollViewColours[i % 2]));
                            {
                                switch (message.Severity)
                                {
                                    case EMessageSeverity.Log:
                                        _style.normal.textColor = Color.white;
                                        break;
                                    case EMessageSeverity.Warning:
                                        _style.normal.textColor = Color.yellow;
                                        break;
                                    case EMessageSeverity.Error:
                                        _style.normal.textColor = Color.red;
                                        break;
                                }
                                GUILayout.Label(message.Text, _style);
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
        
        #region managing
        
        private void ServerGUI()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            DrawToggleFoldout("Server", ref _showServerWindow, _manager.IsServer, "Is Server:");
            if (_showServerWindow)
            {
                if (!_manager.IsServer)
                {
                    _servername = EditorGUILayout.TextField(new GUIContent("Servername:"), _servername);
                    if (GUILayout.Button(new GUIContent("Start Server")))
                        _manager.StartServer(_servername);
                }
                else
                {
                    GUILayout.Label($"Servername: {_manager.ServerInformation.Servername}");
                    GUILayout.Label($"Connected Clients: {_manager.Server_ConnectedClients.Count}/{_manager.ServerInformation.MaxNumberConnectedClients}");
                    if (GUILayout.Button(new GUIContent("Stop Server")))
                        _manager.StopServer();
                
                    _serverClientsViewPos = EditorGUILayout.BeginScrollView(_serverClientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
                    if (_manager.Server_ConnectedClients.Count == 0)
                    {
                        GUILayout.Label($"There are no clients connected to the local server!");
                    }
                    else
                    {
                        var defaultColour = _style.normal.textColor;
                        _style.alignment = TextAnchor.MiddleLeft;
                        for (var i = 0; i < _manager.Server_ConnectedClients.Count; i++)
                        {
                            var client = _manager.Server_ConnectedClients.Values.ElementAt(i);
                            EditorGUILayout.BeginHorizontal();
                            {
                                _style.normal.textColor = client.Colour;
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
            DrawToggleFoldout("Client", ref _showClientWindow, _manager.IsClient, "Is Client:");
            if (_showClientWindow)
            {
                if (!_manager.IsClient)
                {
                    _username = EditorGUILayout.TextField(new GUIContent("Username:"), _username);
                    _userColour = EditorGUILayout.ColorField(new GUIContent("User colour:"), _userColour);
                    if (GUILayout.Button(new GUIContent("Start Client")))
                        _manager.StartClient(_username, _userColour);
                }
                else
                {
                    GUILayout.Label($"ID: {_manager.ClientInformation.ID}");
                    GUILayout.Label($"Username: {_manager.ClientInformation.Username}");
                    EditorGUILayout.ColorField("User colour:", _manager.ClientInformation.Colour);
                    GUILayout.Label($"Servername: {_manager.ServerInformation.Servername}");
                    GUILayout.Label($"Connected Clients: {_manager.Server_ConnectedClients.Count}/{_manager.ServerInformation.MaxNumberConnectedClients}");
                    if (GUILayout.Button(new GUIContent("Stop Client")))
                        _manager.StopClient();
                    
                    _clientClientsViewPos = EditorGUILayout.BeginScrollView(_clientClientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
                    if (_manager.Client_ConnectedClients.Count == 0)
                    {
                        GUILayout.Label($"There are no other clients connected to the server!");
                    }
                    else
                    {
                        var defaultColour = _style.normal.textColor;
                        _style.alignment = TextAnchor.MiddleLeft;
                        for (var i = 0; i < _manager.Client_ConnectedClients.Count; i++)
                        {
                            var client = _manager.Client_ConnectedClients.Values.ElementAt(i);
                            EditorGUILayout.BeginHorizontal();
                            {
                                _style.normal.textColor = client.Colour;
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

        private const float ROW_HEIGHT = 20;
        private GUIStyle GetScrollviewRowStyle(Color colour)
        {
            Texture.SetPixel(0, 0, colour);
            Texture.Apply();
            GUIStyle style = new();
            style.normal.background = Texture;
            style.fixedHeight = ROW_HEIGHT;
            return style;
        }
        
        private static void DrawToggleFoldout(string title, ref bool isExpanded, 
            bool? checkbox = null, string checkboxLabel = null)
        {   
            Color normalColour = new(0.24f, 0.24f, 0.24f);
            Color hoverColour = new(0.27f, 0.27f, 0.27f);
            var currentColour = normalColour;
            
            var backgroundRect = GUILayoutUtility.GetRect(1f, 17f);
            var labelRect = backgroundRect;
            labelRect.xMin += 16f;
            labelRect.xMax -= 2f;

            var foldoutRect = backgroundRect;
            foldoutRect.y += 1f;
            foldoutRect.width = 13f;
            foldoutRect.height = 13f;
            
            var toggleRect = backgroundRect;
            toggleRect.x = backgroundRect.width - 7f;
            toggleRect.y += 2f;
            toggleRect.width = 13f;
            toggleRect.height = 13f;
            
            var toggleLabelRect = backgroundRect;
            toggleLabelRect.x = -10f;
            
            var e = Event.current;
            if (labelRect.Contains(e.mousePosition))
                currentColour = hoverColour;
            EditorGUI.DrawRect(backgroundRect, currentColour);

            if (isExpanded)
            {
                var borderBot = GUILayoutUtility.GetRect(1f, 0.6f);
                EditorGUI.DrawRect(borderBot, new(0, 0, 0));
            }
            
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            isExpanded = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

            if (checkbox is not null)
            {
                if (checkboxLabel is not null)
                {
                    var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight }; 
                    EditorGUI.LabelField(toggleLabelRect, checkboxLabel, labelStyle);
                }
                EditorGUI.Toggle(toggleRect, (bool)checkbox, new("ShurikenToggle"));
            }

            if (e.type == EventType.MouseDown && labelRect.Contains(e.mousePosition) && e.button == 0)
            {
                isExpanded = !isExpanded;
                e.Use();
            }
            
        }
        
        #endregion
    }
}
