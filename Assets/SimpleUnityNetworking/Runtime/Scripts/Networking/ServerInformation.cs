namespace jKnepel.SimpleUnityNetworking.Networking
{
    public class ServerInformation
    {
        public string Servername;
        public int MaxNumberConnectedClients;

        public ServerInformation(string servername, int maxNumberConnectedClients)
        {
            Servername = servername;
            MaxNumberConnectedClients = maxNumberConnectedClients;
        }
    }
}
