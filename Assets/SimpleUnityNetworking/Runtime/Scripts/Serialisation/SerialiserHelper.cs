using System;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public static class SerialiserHelper
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

		// thanks to Glenn Fiedler https://gafferongames.com/post/reading_and_writing_packets/
		private static uint Popcount(uint x)
		{
			uint a = x - ((x >> 1) & 0x55555555);
			uint b = ((a >> 2) & 0x33333333) + (a & 0x33333333);
			uint c = ((b >> 4) + b) & 0x0f0f0f0f;
			uint d = c + (c >> 8);
			uint e = d + (d >> 16);
			uint result = e & 0x0000003f;
			return result;
		}

		private static uint Log2(uint x)
		{
			uint a = x | (x >> 1);
			uint b = a | (a >> 2);
			uint c = b | (b >> 4);
			uint d = c | (c >> 8);
			uint e = d | (d >> 16);
			uint f = e >> 1;
			return Popcount(f);
		}

		public static uint BitsRequired(uint min, uint max)
		{
			return (min == max) ? 0 : Log2(max - min) + 1;
		}
    }
}
