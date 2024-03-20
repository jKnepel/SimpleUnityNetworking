namespace jKnepel.SimpleUnityNetworking.Networking
{
    public class ServerInformation
    {
        public string Servername;
        public uint MaxNumberConnectedClients;

        public ServerInformation(string servername, uint maxNumberConnectedClients)
        {
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
        }
    }
}
