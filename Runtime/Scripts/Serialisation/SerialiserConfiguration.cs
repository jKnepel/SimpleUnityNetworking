using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
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
    }
}
