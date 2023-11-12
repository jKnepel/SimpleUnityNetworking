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
    public class Writer
    {
		#region fields

		/// <summary>
		/// The current byte position of the writer header within the buffer.
		/// </summary>
		/// <remarks>
		/// IMPORTANT! Do not use this position unless you know what you are doing! 
		/// Setting the position manually will not check for buffer bounds or update the length of the written buffer.
		/// </remarks>
		public int Position { get; set; }
		/// <summary>
		/// The highest byte position to which the writer has written a value.
		/// </summary>
		public int Length { get; private set; }
		/// <summary>
		/// The max capacity of the internal buffer.
		/// </summary>
		public int Capacity => _buffer.Length;
		/// <summary>
		/// The configuration of the writer.
		/// </summary>
		public SerialiserConfiguration SerialiserConfiguration { get; protected set; }

		protected byte[] _buffer = new byte[32];

		public readonly int Boolean = 1;
		public readonly int Byte = 1;
		public readonly int Int16 = 2;
		public readonly int Int32 = 4;
		public readonly int Int64 = 8;

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
            Type type = typeof(T);
            Write(val, type);
        }

        protected virtual void Write<T>(T val, Type type)
		{
            if (!_unknownTypes.Contains(type))
            {   
                if (_typeHandlerCache.TryGetValue(type, out Action<Writer, object> handler))
                {   // check for already cached type handler delegates
                    handler(this, val);
                    return;
                }

                Action<Writer, object> customHandler = CreateTypeHandlerDelegate(type, true);
                if (customHandler != null)
                {   // use custom type handler if user defined method was found
                    customHandler(this, val);
                    return;
                }

                // TODO : remove this once pre-compile cached generic handlers are supported
                Action<Writer, object> implementedHandler = CreateTypeHandlerDelegate(type);
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
        private static Action<Writer, object> CreateTypeHandlerDelegate(Type type, bool useCustomWriter = false)
        {   // find implemented or custom write method
            var writerMethod = useCustomWriter
                ?           type.GetMethod($"Write{SerialiserHelper.GetTypeName(type)}", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
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
                Array.Resize(ref _buffer, (_buffer.Length * 2) + size);
		}

		/// <summary>
		/// Skips the writer header ahead by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public virtual void Skip(int bytes)
		{
            AdjustBufferSize(bytes);
            Position += bytes;
            Length = Math.Max(Length, Position);
		}

		/// <summary>
		/// Reverts the writer header back by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public virtual void Revert(int bytes)
		{
			Position -= bytes;
			Position = Mathf.Max(Position, 0);
		}

		/// <summary>
		/// Clears the writter buffer.
		/// </summary>
		public virtual void Clear()
		{
            Position = 0;
            Length = 0;
		}

		/// <returns>The written buffer.</returns>
		public virtual byte[] GetBuffer()
        {
            byte[] result = new byte[Length];
            Array.Copy(_buffer, 0, result, 0, Length);
            return result;
        }

		/// <returns>The entire internal buffer.</returns>
		public virtual byte[] GetFullBuffer()
        {
            return _buffer;
        }

		/// <summary>
		/// Writes a specified number of bytes from a source array starting at a particular offset to the buffer.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="srcOffset"></param>
		/// <param name="count"></param>
		public virtual void BlockCopy(ref byte[] src, int srcOffset, int count)
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
		public virtual void WriteByteSegment(ArraySegment<byte> src)
        {
            byte[] srcArray = src.Array;
            BlockCopy(ref srcArray, 0, src.Count);
        }

		/// <summary>
		/// Writes a source array to the buffer.
		/// </summary>
		/// <param name="src"></param>
		public virtual void WriteByteArray(byte[] src)
        {
            BlockCopy(ref src, 0, src.Length);
        }

        #endregion

        #region primitives

        public virtual void WriteBoolean(bool val)
		{
            AdjustBufferSize(1);
            _buffer[Position++] = (byte)(val ? 1 : 0);
            Length = Math.Max(Length, Position);
		}

        public virtual void WriteByte(byte val)
        {
            AdjustBufferSize(1);
            _buffer[Position++] = val;
            Length = Math.Max(Length, Position);
        }

        public virtual void WriteSByte(sbyte val)
        {
            WriteByte((byte)val);
        }

        public virtual void WriteUInt16(ushort val)
        {
            AdjustBufferSize(2);
            _buffer[Position++] = (byte)val;
            _buffer[Position++] = (byte)(val >> 8);
            Length = Math.Max(Length, Position);
        }

        public virtual void WriteInt16(short val)
        {
            WriteUInt16((ushort)val);
        }

        public virtual void WriteUInt32(uint val)
        {
            AdjustBufferSize(4);
            _buffer[Position++] = (byte)val;
            _buffer[Position++] = (byte)(val >> 8);
            _buffer[Position++] = (byte)(val >> 16);
            _buffer[Position++] = (byte)(val >> 24);
            Length = Math.Max(Length, Position);
        }

        public virtual void WriteInt32(int val)
        {
            WriteUInt32((uint)val);
        }

        public virtual void WriteUInt64(ulong val)
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

        public virtual void WriteInt64(long val)
        {
            WriteUInt64((ulong)val);
        }

        public virtual void WriteChar(char val)
        {
            WriteUInt16(val);
        }

        public virtual void WriteSingle(float val)
        {
            TypeConverter.UIntToFloat converter = new() { Float = val };
            WriteUInt32(converter.UInt);
        }

        public virtual void WriteDouble(double val)
        {
            TypeConverter.ULongToDouble converter = new() { Double = val };
            WriteUInt64(converter.ULong);
        }

        public virtual void WriteDecimal(decimal val)
        {
            TypeConverter.ULongsToDecimal converter = new() { Decimal = val };
            WriteUInt64(converter.ULong1);
            WriteUInt64(converter.ULong2);
        }

        #endregion

        #region unity objects

        public virtual void WriteVector2(Vector2 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
        }

        public virtual void WriteVector3(Vector3 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
        }

        public virtual void WriteVector4(Vector4 val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
            WriteSingle(val.w);
        }

        public virtual void WriteQuaternion(Quaternion val)
        {
            WriteSingle(val.x);
            WriteSingle(val.y);
            WriteSingle(val.z);
            WriteSingle(val.w);
        }

        public virtual void WriteMatrix4x4(Matrix4x4 val)
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

        public virtual void WriteColor(Color val)
        {
            WriteByte((byte)(val.r * 100.0f));
            WriteByte((byte)(val.g * 100.0f));
            WriteByte((byte)(val.b * 100.0f));
            WriteByte((byte)(val.a * 100.0f));
        }

        public virtual void WriteColorWithoutAlpha(Color val)
        {
            WriteByte((byte)(val.r * 100.0f));
            WriteByte((byte)(val.g * 100.0f));
            WriteByte((byte)(val.b * 100.0f));
        }

        public virtual void WriteColor32(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
            WriteByte(val.a);
        }

        public virtual void WriteColor32WithoutAlpha(Color32 val)
        {
            WriteByte(val.r);
            WriteByte(val.g);
            WriteByte(val.b);
        }

        #endregion

        #region objects

        public virtual void WriteString(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                WriteByte(0);
                return;
            }

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

            WriteUInt16((ushort)val.Length);
            byte[] bytes = Encoding.ASCII.GetBytes(val);
            BlockCopy(ref bytes, 0, bytes.Length);
            Length = Math.Max(Length, Position);
        }

        public virtual void WriteStringWithoutFlag(string val)
        {
            if (string.IsNullOrEmpty(val))
			{
                WriteByte(0);
                return;
			}

            if (val.Length > ushort.MaxValue)
                throw new FormatException($"The string can't be longer than {ushort.MaxValue}!");

            byte[] bytes = Encoding.ASCII.GetBytes(val);
            BlockCopy(ref bytes, 0, bytes.Length);
            Length = Math.Max(Length, Position);
        }

        public virtual void WriteArray<T>(T[] val)
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

		public virtual void WriteList<T>(List<T> val)
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

        public virtual void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> val)
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

        public virtual void WriteDateTime(DateTime val)
		{
            WriteInt64(val.ToBinary());
		}

		#endregion
	}
}
