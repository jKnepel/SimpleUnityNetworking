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

        public event Action<Message> OnMessageAdded;

        public Logger(LoggerSettings settings)
        {
            _settings = settings;
        }

        public void SetLoggerSettings(LoggerSettings settings)
        {
            _settings = settings;
        }

        public void Log(string text, EMessageSeverity sev = EMessageSeverity.Error)
        {
            Message msg = new(text, DateTime.Now, sev);
            lock (_messages)
                _messages.Add(msg);
            
            OnMessageAdded?.Invoke(msg);
            
            if (!_settings.PrintDebugToConsole) return;
            
            switch (sev)
            {
                case EMessageSeverity.Log:
                    Debug.Log(msg.GetFormattedString());
                    break;
                case EMessageSeverity.Warning:
                    Debug.LogWarning(msg.GetFormattedString());
                    break;
                case EMessageSeverity.Error:
                    Debug.LogError(msg.GetFormattedString());
                    break;
            }
        }
    }

    public struct Message
    {
        public string Text;
        public DateTime Time;
        public EMessageSeverity Severity;

        public Message(string text, DateTime time, EMessageSeverity severity)
        {
            Text = text;
            Severity = severity;
            Time = time;
        }

        public string GetFormattedString()
        {
            var formattedTime = Time.ToString("H:mm:ss");
            return $"[{formattedTime}] {Text}";
        }
    }

    public enum EMessageSeverity
    {
        Log = 0,
        Warning = 1,
        Error = 2
    }
}