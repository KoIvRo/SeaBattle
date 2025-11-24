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
        private bool isGameOver = false;

        public MainPage()
        {
            InitializeComponent();

            p2pServer = new P2PServer("Player");
            gameEngine = new GameEngine();

            SetupEventHandlers();
            StartDiscovery();

            _ = ServerCleaner();

            // Скрываем специальные атаки изначально
            SpecialAttacksStack.IsVisible = false;
        }

        private void SetupEventHandlers()
        {
            p2pServer.client.MessageReceived += OnMessageReceived;
            p2pServer.server.MessageReceived += OnMessageReceived;
            p2pServer.server.ClientConnected += OnClientConnected;
            p2pServer.discovery.ServerFound += OnServerFound;

            gameEngine.GameStateChanged += OnGameStateChanged;
            gameEngine.BoardUpdated += OnBoardUpdated;
            gameEngine.SpecialAttacksUpdated += OnSpecialAttacksUpdated;
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
                isGameOver = false;

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
                isGameOver = false;

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
                isGameOver = false;

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
                SpecialAttacksStack.IsVisible = false;
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
                    isGameOver = false;
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
                isGameOver = false;
                InitializeGame();
            });
        }

        private async void OnMessageReceived(string ip, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (isGameOver) return;

                if (message.StartsWith("SHOT:"))
                {
                    var parts = message.Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
                    {
                        bool hit = gameEngine.ProcessEnemyShot(x, y);
                        await p2pServer.SendMessage($"RESULT:{x}:{y}:{(hit ? "HIT" : "MISS")}");

                        if (!hit)
                        {
                            gameEngine.SetPlayerTurn(true);
                            GameStatusLabel.Text = "Your turn - enemy missed!";
                        }
                        else
                        {
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

                        if (!hit)
                        {
                            gameEngine.SetPlayerTurn(false);
                            GameStatusLabel.Text = "You missed! Enemy's turn...";
                        }
                        else
                        {
                            GameStatusLabel.Text = "You hit enemy ship! Your turn continues...";

                            if (CheckEnemyWinCondition())
                            {
                                isGameOver = true;
                                await DisplayAlert("Game Over", "You won! All enemy ships are sunk.", "OK");
                                GameStatusLabel.Text = "Game Over - You won!";
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
                    isGameOver = true;
                    await DisplayAlert("Game Over", "You lost! All your ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You lost!";
                }
                // Обработка специальных атак от противника
                else if (message.StartsWith("SPECIAL:"))
                {
                    var parts = message.Split(':');
                    if (parts.Length >= 4)
                    {
                        string attackType = parts[1];
                        int startX = int.Parse(parts[2]);
                        int startY = int.Parse(parts[3]);

                        // Отображаем специальную атаку на нашей доске
                        await ProcessEnemySpecialAttack(attackType, startX, startY);
                    }
                }
                // Обработка результатов специальной атаки от противника
                else if (message.StartsWith("SPECIAL_RESULT:"))
                {
                    var parts = message.Split(':');
                    if (parts.Length >= 2)
                    {
                        bool hit = parts[1] == "HIT";

                        if (!hit)
                        {
                            // ПРИ ПРОМАХЕ - передаем ход противнику
                            gameEngine.SetPlayerTurn(false);
                            GameStatusLabel.Text = "Your special attack missed! Enemy's turn!";
                        }
                        else
                        {
                            // После попадания специальной атакой - ход продолжается у текущего игрока
                            GameStatusLabel.Text = "Your special attack hit! Your turn continues...";
                            gameEngine.SetPlayerTurn(true);

                            if (CheckEnemyWinCondition())
                            {
                                isGameOver = true;
                                await DisplayAlert("Game Over", "You won! All enemy ships are sunk.", "OK");
                                GameStatusLabel.Text = "Game Over - You won!";
                            }
                        }
                    }
                }
            });
        }

        private async Task ProcessEnemySpecialAttack(string attackType, int startX, int startY)
        {
            string attackName = attackType switch
            {
                "HorizontalLine" => "Horizontal Line",
                "VerticalLine" => "Vertical Line",
                "Area3x3" => "3x3 Area",
                _ => "Special Attack"
            };

            // Обрабатываем специальную атаку на нашей доске
            bool hitSomething = false;

            switch (attackType)
            {
                case "HorizontalLine":
                    hitSomething = ProcessHorizontalLineAttackOnPlayer(startX, startY);
                    break;
                case "VerticalLine":
                    hitSomething = ProcessVerticalLineAttackOnPlayer(startX, startY);
                    break;
                case "Area3x3":
                    hitSomething = ProcessArea3x3AttackOnPlayer(startX, startY);
                    break;
            }

            // Отправляем результат специальной атаки
            await p2pServer.SendMessage($"SPECIAL_RESULT:{(hitSomething ? "HIT" : "MISS")}");

            // Обновляем доску чтобы показать атаку
            UpdateBoardDisplay();

            if (!hitSomething)
            {
                // ПРИ ПРОМАХЕ - передаем ход игроку
                gameEngine.SetPlayerTurn(true);
                GameStatusLabel.Text = $"Enemy used {attackName} attack and missed! Your turn!";
            }
            else
            {
                GameStatusLabel.Text = $"Enemy used {attackName} attack and hit! Their turn continues...";

                if (gameEngine.CheckWinCondition())
                {
                    isGameOver = true;
                    await p2pServer.SendMessage("WIN");
                    await DisplayAlert("Game Over", "You lost! All your ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You lost!";
                }
            }
        }

        private bool ProcessHorizontalLineAttackOnPlayer(int startX, int startY)
        {
            bool hitSomething = false;

            // Влево от стартовой точки
            for (int x = startX; x >= 0; x--)
            {
                // Пропускаем уже отмеченные клетки (продолжаем движение)
                if (gameEngine.PlayerBoard[x, startY] == CellState.Hit || gameEngine.PlayerBoard[x, startY] == CellState.Miss)
                    continue;

                if (gameEngine.PlayerBoard[x, startY] == CellState.Ship)
                {
                    gameEngine.PlayerBoard[x, startY] = CellState.Hit;
                    hitSomething = true;
                    break; // Останавливаемся после первого попадания
                }
                else if (gameEngine.PlayerBoard[x, startY] == CellState.Empty)
                {
                    gameEngine.PlayerBoard[x, startY] = CellState.Miss;
                }
            }

            // Вправо от стартовой точки (только если не было попадания в левой части)
            if (!hitSomething)
            {
                for (int x = startX + 1; x < 10; x++)
                {
                    // Пропускаем уже отмеченные клетки (продолжаем движение)
                    if (gameEngine.PlayerBoard[x, startY] == CellState.Hit || gameEngine.PlayerBoard[x, startY] == CellState.Miss)
                        continue;

                    if (gameEngine.PlayerBoard[x, startY] == CellState.Ship)
                    {
                        gameEngine.PlayerBoard[x, startY] = CellState.Hit;
                        hitSomething = true;
                        break; // Останавливаемся после первого попадания
                    }
                    else if (gameEngine.PlayerBoard[x, startY] == CellState.Empty)
                    {
                        gameEngine.PlayerBoard[x, startY] = CellState.Miss;
                    }
                }
            }

            return hitSomething;
        }

        private bool ProcessVerticalLineAttackOnPlayer(int startX, int startY)
        {
            bool hitSomething = false;

            // Вверх от стартовой точки
            for (int y = startY; y >= 0; y--)
            {
                // Пропускаем уже отмеченные клетки (продолжаем движение)
                if (gameEngine.PlayerBoard[startX, y] == CellState.Hit || gameEngine.PlayerBoard[startX, y] == CellState.Miss)
                    continue;

                if (gameEngine.PlayerBoard[startX, y] == CellState.Ship)
                {
                    gameEngine.PlayerBoard[startX, y] = CellState.Hit;
                    hitSomething = true;
                    break; // Останавливаемся после первого попадания
                }
                else if (gameEngine.PlayerBoard[startX, y] == CellState.Empty)
                {
                    gameEngine.PlayerBoard[startX, y] = CellState.Miss;
                }
            }

            // Вниз от стартовой точки (только если не было попадания в верхней части)
            if (!hitSomething)
            {
                for (int y = startY + 1; y < 10; y++)
                {
                    // Пропускаем уже отмеченные клетки (продолжаем движение)
                    if (gameEngine.PlayerBoard[startX, y] == CellState.Hit || gameEngine.PlayerBoard[startX, y] == CellState.Miss)
                        continue;

                    if (gameEngine.PlayerBoard[startX, y] == CellState.Ship)
                    {
                        gameEngine.PlayerBoard[startX, y] = CellState.Hit;
                        hitSomething = true;
                        break; // Останавливаемся после первого попадания
                    }
                    else if (gameEngine.PlayerBoard[startX, y] == CellState.Empty)
                    {
                        gameEngine.PlayerBoard[startX, y] = CellState.Miss;
                    }
                }
            }

            return hitSomething;
        }

        private bool ProcessArea3x3AttackOnPlayer(int centerX, int centerY)
        {
            bool hitSomething = false;

            for (int x = Math.Max(0, centerX - 1); x <= Math.Min(9, centerX + 1); x++)
            {
                for (int y = Math.Max(0, centerY - 1); y <= Math.Min(9, centerY + 1); y++)
                {
                    // Пропускаем уже отмеченные клетки
                    if (gameEngine.PlayerBoard[x, y] == CellState.Hit || gameEngine.PlayerBoard[x, y] == CellState.Miss)
                        continue;

                    if (gameEngine.PlayerBoard[x, y] == CellState.Ship)
                    {
                        gameEngine.PlayerBoard[x, y] = CellState.Hit;
                        hitSomething = true;
                    }
                    else if (gameEngine.PlayerBoard[x, y] == CellState.Empty)
                    {
                        gameEngine.PlayerBoard[x, y] = CellState.Miss;
                    }
                }
            }

            return hitSomething;
        }

        private bool CheckEnemyWinCondition()
        {
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
            return hitCount >= 10;
        }

        private void InitializeGame()
        {
            gameEngine.ResetGame();
            CreateGameBoards();
            UpdateShipsProgress();
            UpdateSpecialAttacks();
            GameStatusLabel.Text = "Click on YOUR board to place ships. Use Rotate to change direction.";
            RotateBtn.IsEnabled = true;
            isGameOver = false;
            SpecialAttacksStack.IsVisible = false;
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
            if (isGameOver) return;

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

        private async void OnEnemyGridClicked(int x, int y)
        {
            if (isGameOver) return;

            if (gameEngine.CurrentState == GameState.PlayerTurn && gameEngine.IsPlayerTurn)
            {
                // Проверяем, используется ли специальная атака
                if (gameEngine.SelectedSpecialAttack != SpecialAttack.None)
                {
                    await ExecuteSpecialAttack(x, y);
                }
                else if (gameEngine.EnemyBoard[x, y] == CellState.Empty)
                {
                    // Обычный выстрел
                    await p2pServer.SendMessage($"SHOT:{x}:{y}");
                    GameStatusLabel.Text = "Waiting for shot result...";
                }
            }
        }

        private async Task ExecuteSpecialAttack(int x, int y)
        {
            (int x, int y, bool hit)[] shots = Array.Empty<(int, int, bool)>();
            string attackName = "";
            string attackType = "";

            switch (gameEngine.SelectedSpecialAttack)
            {
                case SpecialAttack.LineHorizontal:
                    shots = gameEngine.ExecuteLineHorizontalAttack(x, y);
                    attackName = "Horizontal Line";
                    attackType = "HorizontalLine";
                    break;
                case SpecialAttack.LineVertical:
                    shots = gameEngine.ExecuteLineVerticalAttack(x, y);
                    attackName = "Vertical Line";
                    attackType = "VerticalLine";
                    break;
                case SpecialAttack.Area3x3:
                    shots = gameEngine.ExecuteArea3x3Attack(x, y);
                    attackName = "3x3 Area";
                    attackType = "Area3x3";
                    break;
            }

            // Отправляем информацию о специальной атаке противнику
            await p2pServer.SendMessage($"SPECIAL:{attackType}:{x}:{y}");

            // СРАЗУ обновляем доску чтобы показать куда попали
            UpdateBoardDisplay();
            UpdateSpecialAttacks();

            // Обрабатываем результаты выстрелов
            bool hitSomething = shots.Any(shot => shot.hit);
            int hitCount = shots.Count(shot => shot.hit);

            if (hitCount > 0)
            {
                GameStatusLabel.Text = $"{attackName} attack hit {hitCount} time(s)! Your turn continues...";
                gameEngine.SetPlayerTurn(true);

                // Проверяем победу после атаки
                if (CheckEnemyWinCondition())
                {
                    isGameOver = true;
                    await DisplayAlert("Game Over", "You won! All enemy ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You won!";
                    return;
                }
            }
            else
            {
                GameStatusLabel.Text = $"{attackName} attack missed! Enemy's turn...";
                gameEngine.SetPlayerTurn(false);
            }
        }

        // Обработчики специальных атак
        private void OnLineHorizontalClicked(object sender, EventArgs e)
        {
            if (isGameOver) return;
            gameEngine.SelectSpecialAttack(SpecialAttack.LineHorizontal);
            SpecialAttackStatusLabel.Text = "Horizontal Line selected - click on enemy board";
        }

        private void OnLineVerticalClicked(object sender, EventArgs e)
        {
            if (isGameOver) return;
            gameEngine.SelectSpecialAttack(SpecialAttack.LineVertical);
            SpecialAttackStatusLabel.Text = "Vertical Line selected - click on enemy board";
        }

        private void OnArea3x3Clicked(object sender, EventArgs e)
        {
            if (isGameOver) return;
            gameEngine.SelectSpecialAttack(SpecialAttack.Area3x3);
            SpecialAttackStatusLabel.Text = "3x3 Area selected - click on enemy board";
        }

        private void UpdateShipsProgress()
        {
            ShipsProgressLabel.Text = $"Ships: {gameEngine.PlacedShipsCount}/{gameEngine.TotalShipsCount} " +
                                    $"(Current: {gameEngine.CurrentShipSize} cells, " +
                                    $"{(gameEngine.IsShipHorizontal ? "Horizontal" : "Vertical")})";
        }

        private void UpdateSpecialAttacks()
        {
            LineHorizontalBtn.IsEnabled = gameEngine.HasLineHorizontalAttack;
            LineVerticalBtn.IsEnabled = gameEngine.HasLineVerticalAttack;
            Area3x3Btn.IsEnabled = gameEngine.HasArea3x3Attack;

            LineHorizontalBtn.BackgroundColor = gameEngine.HasLineHorizontalAttack ? Colors.LightGreen : Colors.LightGray;
            LineVerticalBtn.BackgroundColor = gameEngine.HasLineVerticalAttack ? Colors.LightBlue : Colors.LightGray;
            Area3x3Btn.BackgroundColor = gameEngine.HasArea3x3Attack ? Colors.LightCoral : Colors.LightGray;

            if (gameEngine.SelectedSpecialAttack != SpecialAttack.None)
            {
                SpecialAttackStatusLabel.Text = $"{gameEngine.SelectedSpecialAttack} selected - click on enemy board";
            }
            else
            {
                SpecialAttackStatusLabel.Text = "Select special attack and click on enemy board";
            }
        }

        private void OnSpecialAttacksUpdated()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateSpecialAttacks();
            });
        }

        private void OnRotateClicked(object sender, EventArgs e)
        {
            if (isGameOver) return;
            gameEngine.RotateShip();
            UpdateShipsProgress();
        }

        private async void OnReadyClicked(object sender, EventArgs e)
        {
            if (isGameOver) return;

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
                if (isGameOver) return;

                switch (newState)
                {
                    case GameState.ShipPlacement:
                        GameStatusLabel.Text = "Place your ships - click on your board";
                        RotateBtn.IsEnabled = true;
                        ReadyBtn.IsEnabled = true;
                        SpecialAttacksStack.IsVisible = false;
                        break;
                    case GameState.PlayerTurn:
                        GameStatusLabel.Text = "Your turn - click enemy board!";
                        RotateBtn.IsEnabled = false;
                        ReadyBtn.IsEnabled = false;
                        SpecialAttacksStack.IsVisible = true;
                        UpdateSpecialAttacks();
                        break;
                    case GameState.EnemyTurn:
                        GameStatusLabel.Text = "Enemy's turn - waiting...";
                        RotateBtn.IsEnabled = false;
                        ReadyBtn.IsEnabled = false;
                        SpecialAttacksStack.IsVisible = false;
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