using UnityEngine;
using jKnepel.SimpleUnityNetworking.Serialisation;

public class CompressionTest : MonoBehaviour
{
    [SerializeField] private Transform _obj1;
    [SerializeField] private Transform _obj2;
    [SerializeField] private float _min; 
    [SerializeField] private float _max; 
    [SerializeField] private float _resolution;

    private SerialiserConfiguration _serialiserConfiguration;

	private void Start()
	{
		_serialiserConfiguration = new SerialiserConfiguration();
		_serialiserConfiguration.CompressFloats = true;
	}

	private void Update()
	{
		_serialiserConfiguration.FloatMinValue = _min;
		_serialiserConfiguration.FloatMaxValue = _max;
		_serialiserConfiguration.FloatResolution = _resolution;
	}

	private void LateUpdate()
    {

        BitWriter writer = new(_serialiserConfiguration);
		writer.WriteVector3(_obj1.position);
        BitReader reader = new(writer.GetBuffer(), _serialiserConfiguration);
		_obj2.position = reader.ReadVector3();
    }
}
