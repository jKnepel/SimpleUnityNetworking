using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Serialisation;
using jKnepel.SimpleUnityNetworking.Utilities;

public class BitPackingTest : MonoBehaviour
{
    void Start()
    {
        TestStruct test = new TestStruct()
        {
            Value1 = true,
            Value2 = false,
            Value3 = true,
            Value4 = true,
            Value5 = 32,
            Value6 = 255,
            Value7 = 42342,
            Value8 = 12312312311,
        };

        Writer writer = new Writer(ESerialiserOptions.EnableBitSerialiserMode);
        writer.Write(test);
        Messaging.DebugByteMessage(writer.GetBuffer(), "BitPacking: ", true);
        Reader reader = new Reader(writer.GetBuffer(), ESerialiserOptions.EnableBitSerialiserMode);
        Debug.Log(reader.Read<TestStruct>());
        /*
        Writer writer2 = new Writer();
		writer2.Write(test);
        Messaging.DebugByteMessage(writer2.GetBuffer(), "Normal: ", true);
        */
	}

    private struct TestStruct
    {
        public bool Value1;
        public bool Value2;
        public bool Value3;
        public bool Value4;
        public byte Value5;
        public byte Value6;
        public ushort Value7;
        public ulong Value8;

        public override readonly string ToString()
        {
            return $"{Value1} {Value2} {Value3} {Value4} {Value5} {Value6} {Value7} {Value8}";
        }
    }
}
