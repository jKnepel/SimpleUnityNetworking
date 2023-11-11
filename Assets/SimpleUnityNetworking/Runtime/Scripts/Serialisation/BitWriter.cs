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
    public class BitWriter
    {
        #region fields

        /// <summary>
        /// The current bit position of the writer header within the buffer.
        /// </summary>
        /// <remarks>
        /// IMPORTANT! Do not use this position unless you know what you are doing! 
        /// Setting the position manually will not check for buffer bounds or update the length of the written buffer.
        /// </remarks>
        public int Position { get; set; }
		/// <summary>
		/// The highest bit position to which the writer has written a value.
		/// </summary>
		public int Length { get; private set; }
        /// <summary>
        /// The amount of bytes which were written to.
        /// </summary>
        public int ByteLength => (int)Math.Ceiling((float)Length / 8);
		/// <summary>
		/// The max capacity of the internal buffer.
		/// </summary>
		public int Capacity => _buffer.Length * 8;
        /// <summary>
        /// The set serialiser option flags of the writer.
        /// </summary>
        public ESerialiserOptions SerialiserOptions { get; }

        private byte[] _buffer = new byte[32];

        private static readonly ConcurrentDictionary<Type, Action<BitWriter, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

        #endregion

        #region lifecycle

        public BitWriter(ESerialiserOptions serialiserOptions = ESerialiserOptions.None)
        {
            SerialiserOptions = serialiserOptions;
        }

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

		internal static void Init() { }

		#endregion

		#region automatic type handler

		public void Write<T>(T val)
        {
            Type type = typeof(T);
            Write(val, type);
        }

        private void Write<T>(T val, Type type)
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
				float remainingBits = size - 8 - Position;
				int requiredBytes = (int)Mathf.Ceil(remainingBits / 8);
				Array.Resize(ref _buffer, (Capacity * 2) + requiredBytes);
			}
		}

		private void WriteBits(ulong val, EPrimitiveBitLength bits)
		{
            int intBits = (int)bits;
			AdjustBufferSize(intBits);

			int bytePosition = Position / 8;
			int valueOffset = intBits + (Position % 8) - 8;
			for (int i = 0; i <= intBits / 8; i++)
			{
				int bufferOffset = valueOffset - 8 * i;
				if (bufferOffset > 0)
					_buffer[bytePosition + i] |= (byte)(val >> bufferOffset);
				else
					_buffer[bytePosition + i] |= (byte)(val << Math.Abs(bufferOffset));
			}

			Position += intBits;
			Length = Math.Max(Length, Position);
		}

		/// <summary>
		/// Skips the writer header ahead by the given number of bits.
		/// </summary>
		/// <param name="bits"></param>
		public void Skip(int bits)
        {
			AdjustBufferSize(bits);
			Position += bits;
			Length = Math.Max(Length, Position);
		}

		/// <summary>
		/// Skips the writer header ahead by the given primitives' lengths.
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
		/// Reverts the writer header back by the given number of bits.
		/// </summary>
		/// <param name="val"></param>
		public void Revert(int bits)
        {
			Position -= bits;
			Position = Math.Max(Position, 0);
		}

        /// <summary>
        /// Reverts the writer header back by the given primitives' lengths.
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
            Position = 0;
            Length = 0;
		}

		/// <returns>The written buffer.</returns>
		public byte[] GetBuffer()
        {
			byte[] result = new byte[ByteLength];
			Array.Copy(_buffer, 0, result, 0, ByteLength);
			return result;
		}

		/// <returns>The entire internal buffer.</returns>
		public byte[] GetFullBuffer()
        {
            return _buffer;
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
		public void BlockCopy(ref byte[] src, int srcOffset, int count)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (srcOffset + count > src.Length)
                throw new ArgumentException(nameof(count));
            if (srcOffset < 0 || count <= 0)
                throw new ArgumentOutOfRangeException();

			for (int i = 0; i < count; i++)
				WriteBits(src[srcOffset + i], EPrimitiveBitLength.Byte);
		}

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public void WriteByteSegment(ArraySegment<byte> src)
        {
			for (int i = 0; i < src.Count; i++)
				WriteBits(src[i], EPrimitiveBitLength.Byte);
		}

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public void WriteByteArray(byte[] src)
        {
            for (int i = 0; i < src.Length; i++)
                WriteBits(src[i], EPrimitiveBitLength.Byte);
		}

        #endregion

        #region primitives

        public void WriteBoolean(bool val)
		{
			WriteBits((ulong)(val ? 1 : 0), EPrimitiveBitLength.Boolean);
		}

        public void WriteByte(byte val)
        {
			WriteBits(val, EPrimitiveBitLength.Byte);
		}

        public void WriteSByte(sbyte val)
        {
            WriteByte((byte)val);
		}

        public void WriteUInt16(ushort val)
        {
			WriteBits(val, EPrimitiveBitLength.Short);
		}

        public void WriteInt16(short val)
        {
            WriteUInt16((ushort)val);
        }

        public void WriteUInt32(uint val)
        {
			WriteBits(val, EPrimitiveBitLength.Int);
		}

        public void WriteInt32(int val)
        {
            WriteUInt32((uint)val);
        }

        public void WriteUInt64(ulong val)
        {
			WriteBits(val, EPrimitiveBitLength.Long);
		}

        public void WriteInt64(long val)
        {
            WriteUInt64((ulong)val);
        }

        public void WriteChar(char val)
        {
            WriteUInt16(val);
        }

        public void WriteSingle(float val)
        {
            TypeConverter.UIntToFloat converter = new() { Float = val };
            WriteUInt32(converter.UInt);
        }

        public void WriteDouble(double val)
        {
            TypeConverter.ULongToDouble converter = new() { Double = val };
            WriteUInt64(converter.ULong);
        }

        public void WriteDecimal(decimal val)
        {
            TypeConverter.ULongsToDecimal converter = new() { Decimal = val };
            WriteUInt64(converter.ULong1);
            WriteUInt64(converter.ULong2);
        }

        #endregion

        #region unity objects

        public void WriteVector2(Vector2 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
        }

        public void WriteVector3(Vector3 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
        }

        public void WriteVector4(Vector4 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
            WriteSingle(val.w);
        }

        public void WriteQuaternion(Quaternion val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
            WriteSingle(val.w);
        }

        public void WriteMatrix4x4(Matrix4x4 val)
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

        public void WriteColor(Color val)
        {
            WriteByte((byte)(val.r * 100.0f));
            WriteByte((byte)(val.g * 100.0f));
            WriteByte((byte)(val.b * 100.0f));
            WriteByte((byte)(val.a * 100.0f));
        }

        public void WriteColorWithoutAlpha(Color val)
        {
            WriteByte((byte)(val.r * 100.0f));
            WriteByte((byte)(val.g * 100.0f));
            WriteByte((byte)(val.b * 100.0f));
        }

        public void WriteColor32(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
            WriteByte(val.a);
        }

        public void WriteColor32WithoutAlpha(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
        }

        #endregion

        #region objects

        public void WriteString(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                WriteByte(0);
                return;
            }

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

            WriteUInt16((ushort)val.Length);
            WriteBits(Encoding.ASCII.GetBytes(val));
        }

        public void WriteStringWithoutFlag(string val)
        {
            if (string.IsNullOrEmpty(val))
			{
                WriteByte(0);
                return;
			}

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

			WriteBits(Encoding.ASCII.GetBytes(val));
		}

        public void WriteArray<T>(T[] val)
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

		public void WriteList<T>(List<T> val)
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

        public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> val)
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

        public void WriteDateTime(DateTime val)
		{
            WriteInt64(val.ToBinary());
		}

		#endregion
	}
}
