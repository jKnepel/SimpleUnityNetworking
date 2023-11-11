using UnityEngine;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.Utilities;

public class BitPackingTest : MonoBehaviour
{
    void Start()
    {
        TestStruct test = new()
        {
            /*
            */
            Value1 = true,
            Value9 = "testing",
            Value2 = false,
            Value3 = true,
            Value4 = true,
            Value5 = 32,
            Value6 = 255,
            Value7 = 42342,
            Value8 = 12312312311,
        };

		BitWriter writer = new();
        writer.Write(test);
        Messaging.DebugByteMessage(writer.GetBuffer(), "BitPacking: ", true);
		
        BitReader reader = new(writer.GetBuffer());
        Debug.Log(reader.Read<TestStruct>());
	}

	private struct TestStruct
    {
        /*
        */
        public bool Value1;
        public string Value9;
        public bool Value2;
        public bool Value3;
        public bool Value4;
        public byte Value5;
        public byte Value6;
        public ushort Value7;
        public ulong Value8;

        public override readonly string ToString()
        {
            return $"{Value1} {Value9} {Value2} {Value3} {Value4} {Value5} {Value6} {Value7} {Value8}";
        }
    }
}
