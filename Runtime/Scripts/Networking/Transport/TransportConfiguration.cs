using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Transporting
{
    [Serializable]
    public abstract class TransportConfiguration : ScriptableObject
    {
        public abstract Transport Transport { get; }
    }
}
