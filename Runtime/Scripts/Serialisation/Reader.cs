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
    public class Reader
    {
		#region fields

		/// <summary>
		/// The current byte position of the writer header within the buffer.
		/// </summary>
		/// <remarks>
		/// IMPORTANT! Do not use this position unless you know what you are doing! 
		/// Setting the position manually will not check for buffer bounds.
		/// </remarks>
		public int Position;
		/// <summary>
		/// The length of the given buffer in bytes.
		/// </summary>
		public int Length => _buffer.Length;
		/// <summary>
		/// The remaining positions until the full length of the buffer.
		/// </summary>
		public int Remaining => Length - Position;
		/// <summary>
		/// The configuration of the reader.
		/// </summary>
		public SerialiserConfiguration SerialiserConfiguration { get; }

		protected byte[] _buffer;

		public readonly int Boolean = 1;
		public readonly int Byte = 1;
		public readonly int Int16 = 2;
		public readonly int Int32 = 4;
		public readonly int Int64 = 8;

        private static readonly ConcurrentDictionary<Type, Func<Reader, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

		#endregion

		#region lifecycle

		public Reader(byte[] bytes, SerialiserConfiguration config = null) 
            : this(new ArraySegment<byte>(bytes), config) { }

        public Reader(ArraySegment<byte> bytes, SerialiserConfiguration config = null)
		{
            if (bytes.Array == null)
                return;

            Position = bytes.Offset;
            _buffer = bytes.Array;
            SerialiserConfiguration = config ?? new();
		}

        static Reader()
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

        protected virtual object Read(Type type)
		{
            if (!_unknownTypes.Contains(type))
			{
                if (_typeHandlerCache.TryGetValue(type, out Func<Reader, object> handler))
                {   // check for already cached type handler delegates
                    return handler(this);
                }

                Func<Reader, object> customHandler = CreateTypeHandlerDelegate(type, true);
                if (customHandler != null)
                {   // use custom type handler if user defined method was found
                    return customHandler(this);
                }

                // TODO : remove this once pre-compile cached generic handlers are supported
                Func<Reader, object> implementedHandler = CreateTypeHandlerDelegate(type, false);
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
        private static Func<Reader, object> CreateTypeHandlerDelegate(Type type, bool useCustomReader = false)
        {   // find implemented or custom read method
            var readerMethod = useCustomReader
                ?           type.GetMethod($"Read{SerialiserHelper.GetTypeName(type)}", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : typeof(Reader).GetMethod($"Read{SerialiserHelper.GetTypeName(type)}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (readerMethod == null)
                return null;

            // parameters
            var instanceArg = Expression.Parameter(typeof(Reader), "instance");

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
            var lambda = Expression.Lambda<Func<Reader, object>>(castResult, instanceArg);
            var action = lambda.Compile();
            _typeHandlerCache.TryAdd(type, action);
            return action;
        }

		#endregion

		#region helpers

		/// <summary>
		/// Skips the reader header ahead by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public virtual void Skip(int bytes)
		{
            if (bytes < 1 || bytes > Remaining)
                return;

            Position += bytes;
		}

		/// <summary>
		/// Reverts the reader header back by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public virtual void Revert(int bytes)
        {
            Position -= bytes; 
            Position = Mathf.Max(Position, 0);
        }

		/// <summary>
		/// Clears the reader buffer.
		/// </summary>
		public virtual void Clear()
		{
            Position += Remaining;
		}

		/// <returns>The full internal buffer.</returns>
		public virtual byte[] GetFullBuffer()
		{
            return _buffer;
		}

		/// <summary>
		/// Reads a specified number of bytes from the internal buffer to a destination array starting at a particular offset.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="dstOffset"></param>
		/// <param name="count"></param>
		public virtual void BlockCopy(ref byte[] dst, int dstOffset, int count)
		{
            Buffer.BlockCopy(_buffer, Position, dst, dstOffset, count);
            Position += count;
		}

		/// <returns>Reads and returns a byte segment of the specified length.</returns>
		public virtual ArraySegment<byte> ReadByteSegment(int count)
		{
            if (count > Remaining)
                throw new IndexOutOfRangeException("The count exceeds the remaining length!");

            ArraySegment<byte> result = new(_buffer, Position, count);
            Position += count;
            return result;
        }

		/// <returns>Reads and returns a byte array of the specified length.</returns>
        public virtual byte[] ReadByteArray(int count)
        {
            return ReadByteSegment(count).ToArray();
        }

		/// <returns>The remaining bytes.</returns>
		public virtual byte[] ReadRemainingBuffer()
		{
            byte[] remaining = new byte[Remaining];
            BlockCopy(ref remaining, 0, Remaining);
            return remaining;
		}

		#endregion

		#region primitives

        public virtual bool ReadBoolean()
		{
            byte result = _buffer[Position++];
            return result == 1;
        }

        public virtual byte ReadByte()
		{
            byte result = _buffer[Position++];
            return result;
		}

        public virtual sbyte ReadSByte()
		{
            sbyte result = (sbyte)_buffer[Position++];
            return result;
		}

        public virtual ushort ReadUInt16()
		{
            ushort result = _buffer[Position++];
            result |= (ushort)(_buffer[Position++] << 8);
            return result;
		}

        public virtual short ReadInt16()
		{
            return (short)ReadUInt16();
		}

        public virtual uint ReadUInt32()
		{
            uint result = _buffer[Position++];
            result |= (uint)(_buffer[Position++] << 8);
            result |= (uint)(_buffer[Position++] << 16);
            result |= (uint)(_buffer[Position++] << 24);
            return result;
		}

        public virtual int ReadInt32()
		{
            return (int)ReadUInt32();
		}

        public virtual ulong ReadUInt64()
		{
            ulong result = _buffer[Position++];
            result |= (ulong)_buffer[Position++] << 8;
            result |= (ulong)_buffer[Position++] << 16;
            result |= (ulong)_buffer[Position++] << 24;
            result |= (ulong)_buffer[Position++] << 32;
            result |= (ulong)_buffer[Position++] << 40;
            result |= (ulong)_buffer[Position++] << 48;
            result |= (ulong)_buffer[Position++] << 56;
            return result;
        }

        public virtual long ReadInt64()
		{
            return (long)ReadUInt64();
		}

        public virtual char ReadChar()
		{
            char result = (char)_buffer[Position++];
            result |= (char)(_buffer[Position++] << 8);
            return result;
        }

        public virtual float ReadSingle()
		{
            TypeConverter.UIntToFloat converter = new() { UInt = ReadUInt32() };
            return converter.Float;
        }

        public virtual double ReadDouble()
		{
            TypeConverter.ULongToDouble converter = new() { ULong = ReadUInt64() };
            return converter.Double;
        }

        public virtual decimal ReadDecimal()
		{
            TypeConverter.ULongsToDecimal converter = new() { ULong1 = ReadUInt64(), ULong2 = ReadUInt64() };
            return converter.Decimal;
        }

		#endregion

		#region unity objects

        public virtual Vector2 ReadVector2()
		{
            return new Vector2(ReadSingle(), ReadSingle());
		}

        public virtual Vector3 ReadVector3()
		{
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
		}

        public virtual Vector4 ReadVector4()
		{
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
		}

        public virtual Quaternion ReadQuaternion()
		{
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
		}

        public virtual Matrix4x4 ReadMatrix4x4()
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

        public virtual Color ReadColor()
		{
            float r = (float)(ReadByte() / 100.0f);
            float g = (float)(ReadByte() / 100.0f);
            float b = (float)(ReadByte() / 100.0f);
            float a = (float)(ReadByte() / 100.0f);
            return new Color(r, g, b, a);
		}

        public virtual Color ReadColorWithoutAlpha()
		{
            float r = (float)(ReadByte() / 100.0f);
            float g = (float)(ReadByte() / 100.0f);
            float b = (float)(ReadByte() / 100.0f);
            return new Color(r, g, b, 1);
        }

        public virtual Color32 ReadColor32()
		{
            return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
		}

        public virtual Color32 ReadColor32WithoutAlpha()
		{
            return new Color32(ReadByte(), ReadByte(), ReadByte(), 255);
        }

		#endregion

		#region objects

        public virtual string ReadString()
		{
            ushort length = ReadUInt16();
            return Encoding.ASCII.GetString(ReadByteArray(length));
		}

        public virtual string ReadStringWithoutFlag(int length)
        {
            return Encoding.ASCII.GetString(ReadByteArray(length));
        }

        public virtual T[] ReadArray<T>()
		{
            int length = ReadInt32();
            T[] array = new T[length];
            for (int i = 0; i < length; i++)
                array[i] = Read<T>();
            return array;
		}

        public virtual List<T> ReadList<T>()
		{
            int count = ReadInt32();
            List<T> list = new(count);
            for (int i = 0; i < count; i++)
                list.Add(Read<T>());
            return list;
        }

        public virtual Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>()
		{
            int count = ReadInt32();
            Dictionary<TKey, TValue> dictionary = new(count);
            for (int i = 0; i < count; i++)
                dictionary.Add(Read<TKey>(), Read<TValue>());
            return dictionary;
        }

        public virtual DateTime ReadDateTime()
		{
            return DateTime.FromBinary(ReadInt64());
		}

        #endregion
    }
}
