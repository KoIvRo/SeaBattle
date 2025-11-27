using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SeaBattle.Services
{
    /// <summary>
    /// Сервис обнаружения серверов в локальной сети
    /// </summary>
    public class ServerDiscovery
    {
        private const int DiscoveryPort = 8081;
        private bool isListening = false;
        private bool isBroadcasting = false;
        private CancellationTokenSource cancellation;
        private UdpClient udpClient;
        private string localUsername = "user";

        public event Action<string, string> ServerFound;

        /// <summary>
        /// Запуск широковещательной рассылки для объявления сервера
        /// </summary>
        /// <param name="username">Имя пользователя/сервера</param>
        public async Task StartBroadcasting(string username = "user")
        {
            if (isBroadcasting)
            {
                return;
            }

            try
            {
                isBroadcasting = true;
                localUsername = username;
                cancellation = new CancellationTokenSource();

                while (isBroadcasting && !cancellation.IsCancellationRequested)
                {
                    SendBroadcast();
                    await Task.Delay(2000);
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Отправка широковещательного сообщения о наличии сервера
        /// </summary>
        private void SendBroadcast()
        {
            using (UdpClient client = new UdpClient())
            {
                client.EnableBroadcast = true;
                byte[] data = Encoding.UTF8.GetBytes($"SERVER:{localUsername}");
                IPAddress broadcastAddress = new IPAddress(new byte[] { 172, 20, 10, 15 });
                client.Send(data, data.Length, new IPEndPoint(broadcastAddress, DiscoveryPort));

                broadcastAddress = new IPAddress(new byte[] { 172, 20, 10, 255 });
                client.Send(data, data.Length, new IPEndPoint(broadcastAddress, DiscoveryPort));

                broadcastAddress = new IPAddress(new byte[] { 255, 255, 255, 255 });
                client.Send(data, data.Length, new IPEndPoint(broadcastAddress, DiscoveryPort));
            }
        }

        /// <summary>
        /// Запуск прослушивания широковещательных сообщений от серверов
        /// </summary>
        public async Task StartListening()
        {
            if (isListening) return;

            try
            {
                isListening = true;
                cancellation = new CancellationTokenSource();
                udpClient = new UdpClient(DiscoveryPort);
                udpClient.EnableBroadcast = true;

                while (isListening && !cancellation.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                    try
                    {
                        var result = await udpClient.ReceiveAsync(cancellation.Token);
                        string msg = Encoding.UTF8.GetString(result.Buffer);
                        string remoteIp = result.RemoteEndPoint.Address.ToString();

                        if (msg.StartsWith("SERVER"))
                        {
                            string[] parts = msg.Split(':', 2);
                            string username = parts.Length > 1 ? parts[1] : "unknown";
                            ServerFound?.Invoke(remoteIp, username);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }
                }
            }
            catch
            {
                isListening = false;
            }
        }

        /// <summary>
        /// Остановка всех операций обнаружения серверов
        /// </summary>
        public void Stop()
        {
            try
            {
                isBroadcasting = false;
                isListening = false;
                cancellation?.Cancel();
                udpClient?.Close();
                udpClient = null;
            }
            catch { }
        }
    }
}