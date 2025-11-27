using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SeaBattle.Services
{
    /// <summary>
    /// TCP клиент для отправки и получения сообщений
    /// </summary>
    public class TcpMessageClient
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;

        public event Action<string, string> MessageReceived;
        private bool is_reading = false;

        public bool IsConnected => client?.Connected == true;

        /// <summary>
        /// Подключение к серверу
        /// </summary>
        /// <param name="targetIp">IP адрес сервера</param>
        /// <param name="port">Порт для подключения</param>
        /// <returns>True если подключение успешно, иначе False</returns>
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

        /// <summary>
        /// Запуск процесса чтения сообщений от сервера
        /// </summary>
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

        /// <summary>
        /// Отправка сообщения на сервер
        /// </summary>
        /// <param name="message">Сообщение для отправки</param>
        /// <returns>True если сообщение отправлено успешно, иначе False</returns>
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

    /// <summary>
    /// TCP сервер для приема подключений и обработки сообщений
    /// </summary>
    public class TcpMessageServer
    {
        private int port = 8080;
        private bool is_listening = false;
        private TcpListener tcpListener;
        private Dictionary<string, TcpClient> connectedClients = new Dictionary<string, TcpClient>();

        public event Action<string, string> MessageReceived;
        public event Action<string> ClientConnected;

        /// <summary>
        /// Запуск прослушивания входящих подключений
        /// </summary>
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

        /// <summary>
        /// Остановка прослушивания и отключение всех клиентов
        /// </summary>
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

        /// <summary>
        /// Отправка сообщения конкретному клиенту
        /// </summary>
        /// <param name="clientIp">IP адрес клиента</param>
        /// <param name="message">Сообщение для отправки</param>
        /// <returns>True если сообщение отправлено успешно, иначе False</returns>
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

        /// <summary>
        /// Обработка подключенного клиента
        /// </summary>
        /// <param name="client">TCP клиент</param>
        /// <param name="clientIp">IP адрес клиента</param>
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