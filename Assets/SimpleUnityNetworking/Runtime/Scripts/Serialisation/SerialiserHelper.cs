using System;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    internal static class SerialiserHelper
    {
		public static string GetTypeName(Type type)
		{
			if (type.IsArray)
				return "Array";

			if (!type.IsGenericType)
				return type.Name;

			int index = type.Name.IndexOf("`");
			return type.Name[..index];
		}

		private static bool IsFlagSet(ESerialiserOptions serialiserOptions, byte flag)
        {
            return ((byte)serialiserOptions & flag) != 0;
		}
    }
}
