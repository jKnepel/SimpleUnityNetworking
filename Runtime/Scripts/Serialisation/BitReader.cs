using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public class BitReader
    {
		#region fields

		/// <summary>
		/// The current bit position of the writer header within the buffer.
		/// </summary>
		/// <remarks>
		/// IMPORTANT! Do not use this position unless you know what you are doing! 
		/// Setting the position manually will not check for buffer bounds.
		/// </remarks>
		public int Position;
        /// <summary>
        /// The length of the given buffer in bits.
        /// </summary>
        public int Length => _buffer.Length * 8;
		/// <summary>
		/// The length of the given buffer in bytes.
		/// </summary>
		public int ByteLength => _buffer.Length; 
		/// <summary>
		/// The remaining positions until the full length of the buffer.
		/// </summary>
		public int Remaining => Length - Position;
        /// <summary>
        /// The configuration of the bit reader.
        /// </summary>
        public SerialiserConfiguration SerialiserConfiguration { get; }

		private readonly byte[] _buffer;

		private static readonly ConcurrentDictionary<Type, Func<BitReader, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

        #endregion

        #region lifecycle

        public BitReader(byte[] bytes, SerialiserConfiguration config = null)
            : this(new ArraySegment<byte>(bytes), config) { }

        public BitReader(ArraySegment<byte> bytes, SerialiserConfiguration config = null)
		{
            if (bytes.Array == null)
                return;

            Position = bytes.Offset;
            _buffer = bytes.Array;
            SerialiserConfiguration = config ?? new();
		}

        static BitReader()
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

        internal static void Init() { }

		#endregion

		#region automatic type handler

		public T Read<T>()
		{
            Type type = typeof(T);
            return (T)Read(type);
        }

        private object Read(Type type)
		{
            if (!_unknownTypes.Contains(type))
			{
                if (_typeHandlerCache.TryGetValue(type, out Func<BitReader, object> handler))
                {   // check for already cached type handler delegates
                    return handler(this);
                }

                Func<BitReader, object> customHandler = CreateTypeHandlerDelegate(type, true);
                if (customHandler != null)
                {   // use custom type handler if user defined method was found
                    return customHandler(this);
                }

                // TODO : remove this once pre-compile cached generic handlers are supported
                Func<BitReader, object> implementedHandler = CreateTypeHandlerDelegate(type, false);
                if (implementedHandler != null)
                {   // use implemented type handler
                    return implementedHandler(this);
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
            {   // TODO : circular dependencies will cause crash
                string typeName = SerialiserHelper.GetTypeName(type);
                throw new SerialiseNotImplemented($"No read method implemented for the type {typeName}!"
                    + $" Implement a Read{typeName} method or use an extension method in the parent type!");
			}

            object obj = FormatterServices.GetUninitializedObject(type);
            foreach (FieldInfo fieldInfo in fieldInfos)
                fieldInfo.SetValue(obj, Read(fieldInfo.FieldType));
            return obj;
        }

        /// <summary>
        /// Constructs and caches pre-compiled expression delegate of type handlers.
        /// </summary>
        /// <remarks>
        /// TODO : also cache generic handlers during compilation
        /// </remarks>
        /// <param name="type">The type of the variable for which the writer is defined</param>
        /// <param name="useCustomReader">Wether the reader method is an instance of the Reader class or a custom static method in the type</param>
        /// <returns></returns>
        private static Func<BitReader, object> CreateTypeHandlerDelegate(Type type, bool useCustomReader = false)
        {   // find implemented or custom read method
            var readerMethod = useCustomReader
                ?           type.GetMethod($"Read{SerialiserHelper.GetTypeName(type)}", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : typeof(BitReader).GetMethod($"Read{SerialiserHelper.GetTypeName(type)}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (readerMethod == null)
                return null;

            // parameters
            var instanceArg = Expression.Parameter(typeof(BitReader), "instance");

            // construct handler call body
            MethodCallExpression call;
            if (readerMethod.IsGenericMethod)
            {
                var genericReader = type.IsArray
                    ? readerMethod.MakeGenericMethod(type.GetElementType())
                    : readerMethod.MakeGenericMethod(type.GetGenericArguments());
                call = useCustomReader
                    ? Expression.Call(genericReader, instanceArg)
                    : Expression.Call(instanceArg, genericReader);
            }
            else
            {
                call = useCustomReader
                    ? Expression.Call(readerMethod, instanceArg)
                    : Expression.Call(instanceArg, readerMethod);
            }

            // cache delegate
            var castResult = Expression.Convert(call, typeof(object));
            var lambda = Expression.Lambda<Func<BitReader, object>>(castResult, instanceArg);
            var action = lambda.Compile();
            _typeHandlerCache.TryAdd(type, action);
            return action;
        }

        #endregion

        #region helpers

        private ulong ReadBits(int bits)
        {
            int bytePosition = Position / 8;
            int relevantBits = Mathf.Min(8 - Position % 8, bits);
            int resultOffset = 64 - relevantBits;

            // get relevant bits from current byte
			ulong result = _buffer[bytePosition];
            result >>= 8 - relevantBits - Position % 8;
            result <<= resultOffset;

            // get remaining bits from remaining bytes
            int remainingBytes = (int)Mathf.Ceil((float)(bits - relevantBits) / 8);
			for (int i = 1; i <= remainingBytes; i++)
            {
                int byteOffset = resultOffset - 8 * i;
                if (byteOffset > 0)
				    result |= (ulong)_buffer[bytePosition + i] << byteOffset;
                else
				    result |= (ulong)_buffer[bytePosition + i] >> Math.Abs(byteOffset);
			}

			Position += bits;
            result >>= 64 - bits;
			return result;
        }

        private ulong ReadBits(EPrimitiveBitLength bits)
        {
            return ReadBits((int)bits);
        }

		/// <summary>
		/// Skips the reader header ahead by the given number of bits.
		/// </summary>
		/// <param name="bits"></param>
		public void Skip(int bits)
		{
            if (bits < 1 || bits > Remaining)
                return;

            Position += bits;
		}

		/// <summary>
		/// Skips the reader header ahead by the given primitives' lengths.
		/// </summary>
		/// <param name="val"></param>
		public void Skip(params EPrimitiveBitLength[] val)
        {
			int bits = 0;
			foreach (EPrimitiveBitLength length in val)
				bits += (byte)length;
			Skip(bits);
		}

		/// <summary>
		/// Reverts the reader header back by the given number of bits.
		/// </summary>
		/// <param name="bits"></param>
		public void Revert(int bits)
		{
			Position -= bits;
			Position = Math.Max(Position, 0);
		}

		/// <summary>
		/// Reverts the reader header back by the given primitives' lengths.
		/// </summary>
		/// <param name="val"></param>
		public void Revert(params EPrimitiveBitLength[] val)
		{
			int bits = 0;
			foreach (EPrimitiveBitLength length in val)
				bits += (byte)length;
			Revert(bits);
		}

		/// <summary>
		/// Clears the writter buffer.
		/// </summary>
		public void Clear()
		{
            Position += Remaining;
		}

		/// <returns>The full internal buffer.</returns>
		public byte[] GetFullBuffer()
		{
            return _buffer;
		}

		/// <summary>
		/// Reads a specified number of bytes from the internal buffer to a destination array starting at a particular offset.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="dstOffset"></param>
		/// <param name="count"></param>
		public void BlockCopy(ref byte[] dst, int dstOffset, int count)
        {
            byte[] bytes = ReadByteArray(count);
            Buffer.BlockCopy(bytes, 0, dst, dstOffset, bytes.Length);
        }

		/// <summary>
		/// Reads a specified number of bytes.
		/// </summary>
		/// <param name="count"></param>
		/// <returns>The read bits inside a byte array.</returns>
		/// <exception cref="ArgumentException">If the count exceeds the buffer</exception>
		public ArraySegment<byte> ReadByteSegment(int count)
        {
			if (count * 8 > Remaining)
				throw new ArgumentException("The count exceeds the remaining length!");

			byte[] bytes = new byte[count];
			for (int i = 0; i < count; i++)
				bytes[i] = (byte)ReadBits(EPrimitiveBitLength.Byte);
			return new(bytes);
		}

		/// <summary>
		/// Reads a specified number of bytes.
		/// </summary>
		/// <param name="count"></param>
		/// <returns>The read bits inside a byte array.</returns>
		/// <exception cref="ArgumentException">If the count exceeds the buffer</exception>
		public byte[] ReadByteArray(int count)
		{
			if (count * 8 > Remaining)
                throw new ArgumentException("The count exceeds the remaining length!");

			byte[] bytes = new byte[count];
            for (int i = 0; i < count; i++)
                bytes[i] = (byte)ReadBits(EPrimitiveBitLength.Byte);
            return bytes;
		}


		/// <returns>The remaining bits inside a byte array.</returns>
		public byte[] ReadRemainingBits()
		{
            int numberBytes = (int)Mathf.Ceil((float)Remaining / 8);
            byte[] bytes = new byte[numberBytes];
            for (int i = 0; i < numberBytes - 1; i++)
                bytes[i] = (byte)ReadBits(EPrimitiveBitLength.Byte);
            bytes[^1] = (byte)ReadBits(Remaining);
            return bytes;
		}

		#endregion

		#region primitives

		public bool ReadBoolean()
		{
			return ReadBits(EPrimitiveBitLength.Boolean) == 1;
		}

        public byte ReadByte()
		{
			return (byte)ReadBits(EPrimitiveBitLength.Byte);
		}

        public sbyte ReadSByte()
		{
            return (sbyte)ReadByte();
		}

        public ushort ReadUInt16()
		{
			return (ushort)ReadBits(EPrimitiveBitLength.Short);
		}

        public short ReadInt16()
		{
            return (short)ReadUInt16();
		}

        public uint ReadUInt32()
		{
			return (uint)ReadBits(EPrimitiveBitLength.Int);
		}

        public int ReadInt32()
		{
            return (int)ReadUInt32();
		}

        public ulong ReadUInt64()
		{
			return ReadBits(EPrimitiveBitLength.Long);
		}

        public long ReadInt64()
		{
            return (long)ReadUInt64();
		}

        public char ReadChar()
		{
            return (char)ReadUInt16();
        }

        public float ReadSingle()
		{
            if (SerialiserConfiguration.CompressFloats)
            {
                return ReadCompressedSingle(
                    SerialiserConfiguration.FloatMinValue,
                    SerialiserConfiguration.FloatMaxValue,
                    SerialiserConfiguration.FloatResolution);
            }
            else
            {
                TypeConverter.UIntToFloat converter = new() { UInt = ReadUInt32() };
                return converter.Float;
            }
        }

		public float ReadCompressedSingle(float min, float max, float resolution)
		{   // thanks to Glenn Fiedler https://gafferongames.com/post/serialization_strategies/
			float delta = max - min;
			float values = delta / resolution;

			uint maxIntValue = (uint)Mathf.Ceil(values);
			uint requiredBits = SerialiserHelper.BitsRequired(0, maxIntValue);
			uint integerValue = (uint)ReadBits((int)requiredBits);

			float normalizedValue = integerValue / (float)maxIntValue;
            return normalizedValue * delta + min;
		}

		public double ReadDouble()
		{
            TypeConverter.ULongToDouble converter = new() { ULong = ReadUInt64() };
            return converter.Double;
        }

        public decimal ReadDecimal()
		{
            TypeConverter.ULongsToDecimal converter = new() { ULong1 = ReadUInt64(), ULong2 = ReadUInt64() };
            return converter.Decimal;
        }

		#endregion

		#region unity objects

        public Vector2 ReadVector2()
		{
            return new Vector2(ReadSingle(), ReadSingle());
		}

        public Vector2 ReadCompressedVector2(float min, float max, float resolution)
        {
            return new Vector2(
                ReadCompressedSingle(min, max, resolution), 
                ReadCompressedSingle(min, max, resolution));
        }

        public Vector3 ReadVector3()
		{
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
		}

		public Vector3 ReadCompressedVector3(float min, float max, float resolution)
		{
			return new Vector3(
                ReadCompressedSingle(min, max, resolution),
                ReadCompressedSingle(min, max, resolution),
				ReadCompressedSingle(min, max, resolution));
		}

		public Vector4 ReadVector4()
		{
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
		}

		public Vector4 ReadCompressedVector4(float min, float max, float resolution)
		{
			return new Vector4(
				ReadCompressedSingle(min, max, resolution),
				ReadCompressedSingle(min, max, resolution),
				ReadCompressedSingle(min, max, resolution));
		}

		public Quaternion ReadQuaternion()
		{
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
		}

        public Matrix4x4 ReadMatrix4x4()
		{
            Matrix4x4 result = new()
			{
                m00 = ReadSingle(), m01 = ReadSingle(), m02 = ReadSingle(), m03 = ReadSingle(),
                m10 = ReadSingle(), m11 = ReadSingle(), m12 = ReadSingle(), m13 = ReadSingle(),
                m20 = ReadSingle(), m21 = ReadSingle(), m22 = ReadSingle(), m23 = ReadSingle(),
                m30 = ReadSingle(), m31 = ReadSingle(), m32 = ReadSingle(), m33 = ReadSingle()
            };
            return result;
        }

        public Color ReadColor()
		{
            float r = (float)(ReadByte() / 100.0f);
            float g = (float)(ReadByte() / 100.0f);
            float b = (float)(ReadByte() / 100.0f);
            float a = (float)(ReadByte() / 100.0f);
            return new Color(r, g, b, a);
		}

        public Color ReadColorWithoutAlpha()
		{
            float r = (float)(ReadByte() / 100.0f);
            float g = (float)(ReadByte() / 100.0f);
            float b = (float)(ReadByte() / 100.0f);
            return new Color(r, g, b, 1);
        }

        public Color32 ReadColor32()
		{
            return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
		}

        public Color32 ReadColor32WithoutAlpha()
		{
            return new Color32(ReadByte(), ReadByte(), ReadByte(), 255);
        }

		#endregion

		#region objects

        public string ReadString()
		{
            ushort length = ReadUInt16();
            return Encoding.ASCII.GetString(ReadByteArray(length));
		}

        public string ReadStringWithoutFlag(int length)
        {
            return Encoding.ASCII.GetString(ReadByteArray(length));
        }

        public T[] ReadArray<T>()
		{
            int length = ReadInt32();
            T[] array = new T[length];
            for (int i = 0; i < length; i++)
                array[i] = Read<T>();
            return array;
		}

        public List<T> ReadList<T>()
		{
            int count = ReadInt32();
            List<T> list = new(count);
            for (int i = 0; i < count; i++)
                list.Add(Read<T>());
            return list;
        }

        public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>()
		{
            int count = ReadInt32();
            Dictionary<TKey, TValue> dictionary = new(count);
            for (int i = 0; i < count; i++)
                dictionary.Add(Read<TKey>(), Read<TValue>());
            return dictionary;
        }

        public DateTime ReadDateTime()
		{
            return DateTime.FromBinary(ReadInt64());
		}

        #endregion
    }
}
