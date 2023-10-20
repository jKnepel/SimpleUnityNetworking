using System;
using System.Collections.Concurrent;
using System.Net;
using UnityEngine;
using jKnepel.SimpleUnityNetworking.Networking.Packets;
using jKnepel.SimpleUnityNetworking.Serialisation;

namespace jKnepel.SimpleUnityNetworking.Networking
{
	public class ClientInformation
	{
		public readonly byte ID;

		public string Username = "Username";
		public Color32 Color = new(255, 255, 255, 255);
		public bool IsHost => ID == 1;

		public ClientInformation(byte id, string username, Color32 color)
		{
			ID = id;
			Username = username;
			Color = color;
		}

		public override string ToString()
		{
			return $"{ID}#{Username}";
		}

		public override bool Equals(object obj)
		{
			if ((obj == null) || !GetType().Equals(obj.GetType()))
			{
				return false;
			}
			else
			{
				return ID.Equals(((ClientInformation)obj).ID);
			}
		}

		public override int GetHashCode()
		{
			return ID;
		}
	}

	internal class ClientInformationSocket : ClientInformation
	{
		public IPEndPoint Endpoint { get; private set; }
		public DateTime LastHeartbeat { get; set; }

		internal readonly ConcurrentDictionary<ushort, (PacketHeader, byte[])> ReceivedPacketsBuffer = new();
		internal readonly ConcurrentDictionary<ushort, ConcurrentDictionary<ushort, byte[]>> ReceivedChunksBuffer = new();

		internal readonly ConcurrentDictionary<ushort, byte[]> SendPacketsBuffer = new();
		internal readonly ConcurrentDictionary<(ushort, ushort), byte[]> SendChunksBuffer = new();

		internal ushort UnreliableLocalSequence { get; set; }
		internal ushort UnreliableRemoteSequence { get; set; }
		internal ushort ReliableLocalSequence { get; set; }
		internal ushort ReliableRemoteSequence { get; set; }
		
		public ClientInformationSocket(byte id, IPEndPoint endpoint, string username, Color32 color) : base(id, username, color)
		{
			Endpoint = endpoint;
			LastHeartbeat = DateTime.Now;

			UnreliableLocalSequence = 0;
			UnreliableRemoteSequence = 0;
			ReliableLocalSequence = 0;
			ReliableRemoteSequence = 0;
		}

	}
}
