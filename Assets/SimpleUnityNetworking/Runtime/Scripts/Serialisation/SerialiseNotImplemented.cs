using System;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public class SerialiseNotImplemented : Exception
    {
        public SerialiseNotImplemented() { }
        public SerialiseNotImplemented(string message) : base(message) { }
        public SerialiseNotImplemented(string message, Exception inner) : base(message, inner) { }
    }
}
