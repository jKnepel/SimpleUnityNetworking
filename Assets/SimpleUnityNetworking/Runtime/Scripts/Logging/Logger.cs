using System;
using System.Collections.Generic;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Logging
{
    public class Logger
    {
        private LoggerSettings _settings;

        private readonly List<Message> _messages = new();
        public List<Message> Messages => _messages;

        public event Action OnMessageAdded;

        public Logger(LoggerSettings settings)
        {
            _settings = settings;
        }

        public void SetLoggerSettings(LoggerSettings settings)
        {
            _settings = settings;
        }

        public void Log(string msg, EMessageSeverity sev = EMessageSeverity.Error, bool isDebug = false)
        {
            var formattedTime = DateTime.Now.ToString("H:mm:ss");
            lock (_messages)
                _messages.Add(new($"[{formattedTime}] {msg}", sev));
            
            OnMessageAdded?.Invoke();
            
            if (!isDebug || _settings.PrintDebugToConsole) return;
            
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
