using System;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    [Serializable]
    public class SerialiserConfiguration
    {
	    /// <summary>
	    /// If, and what kind of compression should be used for all serialisation in the framework.
	    /// </summary>
	    public EUseCompression UseCompression = EUseCompression.Compressed;
	    /// <summary>
        /// If compression is active, this will define the number of decimal places to which
        /// floating point numbers will be compressed.
        /// </summary>
        public int NumberOfDecimalPlaces = 3;
        /// <summary>
        /// If compression is active, this will define the number of bits used by the three compressed Quaternion
        /// components in addition to the two flag bits.
        /// </summary>
        public int BitsPerComponent = 10;
    }

    public enum EUseCompression
    {
	    Uncompressed,
	    Compressed
    }
}
