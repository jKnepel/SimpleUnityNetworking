using UnityEngine;
using jKnepel.SimpleUnityNetworking.SyncDataTypes;
using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Samples
{
    public struct ClientVisualiserData : IStructData
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public ClientVisualiserData(Vector3 position, Quaternion rotation)
	    {
            Position = position;
            Rotation = rotation;
	    }

        public static ClientVisualiserData ReadClientVisualiserData(Reader reader)
	    {
            Vector3 position = reader.ReadVector3();
            Quaternion rotation = reader.ReadQuaternion();
            return new(position, rotation);
	    }

        public static void WriteClientVisualiserData(Writer writer, ClientVisualiserData data)
	    {
            writer.WriteVector3(data.Position);
            writer.WriteQuaternion(data.Rotation);
	    }
    }
}
