using SeaBattle.Models;
using SeaBattle.Services;
using System.Collections.ObjectModel;

namespace SeaBattle
{
    public partial class MainPage : ContentPage
    {
        private P2PServer p2pServer;
        private GameEngine gameEngine;
        private ObservableCollection<ServerInfo> availableServers = new ObservableCollection<ServerInfo>();
        private Dictionary<string, DateTime> serverLastSeen = new Dictionary<string, DateTime>();
        private bool isServerMode = false;
        private bool isGameOver = false; // Добавляем флаг окончания игры

        public MainPage()
        {
            InitializeComponent();

            p2pServer = new P2PServer("Player");
            gameEngine = new GameEngine();

            SetupEventHandlers();
            StartDiscovery();

            _ = ServerCleaner();
        }

        private void SetupEventHandlers()
        {
            p2pServer.client.MessageReceived += OnMessageReceived;
            p2pServer.server.MessageReceived += OnMessageReceived;
            p2pServer.server.ClientConnected += OnClientConnected;
            p2pServer.discovery.ServerFound += OnServerFound;

            gameEngine.GameStateChanged += OnGameStateChanged;
            gameEngine.BoardUpdated += OnBoardUpdated;
        }

        private void StartDiscovery()
        {
            try
            {
                p2pServer.discovery.Stop();
                p2pServer.discovery.StartListening();
                p2pServer.isListeningBroadcast = true;
                StatusLabel.Text = "Searching for servers...";
            }
            catch (Exception) { }
        }

        private async Task ServerCleaner()
        {
            while (true)
            {
                await Task.Delay(3000);

                if (p2pServer.isListeningBroadcast && !isServerMode)
                {
                    CleanOldServers();
                }
            }
        }

        private void CleanOldServers()
        {
            var now = DateTime.Now;
            var serversToRemove = new List<string>();

            foreach (var serverIp in serverLastSeen.Keys.ToList())
            {
                var lastSeen = serverLastSeen[serverIp];
                if ((now - lastSeen).TotalSeconds > 5)
                {
                    serversToRemove.Add(serverIp);
                }
            }

            foreach (var serverIp in serversToRemove)
            {
                serverLastSeen.Remove(serverIp);
                var serverToRemove = availableServers.FirstOrDefault(s => s.IP == serverIp);
                if (serverToRemove != null)
                {
                    availableServers.Remove(serverToRemove);
                }
            }
        }

        private async void OnStartServerClicked(object sender, EventArgs e)
        {
            try
            {
                p2pServer.StartServer();
                isServerMode = true;
                serverLastSeen.Clear();
                availableServers.Clear();
                p2pServer.connectedClientIp = "";
                isGameOver = false; // Сбрасываем флаг игры

                StartServerBtn.IsEnabled = false;
                StopServerBtn.IsEnabled = true;
                DisconnectBtn.IsEnabled = true;
                StatusLabel.Text = "Server running - waiting for connection";
                await DisplayAlert("Server", "Server started successfully", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to start server: {ex.Message}", "OK");
            }
        }

        private async void OnStopServerClicked(object sender, EventArgs e)
        {
            try
            {
                p2pServer.isServerMode = false;
                p2pServer.server.StopListening();
                p2pServer.discovery.Stop();
                isServerMode = false;
                p2pServer.connectedClientIp = "";
                isGameOver = false; // Сбрасываем флаг игры

                StartServerBtn.IsEnabled = true;
                StopServerBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = false;
                StatusLabel.Text = "Server stopped";

                await Task.Delay(500);
                StartDiscovery();

                await DisplayAlert("Server", "Server stopped", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to stop server: {ex.Message}", "OK");
            }
        }

        private async void OnDisconnectClicked(object sender, EventArgs e)
        {
            try
            {
                if (p2pServer.client.IsConnected)
                {
                    p2pServer.client = new TcpMessageClient();
                }

                p2pServer.connectedClientIp = "";
                isGameOver = false; // Сбрасываем флаг игры

                if (isServerMode)
                {
                    p2pServer.isServerMode = false;
                    p2pServer.server.StopListening();
                    isServerMode = false;
                }

                p2pServer.discovery.Stop();
                await Task.Delay(500);
                StartDiscovery();

                StartServerBtn.IsEnabled = true;
                StopServerBtn.IsEnabled = false;
                ReadyBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = false;
                RotateBtn.IsEnabled = false;
                StatusLabel.Text = "Disconnected";
                GameStatusLabel.Text = "Start server or connect to one";
                ShipsProgressLabel.Text = "Ships: 0/3";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to disconnect: {ex.Message}", "OK");
            }
        }

        private async void OnServerSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is ServerInfo server)
            {
                StatusLabel.Text = $"Connecting to {server.Name}...";
                bool connected = await p2pServer.ConnectToServer(server.IP);

                if (connected)
                {
                    StatusLabel.Text = $"Connected to {server.Name}";
                    ReadyBtn.IsEnabled = true;
                    DisconnectBtn.IsEnabled = true;
                    RotateBtn.IsEnabled = true;
                    isGameOver = false; // Сбрасываем флаг игры
                    InitializeGame();
                }
                else
                {
                    StatusLabel.Text = "Failed to connect";
                    await DisplayAlert("Error", "Failed to connect", "OK");
                    StartDiscovery();
                }

                ServersListView.SelectedItem = null;
            }
        }

        private void OnServerFound(string ip, string name)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isServerMode) return;

                serverLastSeen[ip] = DateTime.Now;

                var existingServer = availableServers.FirstOrDefault(s => s.IP == ip);
                if (existingServer == null)
                {
                    availableServers.Add(new ServerInfo { IP = ip, Name = name });
                    ServersListView.ItemsSource = availableServers.ToList();
                }
                else
                {
                    existingServer.Name = name;
                }

                StatusLabel.Text = $"Found {availableServers.Count} server(s)";
            });
        }

        private void OnClientConnected(string clientIp)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!isServerMode) return;

                p2pServer.connectedClientIp = clientIp;
                StatusLabel.Text = $"Client connected: {clientIp}";
                ReadyBtn.IsEnabled = true;
                DisconnectBtn.IsEnabled = true;
                RotateBtn.IsEnabled = true;
                isGameOver = false; // Сбрасываем флаг игры
                InitializeGame();
            });
        }

        private async void OnMessageReceived(string ip, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (isGameOver) return; // Игнорируем сообщения если игра окончена

                if (message.StartsWith("SHOT:"))
                {
                    var parts = message.Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                    {
                        bool hit = gameEngine.ProcessEnemyShot(x, y);
                        await p2pServer.SendMessage($"RESULT:{x}:{y}:{(hit ? "HIT" : "MISS")}");

                        // ИСПРАВЛЕНИЕ 1: После выстрела противника ход переходит к нам ТОЛЬКО если был промах
                        if (!hit)
                        {
                            gameEngine.SetPlayerTurn(true);
                            GameStatusLabel.Text = "Your turn - enemy missed!";
                        }
                        else
                        {
                            // Если попадание - противник продолжает ходить
                            GameStatusLabel.Text = "Enemy hit your ship! Their turn continues...";

                            if (gameEngine.CheckWinCondition())
                            {
                                isGameOver = true;
                                await p2pServer.SendMessage("WIN");
                                await DisplayAlert("Game Over", "You lost! All your ships are sunk.", "OK");
                                GameStatusLabel.Text = "Game Over - You lost!";
                            }
                        }
                    }
                }
                else if (message.StartsWith("RESULT:"))
                {
                    var parts = message.Split(':');
                    if (parts.Length == 4 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                    {
                        bool hit = parts[3] == "HIT";
                        gameEngine.MarkShotResult(x, y, hit);

                        // ИСПРАВЛЕНИЕ 1: Если промах - ход переходит к противнику
                        // Если попадание - остаемся на своем ходе
                        if (!hit)
                        {
                            gameEngine.SetPlayerTurn(false);
                            GameStatusLabel.Text = "You missed! Enemy's turn...";
                        }
                        else
                        {
                            GameStatusLabel.Text = "You hit enemy ship! Your turn continues...";

                            // Проверяем условие победы после нашего попадания
                            if (CheckEnemyWinCondition())
                            {
                                isGameOver = true;
                                await DisplayAlert("Game Over", "You won! All enemy ships are sunk.", "OK");
                                GameStatusLabel.Text = "Game Over - You won!";
                                // Не отправляем WIN сообщение, так как мы уже победили
                            }
                        }
                    }
                }
                else if (message == "READY")
                {
                    gameEngine.SetEnemyReady(true);
                }
                else if (message == "WIN")
                {
                    // ИСПРАВЛЕНИЕ 2: Получаем сообщение о победе противника
                    isGameOver = true;
                    await DisplayAlert("Game Over", "You lost! All your ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You lost!";
                }
            });
        }

        private bool CheckEnemyWinCondition()
        {
            // Более точная проверка победы - считаем попадания по вражеским кораблям
            int hitCount = 0;
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (gameEngine.EnemyBoard[x, y] == CellState.Hit)
                    {
                        hitCount++;
                    }
                }
            }
            // Всего клеток кораблей: 4 + 3 + 3 = 10
            return hitCount >= 10;
        }

        private void InitializeGame()
        {
            gameEngine.ResetGame();
            CreateGameBoards();
            UpdateShipsProgress();
            GameStatusLabel.Text = "Click on YOUR board to place ships. Use Rotate to change direction.";
            RotateBtn.IsEnabled = true;
            isGameOver = false; // Сбрасываем флаг игры
        }

        private void CreateGameBoards()
        {
            CreateGrid(PlayerGrid, true);
            CreateGrid(EnemyGrid, false);
        }

        private void CreateGrid(Grid grid, bool isPlayerGrid)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            for (int i = 0; i < 10; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    var button = new Button
                    {
                        BackgroundColor = Colors.LightBlue,
                        BorderColor = Colors.Black,
                        BorderWidth = 1,
                        CornerRadius = 0
                    };

                    if (isPlayerGrid)
                    {
                        int currentX = x;
                        int currentY = y;
                        button.Clicked += (s, e) => OnPlayerGridClicked(currentX, currentY);
                    }
                    else
                    {
                        int currentX = x;
                        int currentY = y;
                        button.Clicked += (s, e) => OnEnemyGridClicked(currentX, currentY);
                    }

                    grid.Children.Add(button);
                    Grid.SetRow(button, y);
                    Grid.SetColumn(button, x);
                }
            }

            UpdateBoardDisplay();
        }

        private void OnPlayerGridClicked(int x, int y)
        {
            if (isGameOver) return; // Игнорируем клики если игра окончена

            if (gameEngine.CurrentState == GameState.ShipPlacement)
            {
                bool placed = gameEngine.PlaceShip(x, y);
                if (placed)
                {
                    UpdateBoardDisplay();
                    UpdateShipsProgress();

                    if (gameEngine.AllShipsPlaced())
                    {
                        GameStatusLabel.Text = "All ships placed! Click Ready when done.";
                        RotateBtn.IsEnabled = false;
                    }
                    else
                    {
                        GameStatusLabel.Text = $"Ship placed! Place {gameEngine.TotalShipsCount - gameEngine.PlacedShipsCount} more ships.";
                    }
                }
                else
                {
                    DisplayAlert("Cannot Place Ship",
                        $"Cannot place {gameEngine.CurrentShipSize}-cell ship at position {x},{y}. Try another location.",
                        "OK");
                }
            }
        }

        private void UpdateShipsProgress()
        {
            ShipsProgressLabel.Text = $"Ships: {gameEngine.PlacedShipsCount}/{gameEngine.TotalShipsCount} " +
                                    $"(Current: {gameEngine.CurrentShipSize} cells, " +
                                    $"{(gameEngine.IsShipHorizontal ? "Horizontal" : "Vertical")})";
        }

        private async void OnEnemyGridClicked(int x, int y)
        {
            if (isGameOver) return; // Игнорируем клики если игра окончена

            if (gameEngine.CurrentState == GameState.PlayerTurn &&
                gameEngine.IsPlayerTurn &&
                gameEngine.EnemyBoard[x, y] == CellState.Empty)
            {
                await p2pServer.SendMessage($"SHOT:{x}:{y}");
                GameStatusLabel.Text = "Waiting for shot result...";
            }
        }

        private void OnRotateClicked(object sender, EventArgs e)
        {
            if (isGameOver) return; // Игнорируем клики если игра окончена
            gameEngine.RotateShip();
            UpdateShipsProgress();
        }

        private async void OnReadyClicked(object sender, EventArgs e)
        {
            if (isGameOver) return; // Игнорируем клики если игра окончена

            if (gameEngine.CurrentState == GameState.ShipPlacement && gameEngine.AllShipsPlaced())
            {
                gameEngine.SetPlayerReady(true);
                await p2pServer.SendMessage("READY");

                if (gameEngine.IsEnemyReady)
                {
                    gameEngine.StartGame();
                }
                else
                {
                    GameStatusLabel.Text = "Waiting for enemy...";
                }
            }
            else if (!gameEngine.AllShipsPlaced())
            {
                await DisplayAlert("Not Ready", $"Place all ships first! {gameEngine.TotalShipsCount - gameEngine.PlacedShipsCount} ships remaining.", "OK");
            }
        }

        private void OnGameStateChanged(GameState newState)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isGameOver) return; // Игнорируем если игра окончена

                switch (newState)
                {
                    case GameState.ShipPlacement:
                        GameStatusLabel.Text = "Place your ships - click on your board";
                        RotateBtn.IsEnabled = true;
                        ReadyBtn.IsEnabled = true;
                        break;
                    case GameState.PlayerTurn:
                        GameStatusLabel.Text = "Your turn - click enemy board!";
                        RotateBtn.IsEnabled = false;
                        ReadyBtn.IsEnabled = false;
                        break;
                    case GameState.EnemyTurn:
                        GameStatusLabel.Text = "Enemy's turn - waiting...";
                        RotateBtn.IsEnabled = false;
                        ReadyBtn.IsEnabled = false;
                        break;
                }
            });
        }

        private void OnBoardUpdated()
        {
            UpdateBoardDisplay();
        }

        private void UpdateBoardDisplay()
        {
            UpdateGrid(PlayerGrid, true);
            UpdateGrid(EnemyGrid, false);
        }

        private void UpdateGrid(Grid grid, bool isPlayerGrid)
        {
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    var button = grid.Children
                        .OfType<Button>()
                        .FirstOrDefault(b => Grid.GetRow(b) == y && Grid.GetColumn(b) == x);

                    if (button != null)
                    {
                        var cell = isPlayerGrid ?
                            gameEngine.PlayerBoard[x, y] :
                            gameEngine.EnemyBoard[x, y];

                        button.BackgroundColor = cell switch
                        {
                            CellState.Ship => Colors.Gray,
                            CellState.Hit => Colors.Red,
                            CellState.Miss => Colors.White,
                            _ => Colors.LightBlue
                        };
                    }
                }
            }
        }
    }
}