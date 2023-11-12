using System;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    [Serializable]
    public class SerialiserConfiguration
    {
		/// <summary>
		/// Whether floats should be automatically compressed by the serialiser.
		/// </summary>
		public bool CompressFloats { get => _compressFloats; set => _compressFloats = value; }
        [SerializeField] private bool _compressFloats = false;

        /// <summary>
        /// The minimum value defining the range in which the compressed float can be saved.
        /// </summary>
        public float FloatMinValue { get => _floatMinValue; set => _floatMinValue = value; }
        [SerializeField] private float _floatMinValue = -1000;

        /// <summary>
        /// The maximum value defining the range in which the compressed float can be saved.
        /// </summary>
        public float FloatMaxValue { get => _floatMaxValue; set => _floatMaxValue = value; }
        [SerializeField] private float _floatMaxValue = 1000;

        /// <summary>
        /// The floating point resolution in which the float is serialised.
        /// </summary>
        public float FloatResolution { get => _floatResolution; set => _floatResolution = value; }
        [SerializeField] private float _floatResolution = 0.001f;

        /// <summary>
        /// Whether Quaternions should be automatically compressed by the serialiser.
        /// </summary>
        public bool CompressQuaternions { get => _compressQuaternions; set => _compressQuaternions = value; }
        [SerializeField] private bool _compressQuaternions = false;

        /// <summary>
        /// The number of bits used by each compressed Quaternion component.
        /// </summary>
        public int BitsPerComponent { get => _bitsPerComponent; set => _bitsPerComponent = value; }
        [SerializeField] private int _bitsPerComponent = 9;
    }
}
