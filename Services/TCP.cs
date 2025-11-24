using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SeaBattle.Services
{
    public class TcpMessageClient
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;

        public event Action<string, string> MessageReceived;
        private bool is_reading = false;

        public bool IsConnected => client?.Connected == true;

        public async Task<bool> ConnectToServer(string targetIp, int port)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(targetIp, port);
                var stream = client.GetStream();
                writer = new StreamWriter(stream, Encoding.UTF8);
                reader = new StreamReader(stream, Encoding.UTF8);


                StartReading();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void StartReading()
        {
            if (is_reading) return;
            is_reading = true;

            try
            {
                while (IsConnected && is_reading)
                {
                    string message = await reader.ReadLineAsync();
                    if (message == null) break;
                    string localIp = ((IPEndPoint)client.Client.LocalEndPoint).Address.ToString();
                    MessageReceived?.Invoke(localIp, message);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                is_reading = false;
            }
        }

        public async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                if (!IsConnected || writer == null)
                    return false;

                await writer.WriteLineAsync(message);
                await writer.FlushAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public class TcpMessageServer
    {
        private int port = 8080;
        private bool is_listening = false;
        private TcpListener tcpListener;
        private Dictionary<string, TcpClient> connectedClients = new Dictionary<string, TcpClient>();

        public event Action<string, string> MessageReceived;
        public event Action<string> ClientConnected;

        public async Task StartListeningAsync()
        {
            if (is_listening) return;

            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            is_listening = true;

            while (is_listening)
            {
                try
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    connectedClients[clientIp] = client;
                    ClientConnected?.Invoke(clientIp);

                    _ = Task.Run(() => HandleClientAsync(client, clientIp));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        public void StopListening()
        {
            is_listening = false;

            foreach (var client in connectedClients.Values)
            {
                client?.Close();
            }
            connectedClients.Clear();

            tcpListener?.Stop();
        }

        public async Task<bool> SendMessageToClient(string clientIp, string message)
        {
            try
            {
                if (connectedClients.TryGetValue(clientIp, out TcpClient client) && client.Connected)
                {
                    var stream = client.GetStream();
                    var writer = new StreamWriter(stream, Encoding.UTF8);

                    await writer.WriteLineAsync(message);
                    await writer.FlushAsync();
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task HandleClientAsync(TcpClient client, string clientIp)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (client.Connected && is_listening)
                    {
                        string message = await reader.ReadLineAsync();
                        if (message == null) break;
                        MessageReceived?.Invoke(clientIp, message);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                connectedClients.Remove(clientIp);
                client?.Close();
            }
        }
    }
}