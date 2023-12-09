using System.Linq;
using System.Net;
using UnityEngine;
using UnityEditor;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Utilities;
using System;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    internal class NetworkManagerEditor
    {
        private string _joinServerIP = string.Empty;
        private int _joinServerPort = 0;

        private string _newServername = "New Server";
        private byte _maxNumberClients = 254;

        private bool _isAutoscroll = true;

        private readonly GUIStyle _style = new();
        private Vector2 _serversViewPos;
        private Vector2 _clientsViewPos;
        private Vector2 _messagesViewPos;
        private readonly Color[] _scrollViewColors = new Color[] { new(0.25f, 0.25f, 0.25f), new(0.23f, 0.23f, 0.23f) };

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

        private const float ROW_HEIGHT = 20;

        public void SubscribeNetworkEvents(NetworkEvents events, Action repaintEditorWindow)
        {
            events.OnConnectionStatusUpdated += repaintEditorWindow;
            events.OnConnectedClientListUpdated += repaintEditorWindow;
            events.OnOpenServerListUpdated += repaintEditorWindow;
            events.OnNetworkMessageAdded += repaintEditorWindow;
        }

        public void UnsubscribeNetworkEvents(NetworkEvents events, Action repaintEditorWindow)
        {
            events.OnConnectionStatusUpdated -= repaintEditorWindow;
            events.OnConnectedClientListUpdated -= repaintEditorWindow;
            events.OnOpenServerListUpdated -= repaintEditorWindow;
            events.OnNetworkMessageAdded -= repaintEditorWindow;
        }

        public void OnInspectorGUI(NetworkManager manager)
        {
            if (manager.IsConnected)
                ShowConnectedGUI(manager);
            else
                ShowDisconnectedGUI(manager);

            // messages
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            ShowSystemMessages();
        }

        private void ShowConnectedGUI(NetworkManager manager)
        {
            // connected clients list
            GUILayout.Label($"Current Server: {manager.ServerInformation.Servername}");
            _clientsViewPos = EditorGUILayout.BeginScrollView(_clientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
            {
                if (manager.NumberConnectedClients == 0)
                {
                    GUILayout.Label($"There are no Clients in this server!");
                }
                else
                {
                    Color defaultColor = _style.normal.textColor;
                    _style.alignment = TextAnchor.MiddleLeft;
                    for (int i = 0; i < manager.NumberConnectedClients; i++)
                    {
                        ClientInformation client = manager.ConnectedClients.Values.ElementAt(i);
                        EditorGUILayout.BeginHorizontal(GetScrollviewRowStyle(_scrollViewColors[i % 2]));
                        {
                            _style.normal.textColor = client.Color;
                            GUILayout.Label($"#{client.ID} {client.Username}", _style);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    _style.normal.textColor = defaultColor;
                }
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(new GUIContent(manager.IsHost ? "Close Server" : "Leave Server")))
                manager.DisconnectFromServer();
        }

        private void ShowDisconnectedGUI(NetworkManager manager)
        {
            // open servers list
            _serversViewPos = EditorGUILayout.BeginScrollView(_serversViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MinHeight(50), GUILayout.MaxHeight(200));
            {
                if (manager.IsServerDiscoveryActive)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Server Discovery", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Open Servers Count: {manager.OpenServers?.Count}");
                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < manager.OpenServers?.Count; i++)
                    {
                        OpenServer server = manager.OpenServers[i];
                        EditorGUILayout.BeginHorizontal(GetScrollviewRowStyle(_scrollViewColors[i % 2]));
                        {
                            GUILayout.Label(server.Servername);
                            GUILayout.Label($"#{server.NumberConnectedClients}/{server.MaxNumberConnectedClients}");
                            if (GUILayout.Button(new GUIContent("Connect To Server"), GUILayout.ExpandWidth(false)))
                                manager.JoinServer(server.Endpoint.Address, server.Endpoint.Port);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label("Server Discovery", EditorStyles.boldLabel);
                    GUILayout.Label("Server Discovery is currently inactive!");
                }
            }
            EditorGUILayout.EndScrollView();

            // manually join server
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label("Manually Join a Server", EditorStyles.boldLabel);
                _joinServerIP = EditorGUILayout.TextField(new GUIContent("Server IP"), _joinServerIP);
                _joinServerPort = EditorGUILayout.IntField(new GUIContent("Server Port"), _joinServerPort);
                if (GUILayout.Button(new GUIContent("Join")))
                {
                    if (IPAddress.TryParse(_joinServerIP, out IPAddress address))
                    {
                        Messaging.DebugMessage("The given Server IP is not valid!");
                        return;
                    }
                    manager.JoinServer(address, _joinServerPort);
                }
            }
            EditorGUILayout.EndVertical();

            // new server options
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label("Create a New Server", EditorStyles.boldLabel);
                _newServername = EditorGUILayout.TextField(new GUIContent("Server Name", "The name of the server."), _newServername);
                _maxNumberClients = (byte)EditorGUILayout.IntField(new GUIContent("Max Clients", "The number of clients that can connect to the server."), _maxNumberClients);
                if (GUILayout.Button(new GUIContent("Create")))
                    manager.CreateServer(_newServername, _maxNumberClients);
            }
            EditorGUILayout.EndVertical();
        }

        private void ShowSystemMessages()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Network Messages:");
            GUILayout.FlexibleSpace();
            _isAutoscroll = EditorGUILayout.Toggle(new GUIContent(" ", "Is Autoscrolling Messages"), _isAutoscroll);
            EditorGUILayout.EndHorizontal();
            _messagesViewPos = EditorGUILayout.BeginScrollView(_messagesViewPos,
                EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MinHeight(100), GUILayout.MaxHeight(200));
            {
                Color defaultColor = _style.normal.textColor;
                for (int i = 0; i < Messaging.Messages.Count; i++)
                {
                    Message message = Messaging.Messages.ElementAt(i);
                    EditorGUILayout.BeginHorizontal(GetScrollviewRowStyle(_scrollViewColors[i % 2]));
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
                _style.normal.textColor = defaultColor;
            }
            EditorGUILayout.EndScrollView();
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
    }

    public struct TestMessage : IStructData
    {
        public string Text;

        public TestMessage(string msg)
        {
            Text = msg;
        }

        public TestMessage ReadTestMessage(Reader reader)
        {
            string text = reader.ReadString();
            return new(text);
        }

        public void WriteTestMessage(Writer writer, TestMessage value)
        {
            writer.WriteString(value.Text);
        }
    }
}
