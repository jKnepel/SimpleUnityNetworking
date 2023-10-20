using System;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Utilities
{
    public static class Messaging
    {
        public static Action OnNetworkMessageAdded;

        private static readonly List<Message> _messages = new();
        public static List<Message> Messages => _messages;

        public static bool ShowDebugMessages = true;

        public static void DebugMessage(string msg, EMessageSeverity sev = EMessageSeverity.Error)
        {   // TODO : add alternative message system to unity
            if (!ShowDebugMessages)
                return;

            string formattedTime = DateTime.Now.ToString("H:mm:ss");
            switch (sev)
			{
                case EMessageSeverity.Log:
                    Debug.Log($"[{formattedTime}] {msg}");
                    break;
                case EMessageSeverity.Warning:
                    Debug.LogWarning($"[{formattedTime}] {msg}");
                    break;
                case EMessageSeverity.Error:
                    Debug.LogError($"[{formattedTime}] {msg}");
                    break;
            }
		}

        public static void DebugByteMessage(byte[] bytes, string msg, bool inBinary = false)
        {   // TODO : add alternative message system to unity
            if (!ShowDebugMessages)
                return;

            foreach (byte d in bytes)
                msg += Convert.ToString(d, inBinary ? 2 : 16).PadLeft(inBinary ? 8 : 2, '0') + " ";
            Debug.Log(msg);
        }

        public static void SystemMessage(string text, EMessageSeverity severity = EMessageSeverity.Log)
		{
            string formattedTime = DateTime.Now.ToString("H:mm:ss");
            _messages.Add(new($"[{formattedTime}] {text}", severity));
		}
    }

    public struct Message
	{
        public string Text;
        public EMessageSeverity Severity;

        public Message(string text, EMessageSeverity severity = EMessageSeverity.Log)
		{
            Text = text;
            Severity = severity;
		}
	}

    public enum EMessageSeverity
	{
        Log = 0,
        Warning = 1,
        Error = 2
	}
}
