using System;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    [Serializable]
    public class SerialiserConfiguration
    {
	    /// <summary>
	    /// If, and what kind of compression should be used for all serialisation in the framework.
	    /// </summary>
	    public EUseCompression UseCompression { get => _useCompression; set => _useCompression = value; }
	    [SerializeField] private EUseCompression _useCompression = EUseCompression.Compressed;
	    
	    /// <summary>
        /// If compression is active, this will define the number of decimal places to which
        /// floating point numbers will be compressed.
        /// </summary>
        public int NumberOfDecimalPlaces { get => _numberOfDecimalPlaces; set => _numberOfDecimalPlaces = value; }
        [SerializeField] private int _numberOfDecimalPlaces = 3;

        /// <summary>
        /// If compression is active, this will define the number of bits used by the three compressed Quaternion
        /// components in addition to the two flag bits.
        /// </summary>
        public int BitsPerComponent { get => _bitsPerComponent; set => _bitsPerComponent = value; }
        [SerializeField] private int _bitsPerComponent = 10;
    }

    public enum EUseCompression
    {
	    Uncompressed,
	    Compressed
    }
}
