using System.Linq;
using System.Net;
using UnityEngine;
using UnityEditor;
using jKnepel.SimpleUnityNetworking.Networking;
using jKnepel.SimpleUnityNetworking.Networking.ServerDiscovery;
using jKnepel.SimpleUnityNetworking.Utilities;
using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Managing
{
    public class StaticNetworkManagerWindow : EditorWindow
    {
        [SerializeField] private NetworkConfiguration _cachedNetworkConfiguration = null;
        private NetworkConfiguration NetworkConfiguration
        {
            get => StaticNetworkManager.NetworkConfiguration;
            set
            {
                if (_cachedNetworkConfiguration != value)
                    _settingsEditor = null;

                _cachedNetworkConfiguration = value;
                StaticNetworkManager.NetworkConfiguration = _cachedNetworkConfiguration;
            }
        }

        private Editor _settingsEditor = null;
        public Editor SettingsEditor
        {
            get
            {
                if (_settingsEditor == null)
                    _settingsEditor = Editor.CreateEditor(NetworkConfiguration);
                return _settingsEditor;
            }
        }

        private string _joinServerIP = string.Empty;
        private int _joinServerPort = 0;

        private string _newServername = "New Server";
        private byte _maxNumberClients = 254;

        private bool _isAutoscroll = true;

        private readonly GUIStyle _style = new();
        private Vector2 _scrollViewPosition = Vector2.zero;
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

        [MenuItem("Window/SimpleUnityNetworking/Network Manager")]
        public static void ShowWindow()
        {
            GetWindow(typeof(StaticNetworkManagerWindow), false, "Network Manager");
        }

        private void OnEnable()
        {
            StaticNetworkManager.OnConnectionStatusUpdated += Repaint;
            StaticNetworkManager.OnConnectedClientListUpdated += Repaint;
            StaticNetworkManager.OnOpenServerListUpdated += Repaint;
            StaticNetworkManager.OnNetworkMessageAdded += Repaint;
        }

        private void OnDisable()
        {
            StaticNetworkManager.OnConnectionStatusUpdated -= Repaint;
            StaticNetworkManager.OnConnectedClientListUpdated -= Repaint;
            StaticNetworkManager.OnOpenServerListUpdated -= Repaint;
            StaticNetworkManager.OnNetworkMessageAdded -= Repaint;
        }

        private void OnGUI()
        {
            if (EditorApplication.isCompiling)
            {
                GUILayout.Label("The editor is compiling...\nSettings will show up after recompile.", EditorStyles.largeLabel);
                return;
            }

            _scrollViewPosition = EditorGUILayout.BeginScrollView(_scrollViewPosition);
            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

            GUILayout.Label("Network Manager", EditorStyles.largeLabel);

            NetworkConfiguration = (NetworkConfiguration)EditorGUILayout.ObjectField(_cachedNetworkConfiguration, typeof(NetworkConfiguration), true);
            if (NetworkConfiguration != null)
                SettingsEditor.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (StaticNetworkManager.IsConnected)
                ShowConnectedGUI();
            else
                ShowDisconnectedGUI();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            ShowSystemMessages();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void ShowConnectedGUI()
        {
            // connected clients list
            GUILayout.Label($"Current Server: {StaticNetworkManager.ServerInformation.Servername}");
            _clientsViewPos = EditorGUILayout.BeginScrollView(_clientsViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(150));
            {
                if (StaticNetworkManager.NumberConnectedClients == 0)
                {
                    GUILayout.Label($"There are no Clients in this server!");
                }
                else
                {
                    Color defaultColor = _style.normal.textColor;
                    _style.alignment = TextAnchor.MiddleLeft;
                    for (int i = 0; i < StaticNetworkManager.NumberConnectedClients; i++)
                    {
                        ClientInformation client = StaticNetworkManager.ConnectedClients.Values.ElementAt(i);
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

            if (GUILayout.Button(new GUIContent(StaticNetworkManager.IsHost ? "Close Server" : "Leave Server")))
                StaticNetworkManager.DisconnectFromServer();


            // **** TEMPORARY TESTS ****

            static void ReceiveMessage(byte id, byte[] msg)
            {
                Reader reader = new(msg);
                TestMessage message = reader.Read<TestMessage>();
                Debug.Log($"ID: {id} Text: {message.Text}");
            }
            if (GUILayout.Button(new GUIContent("Register")))
            {
                StaticNetworkManager.RegisterByteData("abc", ReceiveMessage);
            }
            if (GUILayout.Button(new GUIContent("Unregister")))
            {
                StaticNetworkManager.UnregisterByteData("abc", ReceiveMessage);
            }
            if (GUILayout.Button(new GUIContent("Send")))
            {
                Writer writer = new();
                writer.Write(new TestMessage(LOREM_IPSUM));
                StaticNetworkManager.SendByteData(0, "abc", writer.GetBuffer(), ENetworkChannel.ReliableOrdered, (success) => Debug.Log(success));
            }
        }

        private const string LOREM_IPSUM = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed ac feugiat enim. Fusce ut arcu maximus, ultricies ligula eu, consectetur ipsum. Donec dignissim cursus eleifend. Sed placerat varius diam, eget faucibus sem tempor quis. Ut in metus a lorem porttitor eleifend nec vel massa. Donec fringilla, quam nec bibendum dignissim, diam massa porta ante, nec ultricies tortor diam quis quam. Quisque consectetur eu eros vel fringilla. Duis leo tortor, vehicula vitae pellentesque non, tincidunt in dolor.";

        private void ShowDisconnectedGUI()
        {
            // open servers list
            _serversViewPos = EditorGUILayout.BeginScrollView(_serversViewPos, EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(200));
            {
                if (StaticNetworkManager.IsServerDiscoveryActive)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Server Discovery", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Open Servers Count: {StaticNetworkManager.OpenServers?.Count}");
                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < StaticNetworkManager.OpenServers?.Count; i++)
                    {
                        OpenServer server = StaticNetworkManager.OpenServers[i];
                        EditorGUILayout.BeginHorizontal(GetScrollviewRowStyle(_scrollViewColors[i % 2]));
                        {
                            GUILayout.Label(server.Servername);
                            GUILayout.Label($"#{server.NumberConnectedClients}/{server.MaxNumberConnectedClients}");
                            if (GUILayout.Button(new GUIContent("Connect To Server"), GUILayout.ExpandWidth(false)))
                                StaticNetworkManager.JoinServer(server.Endpoint.Address, server.Endpoint.Port);
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
                    StaticNetworkManager.JoinServer(address, _joinServerPort);
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
                    StaticNetworkManager.CreateServer(_newServername, _maxNumberClients);
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
                EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(200));
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
}
