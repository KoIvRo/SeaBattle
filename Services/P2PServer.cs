namespace SeaBattle.Services
{
    public class P2PServer
    {
        /*
        События:
           listener.MessageReceived += OnMessageReceived;
           listener.ClientConnected += OnClientConnected;
           sender.MessageReceived += OnMessageReceived;
        StartServer() запускать когда ты сервак
        ConnectToServer(IP сервака) когда ты клиент
        StartDiscovering() при запуске приложения, пока ты не сервак и не клиент
        Вообще переключение между сервером и клиентом должно работать нормально, но я не тестил
        */
        public TcpMessageClient client;
        public TcpMessageServer server;
        public ServerDiscovery discovery;
        public bool isServerMode = false;
        public string connectedClientIp = "";
        public bool isBroadcasting = false;
        public bool isListeningBroadcast = false;
        public string username = "user";

        public P2PServer(string name)
        {
            client = new TcpMessageClient();
            server = new TcpMessageServer();
            discovery = new ServerDiscovery();
            username = name;
        }

        public async void StartServer()
        {
            try
            {
                if (isServerMode)
                {
                    return;
                }

                StopDiscovery();
                isServerMode = true;
                isBroadcasting = true;
                server.StartListeningAsync();
                discovery.StartBroadcasting(username);
            }
            catch (Exception)
            {

            }
        }

        public async Task<bool> ConnectToServer(string IP)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(IP))
                {
                    return false;
                }

                isListeningBroadcast = false;
                StopDiscovery();

                if (isServerMode)
                {
                    isServerMode = false;
                    server.StopListening();
                    await Task.Delay(100);
                }

                string targetIp = IP.Trim();
                bool connected = await client.ConnectToServer(targetIp, 8080);

                if (!connected)
                {
                    isListeningBroadcast = true;
                    discovery.Stop();
                    await Task.Delay(100);
                    discovery.StartListening();
                }

                return connected;
            }
            catch (Exception)
            {
                isListeningBroadcast = true;
                discovery.Stop();
                await Task.Delay(100);
                discovery.StartListening();
                return false;
            }
        }

        public async Task<bool> SendMessage(string Text)
        {
            try
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return false;
                }

                if (client.IsConnected)
                {
                    bool sent = await client.SendMessageAsync(Text.Trim());
                    return sent;
                }
                else if (isServerMode && !string.IsNullOrEmpty(connectedClientIp))
                {
                    bool sent = await server.SendMessageToClient(connectedClientIp, Text.Trim());
                    return sent;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }


        private void StopDiscovery()
        {
            discovery.Stop();
            isBroadcasting = false;
            isListeningBroadcast = false;
        }
    }
}