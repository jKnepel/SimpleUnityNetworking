using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public class BitWriter : Writer
    {
        #region fields

        /// <summary>
        /// The current bit position of the writer header within the buffer.
        /// </summary>
        /// <remarks>
        /// IMPORTANT! Do not use this position unless you know what you are doing! 
        /// Setting the position manually will not check for buffer bounds or update the length of the written buffer.
        /// </remarks>
        public new int Position { get; set; }
		/// <summary>
		/// The highest bit position to which the writer has written a value.
		/// </summary>
		public new int Length { get; private set; }
        /// <summary>
        /// The amount of bytes which were written to.
        /// </summary>
        public int ByteLength => (int)Math.Ceiling((float)Length / 8);
		/// <summary>
		/// The max capacity of the internal buffer.
		/// </summary>
		public new int Capacity => _buffer.Length * 8;

		public new readonly int Boolean = 1;
		public new readonly int Byte = 8;
		public new readonly int Int16 = 16;
		public new readonly int Int32 = 32;
		public new readonly int Int64 = 64;

		private static readonly ConcurrentDictionary<Type, Action<BitWriter, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

        #endregion

        #region lifecycle

        public BitWriter(SerialiserConfiguration config = null)
            : base(config) { }

        static BitWriter()
        {   // caches all implemented type handlers during compilation
            CreateTypeHandlerDelegate(typeof(bool));
            CreateTypeHandlerDelegate(typeof(byte));
            CreateTypeHandlerDelegate(typeof(sbyte));
            CreateTypeHandlerDelegate(typeof(ushort));
            CreateTypeHandlerDelegate(typeof(short));
            CreateTypeHandlerDelegate(typeof(uint));
            CreateTypeHandlerDelegate(typeof(int));
            CreateTypeHandlerDelegate(typeof(ulong));
            CreateTypeHandlerDelegate(typeof(long));
            CreateTypeHandlerDelegate(typeof(string));
            CreateTypeHandlerDelegate(typeof(char));
            CreateTypeHandlerDelegate(typeof(float));
            CreateTypeHandlerDelegate(typeof(double));
            CreateTypeHandlerDelegate(typeof(decimal));
            CreateTypeHandlerDelegate(typeof(Vector2));
            CreateTypeHandlerDelegate(typeof(Vector3));
            CreateTypeHandlerDelegate(typeof(Vector4));
            CreateTypeHandlerDelegate(typeof(Matrix4x4));
            CreateTypeHandlerDelegate(typeof(Color));
            CreateTypeHandlerDelegate(typeof(Color32));
            CreateTypeHandlerDelegate(typeof(DateTime));
        }

		#endregion

		#region automatic type handler

		protected override void Write<T>(T val, Type type)
		{
            if (!_unknownTypes.Contains(type))
            {   
                if (_typeHandlerCache.TryGetValue(type, out Action<BitWriter, object> handler))
                {   // check for already cached type handler delegates
                    handler(this, val);
                    return;
                }

                Action<BitWriter, object> customHandler = CreateTypeHandlerDelegate(type, true);
                if (customHandler != null)
                {   // use custom type handler if user defined method was found
                    customHandler(this, val);
                    return;
                }

                // TODO : remove this once pre-compile cached generic handlers are supported
                Action<BitWriter, object> implementedHandler = CreateTypeHandlerDelegate(type);
                if (implementedHandler != null)
                {   // use implemented type handler
                    implementedHandler(this, val);
                    return;
                }

                // save types that don't have any a type handler and need to be recursively serialised
                _unknownTypes.Add(type);
			}

            // recursively serialise type if no handler is found
            // TODO : circular dependencies will cause crash
            // TODO : add attributes for serialisation
            // TODO : add serialisation options to handle size, circular dependencies etc. 
            // TODO : handle properties
            FieldInfo[] fieldInfos = type.GetFields();               
            if (fieldInfos.Length == 0 || fieldInfos.Where(x => x.FieldType == type).Any())
			{
                string typeName = SerialiserHelper.GetTypeName(type);
                throw new SerialiseNotImplemented($"No write method implemented for the type {typeName}!"
                    + $" Implement a Write{typeName} method or use an extension method in the parent type!");
			}

            foreach (FieldInfo fieldInfo in fieldInfos)
                Write(fieldInfo.GetValue(val), fieldInfo.FieldType);
        }

        /// <summary>
        /// Constructs and caches pre-compiled expression delegate of type handlers.
        /// </summary>
        /// <remarks>
        /// TODO : also cache generic handlers during compilation
        /// </remarks>
        /// <param name="type">The type of the variable for which the writer is defined</param>
        /// <param name="useCustomWriter">Wether the writer method is an instance of the Writer class or a custom static method in the type</param>
        /// <returns></returns>
        private static Action<BitWriter, object> CreateTypeHandlerDelegate(Type type, bool useCustomWriter = false)
        {   // find implemented or custom write method
            var writerMethod = useCustomWriter
                ?           type.GetMethod($"Write{SerialiserHelper.GetTypeName(type)}", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : typeof(BitWriter).GetMethod($"Write{SerialiserHelper.GetTypeName(type)}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (writerMethod == null)
                return null;

            // parameters
            var instanceArg = Expression.Parameter(typeof(BitWriter), "instance");
            var objectArg = Expression.Parameter(typeof(object), "value");
            var castArg = Expression.Convert(objectArg, type);

            // construct handler call body
            MethodCallExpression call;
            if (writerMethod.IsGenericMethod)
			{
                var genericWriter = type.IsArray
                    ? writerMethod.MakeGenericMethod(type.GetElementType())
                    : writerMethod.MakeGenericMethod(type.GetGenericArguments());
                call = useCustomWriter
                    ? Expression.Call(genericWriter, instanceArg, castArg)
                    : Expression.Call(instanceArg, genericWriter, castArg);
            }
            else
			{
                call = useCustomWriter
                    ? Expression.Call(writerMethod, instanceArg, castArg)
                    : Expression.Call(instanceArg, writerMethod, castArg);
			}

            // cache delegate
            var lambda = Expression.Lambda<Action<BitWriter, object>>(call, instanceArg, objectArg);
            var action = lambda.Compile();
            _typeHandlerCache.TryAdd(type, action);
            return action;
        }

        #endregion

        #region helpers

        private void AdjustBufferSize(int size)
		{
			if (Position + size > Capacity)
			{
				int requiredBytes = (int)Mathf.Ceil((float)(Position + size - Capacity) / 8);
				Array.Resize(ref _buffer, (_buffer.Length * 2) + requiredBytes);
			}
		}

		private void WriteBits(ulong val, int bits)
		{
			AdjustBufferSize(bits);

			int bytePosition = Position / 8;
			int valueOffset = bits + (Position % 8) - 8;
			for (int i = 0; i <= bits / 8; i++)
			{
				int bufferOffset = valueOffset - 8 * i;
				if (bufferOffset > 0)
					_buffer[bytePosition + i] |= (byte)(val >> bufferOffset);
				else
					_buffer[bytePosition + i] |= (byte)(val << Math.Abs(bufferOffset));
			}

			Position += bits;
			Length = Math.Max(Length, Position);
		}

		/// <summary>
		/// Skips the writer header ahead by the given number of bits.
		/// </summary>
		/// <param name="bits"></param>
		public override void Skip(int bits)
        {
			AdjustBufferSize(bits);
			Position += bits;
			Length = Math.Max(Length, Position);
		}

		/// <summary>
		/// Reverts the writer header back by the given number of bits.
		/// </summary>
		/// <param name="val"></param>
		public override void Revert(int bits)
        {
			Position -= bits;
			Position = Math.Max(Position, 0);
		}

		/// <summary>
		/// Clears the writer buffer.
		/// </summary>
		public override void Clear()
		{
			Position = 0;
			Length = 0;
		}

		/// <returns>The written buffer.</returns>
		public override byte[] GetBuffer()
        {
			byte[] result = new byte[ByteLength];
			Array.Copy(_buffer, 0, result, 0, ByteLength);
			return result;
		}

		/// <summary>
		/// Writes a specified number of bytes from a source array starting at a particular byte offset to the buffer.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="srcOffset"></param>
		/// <param name="count"></param>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public override void BlockCopy(ref byte[] src, int srcOffset, int count)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (srcOffset + count > src.Length)
                throw new ArgumentException(nameof(count));
            if (srcOffset < 0 || count <= 0)
                throw new ArgumentOutOfRangeException();

			for (int i = 0; i < count; i++)
				WriteBits(src[srcOffset + i], Byte);
		}

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public override void WriteByteSegment(ArraySegment<byte> src)
        {
			for (int i = 0; i < src.Count; i++)
				WriteBits(src[i], Byte);
		}

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public override void WriteByteArray(byte[] src)
        {
            for (int i = 0; i < src.Length; i++)
                WriteBits(src[i], Byte);
		}

		#endregion

		#region primitives

		public override void WriteBoolean(bool val)
		{
			WriteBits((ulong)(val ? 1 : 0), Boolean);
		}

        public override void WriteByte(byte val)
        {
			WriteBits(val, Byte);
		}

        public override void WriteSByte(sbyte val)
        {
            WriteByte((byte)val);
		}

        public override void WriteUInt16(ushort val)
        {
			WriteBits(val, Int16);
		}

        public override void WriteInt16(short val)
        {
            WriteUInt16((ushort)val);
        }

        public override void WriteUInt32(uint val)
        {
			WriteBits(val, Int32);
		}

        public override void WriteInt32(int val)
        {
            WriteUInt32((uint)val);
        }

        public override void WriteUInt64(ulong val)
        {
			WriteBits(val, Int64);
		}

        public override void WriteInt64(long val)
        {
            WriteUInt64((ulong)val);
        }

        public override void WriteChar(char val)
        {
            WriteUInt16(val);
        }

        public override void WriteSingle(float val)
        {
            if (SerialiserConfiguration.CompressFloats)
            {
                WriteCompressedSingle(
                    val,
                    SerialiserConfiguration.FloatMinValue,
                    SerialiserConfiguration.FloatMaxValue,
                    SerialiserConfiguration.FloatResolution);
            }
            else
            {
				TypeConverter.UIntToFloat converter = new() { Float = val };
				WriteUInt32(converter.UInt);
			}
        }

        public void WriteUncompressedSingle(float val)
        {
			TypeConverter.UIntToFloat converter = new() { Float = val };
			WriteUInt32(converter.UInt);
		}

        public void WriteCompressedSingle(float val, float min, float max, float resolution)
		{   // thanks to Glenn Fiedler https://gafferongames.com/post/serialization_strategies/
			float delta = max - min;
			float values = delta / resolution;

			float normalizedValue = Mathf.Clamp((val - min) / delta, 0.0f, 1.0f);
			uint maxIntValue = (uint)Mathf.Ceil(values);
            uint requiredBits = SerialiserHelper.BitsRequired(0, maxIntValue);
			uint intValue = (uint)Mathf.Floor(normalizedValue * maxIntValue + 0.5f);

            WriteBits(intValue, (int)requiredBits);
		}

		public override void WriteDouble(double val)
        {
            TypeConverter.ULongToDouble converter = new() { Double = val };
            WriteUInt64(converter.ULong);
        }

        public override void WriteDecimal(decimal val)
        {
            TypeConverter.ULongsToDecimal converter = new() { Decimal = val };
            WriteUInt64(converter.ULong1);
            WriteUInt64(converter.ULong2);
        }

        #endregion

        #region unity objects

        public override void WriteVector2(Vector2 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
        }

        public void WriteCompressedVector2(Vector2 val,  float min, float max, float resolution)
        {
            WriteCompressedSingle(val.x, min, max, resolution);
            WriteCompressedSingle(val.y, min, max, resolution);
        }

        public override void WriteVector3(Vector3 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
        }

		public void WriteCompressedVector3(Vector3 val, float min, float max, float resolution)
		{
			WriteCompressedSingle(val.x, min, max, resolution);
			WriteCompressedSingle(val.y, min, max, resolution);
			WriteCompressedSingle(val.z, min, max, resolution);
		}

		public override void WriteVector4(Vector4 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
            WriteSingle(val.w);
        }

		public void WriteCompressedVector4(Vector4 val, float min, float max, float resolution)
		{
			WriteCompressedSingle(val.x, min, max, resolution);
			WriteCompressedSingle(val.y, min, max, resolution);
			WriteCompressedSingle(val.z, min, max, resolution);
			WriteCompressedSingle(val.w, min, max, resolution);
		}

		public override void WriteQuaternion(Quaternion val)
        {
            if (SerialiserConfiguration.CompressQuaternions)
            {
                CompressedQuaternion q = new(val, SerialiserConfiguration.BitsPerComponent);
                WriteBits(q.Largest, 2);
                WriteBits(q.A, SerialiserConfiguration.BitsPerComponent);
                WriteBits(q.B, SerialiserConfiguration.BitsPerComponent);
                WriteBits(q.C, SerialiserConfiguration.BitsPerComponent);
            }
            else
            {
                WriteUncompressedSingle(val.x);
				WriteUncompressedSingle(val.y);
				WriteUncompressedSingle(val.z);
				WriteUncompressedSingle(val.w);
            }
        }

        public override void WriteMatrix4x4(Matrix4x4 val)
        {
            WriteSingle(val.m00);
            WriteSingle(val.m01);
            WriteSingle(val.m02);
            WriteSingle(val.m03);
            WriteSingle(val.m10);
            WriteSingle(val.m11);
            WriteSingle(val.m12);
            WriteSingle(val.m13);
            WriteSingle(val.m20);
            WriteSingle(val.m21);
            WriteSingle(val.m22);
            WriteSingle(val.m23);
        }

        public override void WriteColor(Color val)
        {
            WriteByte((byte)(val.r * 100.0f));
            WriteByte((byte)(val.g * 100.0f));
            WriteByte((byte)(val.b * 100.0f));
            WriteByte((byte)(val.a * 100.0f));
        }

        public override void WriteColorWithoutAlpha(Color val)
        {
            WriteByte((byte)(val.r * 100.0f));
            WriteByte((byte)(val.g * 100.0f));
            WriteByte((byte)(val.b * 100.0f));
        }

        public override void WriteColor32(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
            WriteByte(val.a);
        }

        public override void WriteColor32WithoutAlpha(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
        }

        #endregion

        #region objects

        public override void WriteString(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                WriteByte(0);
                return;
            }

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

            WriteUInt16((ushort)val.Length);
            WriteByteArray(Encoding.ASCII.GetBytes(val));
        }

        public override void WriteStringWithoutFlag(string val)
        {
            if (string.IsNullOrEmpty(val))
			{
                WriteByte(0);
                return;
			}

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

			WriteByteArray(Encoding.ASCII.GetBytes(val));
		}

        public override void WriteArray<T>(T[] val)
		{
            if (val == null)
			{
                WriteInt32(0);
                return;
			}

            WriteInt32(val.Length);
            foreach (T t in val)
                Write(t);
        }

		public override void WriteList<T>(List<T> val)
        {
            if (val == null)
			{
                WriteInt32(0);
                return;
			}

            WriteInt32(val.Count);
            foreach (T t in val)
                Write(t);
        }

        public override void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> val)
		{
            if (val == null)
            {
                WriteInt32(0);
                return;
            }

            WriteInt32(val.Count);
            foreach (KeyValuePair<TKey, TValue> entry in val)
			{
                Write(entry.Key);
                Write(entry.Value);
			}
		}

        public override void WriteDateTime(DateTime val)
		{
            WriteInt64(val.ToBinary());
		}

		#endregion
	}
}
