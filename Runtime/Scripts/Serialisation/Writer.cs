using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public class Writer
    {
		#region fields

		/// <summary>
		/// The current byte position of the writer header within the buffer.
		/// </summary>
		/// <remarks>
		/// Do not use this position unless you know what you are doing! 
		/// Setting the position manually will not check for buffer bounds or update the length of the written buffer.
		/// </remarks>
		public int Position { get; set; }
		/// <summary>
		/// The highest byte position to which the writer has written a value.
		/// </summary>
		public int Length { get; set; }
		/// <summary>
		/// The max capacity of the internal buffer.
		/// </summary>
		public int Capacity => _buffer.Length;
		/// <summary>
		/// The configuration of the writer.
		/// </summary>
		public SerialiserConfiguration SerialiserConfiguration { get; }

		private byte[] _buffer = new byte[32];

		private static readonly ConcurrentDictionary<Type, Action<Writer, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

		#endregion

		#region lifecycle

		public Writer(SerialiserConfiguration config = null)
		{
            SerialiserConfiguration = config ?? new();
		}

	    static Writer()
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
            var type = typeof(T);
            Write(val, type);
        }

        private void Write<T>(T val, Type type)
		{
            if (!_unknownTypes.Contains(type))
            {   
                if (_typeHandlerCache.TryGetValue(type, out Action<Writer, object> handler))
                {   // check for already cached type handler delegates
                    handler(this, val);
                    return;
                }

                var customHandler = CreateTypeHandlerDelegate(type, true);
                if (customHandler != null)
                {   // use custom type handler if user defined method was found
                    customHandler(this, val);
                    return;
                }

                // TODO : remove this once pre-compile cached generic handlers are supported
                var implementedHandler = CreateTypeHandlerDelegate(type);
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
            var fieldInfos = type.GetFields();               
            if (fieldInfos.Length == 0 || fieldInfos.Any(x => x.FieldType == type))
			{
                var typeName = SerialiserHelper.GetTypeName(type);
                throw new SerialiseNotImplemented($"No write method implemented for the type {typeName}!"
                    + $" Implement a Write{typeName} method or use an extension method in the parent type!");
			}

            foreach (var fieldInfo in fieldInfos)
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
        private static Action<Writer, object> CreateTypeHandlerDelegate(Type type, bool useCustomWriter = false)
        {   // find implemented or custom write method
            var writerMethod = useCustomWriter
                ?           type.GetMethod("Write", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : typeof(Writer).GetMethod($"Write{SerialiserHelper.GetTypeName(type)}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (writerMethod == null)
                return null;

            // parameters
            var instanceArg = Expression.Parameter(typeof(Writer), "instance");
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
            var lambda = Expression.Lambda<Action<Writer, object>>(call, instanceArg, objectArg);
            var action = lambda.Compile();
            _typeHandlerCache.TryAdd(type, action);
            return action;
        }

        #endregion

        #region helpers

        private void AdjustBufferSize(int size)
		{
            if (Position + size > _buffer.Length)
                Array.Resize(ref _buffer, _buffer.Length * 2 + size);
		}

		/// <summary>
		/// Skips the writer header ahead by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public void Skip(int bytes)
		{
            AdjustBufferSize(bytes);
            Position += bytes;
            Length = Math.Max(Length, Position);
		}

		/// <summary>
		/// Reverts the writer header back by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public void Revert(int bytes)
		{
			Position -= bytes;
			Position = Mathf.Max(Position, 0);
		}

		/// <summary>
		/// Clears the writer buffer.
		/// </summary>
		public void Clear()
		{
            Position = 0;
            Length = 0;
		}

		/// <returns>The written buffer.</returns>
		public byte[] GetBuffer()
        {
            byte[] result = new byte[Length];
            Array.Copy(_buffer, 0, result, 0, Length);
            return result;
        }

		/// <returns>The entire internal buffer.</returns>
		public byte[] GetFullBuffer()
        {
            return _buffer;
        }

		/// <summary>
		/// Writes a specified number of bytes from a source array starting at a particular offset to the buffer.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="srcOffset"></param>
		/// <param name="count"></param>
		public void BlockCopy(ref byte[] src, int srcOffset, int count)
        {
			AdjustBufferSize(count);
            Buffer.BlockCopy(src, srcOffset, _buffer, Position, count);
            Position += count;
            Length = Math.Max(Length, Position);
        }

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public void WriteByteSegment(ArraySegment<byte> src)
        {
            var srcArray = src.Array;
            BlockCopy(ref srcArray, 0, src.Count);
        }

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public void WriteByteArray(byte[] src)
        {
            BlockCopy(ref src, 0, src.Length);
        }

        #endregion

        #region primitives

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolean(bool val)
		{
            AdjustBufferSize(1);
            _buffer[Position++] = (byte)(val ? 1 : 0);
            Length = Math.Max(Length, Position);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte val)
        {
            AdjustBufferSize(1);
            _buffer[Position++] = val;
            Length = Math.Max(Length, Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte val)
        {
            WriteByte((byte)val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
				WriteVLQCompression(val);
	        }
	        else
	        {
	            AdjustBufferSize(2);
	            _buffer[Position++] = (byte)val;
	            _buffer[Position++] = (byte)(val >> 8);
	            Length = Math.Max(Length, Position);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(short val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        WriteVLQCompression(ZigZagEncode(val));
	        }
	        else
	        {
		        AdjustBufferSize(2);
		        _buffer[Position++] = (byte)val;
		        _buffer[Position++] = (byte)(val >> 8);
		        Length = Math.Max(Length, Position);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        WriteVLQCompression(val);
	        }
	        else
	        {
		        AdjustBufferSize(4);
		        _buffer[Position++] = (byte)val;
		        _buffer[Position++] = (byte)(val >> 8);
		        _buffer[Position++] = (byte)(val >> 16);
		        _buffer[Position++] = (byte)(val >> 24);
		        Length = Math.Max(Length, Position);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        WriteVLQCompression(ZigZagEncode(val));
	        }
	        else
	        {
		        AdjustBufferSize(4);
		        _buffer[Position++] = (byte)val;
		        _buffer[Position++] = (byte)(val >> 8);
		        _buffer[Position++] = (byte)(val >> 16);
		        _buffer[Position++] = (byte)(val >> 24);
		        Length = Math.Max(Length, Position);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        WriteVLQCompression(val);
	        }
	        else
	        {
		        AdjustBufferSize(8);
		        _buffer[Position++] = (byte)val;
		        _buffer[Position++] = (byte)(val >> 8);
		        _buffer[Position++] = (byte)(val >> 16);
		        _buffer[Position++] = (byte)(val >> 24);
		        _buffer[Position++] = (byte)(val >> 32);
		        _buffer[Position++] = (byte)(val >> 40);
		        _buffer[Position++] = (byte)(val >> 48);
		        _buffer[Position++] = (byte)(val >> 56);
		        Length = Math.Max(Length, Position);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        WriteVLQCompression(ZigZagEncode(val));
	        }
	        else
	        {
		        AdjustBufferSize(8);
		        _buffer[Position++] = (byte)val;
		        _buffer[Position++] = (byte)(val >> 8);
		        _buffer[Position++] = (byte)(val >> 16);
		        _buffer[Position++] = (byte)(val >> 24);
		        _buffer[Position++] = (byte)(val >> 32);
		        _buffer[Position++] = (byte)(val >> 40);
		        _buffer[Position++] = (byte)(val >> 48);
		        _buffer[Position++] = (byte)(val >> 56);
		        Length = Math.Max(Length, Position);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char val)
        {
            WriteUInt16(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingle(float val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        var compressed = val * Mathf.Pow(10, SerialiserConfiguration.NumberOfDecimalPlaces);
		        WriteVLQCompression(ZigZagEncode((int)compressed));
	        }
	        else
	        {
	            TypeConverter.UIntToFloat converter = new() { Float = val };
	            WriteUInt32(converter.UInt);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double val)
        {
            TypeConverter.ULongToDouble converter = new() { Double = val };
            WriteUInt64(converter.ULong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimal(decimal val)
        {
            TypeConverter.ULongsToDecimal converter = new() { Decimal = val };
            WriteUInt64(converter.ULong1);
            WriteUInt64(converter.ULong2);
        }

        #endregion

        #region unity objects

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2(Vector2 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3(Vector3 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector4(Vector4 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
            WriteSingle(val.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteQuaternion(Quaternion val)
        {
	        if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
	        {
		        CompressedQuaternion q = new(val, SerialiserConfiguration.BitsPerComponent);
		        WriteVLQCompression(q.PackedQuaternion);
	        }
	        else
	        {
	            WriteSingle(val.x);
	            WriteSingle(val.y);
	            WriteSingle(val.z);
	            WriteSingle(val.w);
	        }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor(Color val)
        {
            WriteByte((byte)(val.r * 100f));
            WriteByte((byte)(val.g * 100f));
            WriteByte((byte)(val.b * 100f));
            WriteByte((byte)(val.a * 100f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColorWithoutAlpha(Color val)
        {
            WriteByte((byte)(val.r * 100f));
            WriteByte((byte)(val.g * 100f));
            WriteByte((byte)(val.b * 100f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor32(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
            WriteByte(val.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor32WithoutAlpha(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
        }

        #endregion

        #region objects

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var bytes = Encoding.ASCII.GetBytes(val);
            BlockCopy(ref bytes, 0, bytes.Length);
            Length = Math.Max(Length, Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringWithoutFlag(string val)
        {
            if (string.IsNullOrEmpty(val))
			{
                WriteByte(0);
                return;
			}

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

            var bytes = Encoding.ASCII.GetBytes(val);
            BlockCopy(ref bytes, 0, bytes.Length);
            Length = Math.Max(Length, Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(T[] val)
		{
            if (val == null)
			{
                WriteInt32(0);
                return;
			}

            WriteInt32(val.Length);
            foreach (var t in val)
                Write(t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteList<T>(List<T> val)
        {
            if (val == null)
			{
                WriteInt32(0);
                return;
			}

            WriteInt32(val.Count);
            foreach (var t in val)
                Write(t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> val)
		{
            if (val == null)
            {
                WriteInt32(0);
                return;
            }

            WriteInt32(val.Count);
            foreach (var entry in val)
			{
                Write(entry.Key);
                Write(entry.Value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDateTime(DateTime val)
		{
            WriteInt64(val.ToBinary());
		}

		#endregion
		
		#region utilities
		
		/// <summary>
		/// "ZigZagEncoding" based on google protocol buffers.
		/// See for <a href="https://protobuf.dev/programming-guides/encoding/">reference</a>.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static ulong ZigZagEncode(short val)
		{
			return (ulong)((val >> 15) ^ (val << 1));
		}

		/// <summary>
		/// "ZigZagEncoding" based on google protocol buffers.
		/// See for <a href="https://protobuf.dev/programming-guides/encoding/">reference</a>.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static ulong ZigZagEncode(int val)
		{
			return (ulong)((val >> 31) ^ (val << 1));
		}
		
		/// <summary>
		/// "ZigZagEncoding" based on google protocol buffers.
		/// See for <a href="https://protobuf.dev/programming-guides/encoding/">reference</a>.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static ulong ZigZagEncode(long val)
		{
			return (ulong)((val >> 63) ^ (val << 1));
		}

		/// <summary>
		/// Uses a 7-bit VLQ encoding scheme based on the MIDI compression system.
		/// See for <a href="https://web.archive.org/web/20051129113105/http://www.borg.com/~jglatt/tech/midifile/vari.htm">reference</a>
		/// </summary>
		/// <param name="val"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WriteVLQCompression(ulong val)
		{
			do
			{
				var lowerBits = (byte)(val & 0x7F);
				val >>= 7;
				if (val > 0)
					lowerBits |= 0x80;
				_buffer[Position++] = lowerBits;
			} while (val > 0);

			Length = Math.Max(Length, Position);
		}
		
		#endregion
	}
}
