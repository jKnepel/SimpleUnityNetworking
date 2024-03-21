using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Networking
{
	public class ClientInformation
	{
		public readonly uint ID;

		public string Username;
		public Color32 Colour;
		
		public ClientInformation(uint id, string username, Color32 colour)
		{
			ID = id;
			Username = username;
			Colour = colour;
		}

		public override string ToString()
		{
			return $"{ID}#{Username}";
		}

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType())
			{
				return false;
			}

			return ID.Equals(((ClientInformation)obj).ID);
		}

		public override int GetHashCode()
		{
			return (int)ID;
		}
	}
}
