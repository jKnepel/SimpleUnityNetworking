using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public class Writer
    {
        #region fields

        public int Position;
        public int Length { get; private set; }
        public int Capacity => _buffer.Length;

        private byte[] _buffer = new byte[32];

        private static readonly ConcurrentDictionary<Type, Action<Writer, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

        #endregion

        #region lifecycle

        public Writer() {}

        static Writer()
        {   // caches all implemented type handlers during compilation
            // TODO : call this during compilation instead of on first writer mention
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

		public void Write<T>(T val)
        {
            Type type = typeof(T);
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
                string typeName = GetTypeName(type);
                throw new SerialiseNotImplemented($"No write method implemented for the type {typeName}!"
                    + $" Implement a Write{typeName} method or use an extension method in the parent type!");
			}

            foreach (FieldInfo fieldInfo in fieldInfos)
                Write(fieldInfo.GetValue(val), fieldInfo.FieldType);
        }

        private static string GetTypeName(Type type)
		{
            if (type.IsArray)
                return "Array";

            if (!type.IsGenericType)
                return type.Name;

            int index = type.Name.IndexOf("`");
            return type.Name.Substring(0, index);
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
                ?           type.GetMethod($"Write{GetTypeName(type)}", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : typeof(Writer).GetMethod($"Write{GetTypeName(type)}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
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

        public void AdjustBufferSize(int size)
		{
            if (Position + size > _buffer.Length)
                Array.Resize(ref _buffer, (_buffer.Length * 2) + size);
		}

        public void Skip(int val)
		{
            AdjustBufferSize(val);
            Position += val;
            Length = Math.Max(Length, Position);
		}

        public void Clear()
		{
            Position = 0;
            Length = 0;
		}

        public byte[] GetBuffer()
        {
            byte[] result = new byte[Length];
            Array.Copy(_buffer, 0, result, 0, Length);
            return result;
        }

        public byte[] GetFullBuffer()
        {
            return _buffer;
        }

        public void BlockCopy(ref byte[] src, int srcOffset, int count)
        {
            AdjustBufferSize(count);
            Buffer.BlockCopy(src, srcOffset, _buffer, Position, count);
            Position += count;
            Length = Math.Max(Length, Position);
        }

        #endregion

        #region primitives

        public void WriteBoolean(bool val)
		{
            AdjustBufferSize(1);
            _buffer[Position++] = (byte)(val ? 1 : 0);
            Length = Math.Max(Length, Position);
		}

        public void WriteByte(byte val)
        {
            AdjustBufferSize(1);
            _buffer[Position++] = val;
            Length = Math.Max(Length, Position);
        }

        public void WriteSByte(sbyte val)
        {
            AdjustBufferSize(1);
            _buffer[Position++] = (byte)val;
            Length = Math.Max(Length, Position);
        }

        public void WriteUInt16(ushort val)
        {
            AdjustBufferSize(2);
            _buffer[Position++] = (byte)val;
            _buffer[Position++] = (byte)(val >> 8);
            Length = Math.Max(Length, Position);
        }

        public void WriteInt16(short val)
        {
            WriteUInt16((ushort)val);
        }

        public void WriteUInt32(uint val)
        {
            AdjustBufferSize(4);
            _buffer[Position++] = (byte)val;
            _buffer[Position++] = (byte)(val >> 8);
            _buffer[Position++] = (byte)(val >> 16);
            _buffer[Position++] = (byte)(val >> 24);
            Length = Math.Max(Length, Position);
        }

        public void WriteInt32(int val)
        {
            WriteUInt32((uint)val);
        }

        public void WriteUInt64(ulong val)
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
            byte[] bytes = Encoding.ASCII.GetBytes(val);
            BlockCopy(ref bytes, 0, bytes.Length);
            Length = Math.Max(Length, Position);
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

            byte[] bytes = Encoding.ASCII.GetBytes(val);
            BlockCopy(ref bytes, 0, bytes.Length);
            Length = Math.Max(Length, Position);
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