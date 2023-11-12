using System;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    [Serializable]
    public class SerialiserConfiguration
    {
        [SerializeField] private bool _compressFloats = false;
        public bool CompressFloats { get => _compressFloats; set => _compressFloats = value; }

        [SerializeField] private float _floatMinValue = -1000;
        public float FloatMinValue { get => _floatMinValue; set => _floatMinValue = value; }

        [SerializeField] private float _floatMaxValue = 1000;
        public float FloatMaxValue { get => _floatMaxValue; set => _floatMaxValue = value; }

        [SerializeField] private float _floatResolution = 0.001f;
        public float FloatResolution { get => _floatResolution; set => _floatResolution = value; }

        [SerializeField] private bool _compressQuaternions = false;
        public bool CompressQuaternions { get => _compressQuaternions; set => _compressQuaternions = value; }

        [SerializeField] private int _bitsPerComponent = 9;
        public int BitsPerComponent { get => _bitsPerComponent; set => _bitsPerComponent = value; }
    }
}
