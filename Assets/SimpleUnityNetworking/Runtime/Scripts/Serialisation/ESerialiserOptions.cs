using System;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    [Flags]
    public enum ESerialiserOptions : byte
    {
        None = 0,
        EnableBitSerialiserMode = 1
    }
}
