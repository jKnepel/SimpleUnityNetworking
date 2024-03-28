using System;

namespace jKnepel.SimpleUnityNetworking.Logging
{
    [Serializable]
    public class LoggerSettings
    {
        /// <summary>
        /// Whether logged messages by the framework should also be printed to the console.
        /// </summary>
        public bool PrintToConsole = true;

        /// <summary>
        /// Whether log level messages should be printed to the console.
        /// </summary>
        public bool PrintLog = true;
        /// <summary>
        /// Whether warning level messages should be printed to the console.
        /// </summary>
        public bool PrintWarning = true;
        /// <summary>
        /// Whether error level messages should be printed to the console.
        /// </summary>
        public bool PrintError = true;
    }
}
