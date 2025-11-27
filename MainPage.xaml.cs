using SeaBattle.Models;
using SeaBattle.Services;
using System.Collections.ObjectModel;

namespace SeaBattle
{
    public partial class MainPage : ContentPage
    {
        private P2PServer p2pServer;
        private GameEngine gameEngine;
        private GameBoardManager boardManager;
        private NetworkMessageHandler messageHandler;
        private SpecialAttackHandler attackHandler;

        private ObservableCollection<ServerInfo> availableServers = new ObservableCollection<ServerInfo>();
        private Dictionary<string, DateTime> serverLastSeen = new Dictionary<string, DateTime>();

        public bool IsGameOver { get; private set; }
        private bool isServerMode = false;

        public MainPage()
        {
            InitializeComponent();

            p2pServer = new P2PServer("Player");
            gameEngine = new GameEngine();

            boardManager = new GameBoardManager(gameEngine, PlayerGrid, EnemyGrid, OnPlayerGridClicked, OnEnemyGridClicked);

            messageHandler = new NetworkMessageHandler(
                gameEngine, p2pServer,
                () => IsGameOver,
                UpdateGameStatus,
                EndGame,
                CheckEnemyWinCondition,
                ProcessEnemySpecialAttack
            );

            attackHandler = new SpecialAttackHandler(
                gameEngine, p2pServer,
                UpdateBoardDisplay,
                UpdateSpecialAttacks,
                EndGame,
                CheckEnemyWinCondition,
                UpdateGameStatus
            );

            SetupEventHandlers();
            StartDiscovery();
            _ = ServerCleaner();

            SpecialAttacksStack.IsVisible = false;
        }

        public void UpdateGameStatus(string status)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GameStatusLabel.Text = status;
            });
        }

        public void UpdateBoardDisplay()
        {
            boardManager.UpdateBoardDisplay();
        }

        public bool CheckEnemyWinCondition()
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

        public void EndGame(bool isWinner)
        {
            IsGameOver = true;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (isWinner)
                {
                    await DisplayAlert("Game Over", "You won! All enemy ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You won!";
                }
                else
                {
                    await DisplayAlert("Game Over", "You lost! All your ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You lost!";
                }
                SpecialAttacksStack.IsVisible = false;
            });
        }

        private void SetupEventHandlers()
        {
            p2pServer.client.MessageReceived += messageHandler.OnMessageReceived;
            p2pServer.server.MessageReceived += messageHandler.OnMessageReceived;
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
                IsGameOver = false;

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
                IsGameOver = false;

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
                IsGameOver = false;

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
                    IsGameOver = false;
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
                IsGameOver = false;
                InitializeGame();
            });
        }

        private void InitializeGame()
        {
            gameEngine.ResetGame();
            boardManager.CreateGameBoards();
            UpdateShipsProgress();
            UpdateSpecialAttacks();
            GameStatusLabel.Text = "Click on YOUR board to place ships. Use Rotate to change direction.";
            RotateBtn.IsEnabled = true;
            IsGameOver = false;
            SpecialAttacksStack.IsVisible = false;
        }

        private void OnPlayerGridClicked(int x, int y)
        {
            if (IsGameOver) return;

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
                        $"Cannot place {gameEngine.CurrentShipSize}-cell ship at position {x},{y}. " +
                        "Ships cannot be placed next to each other or outside the board.",
                        "OK");
                }
            }
        }

        private async void OnEnemyGridClicked(int x, int y)
        {
            if (IsGameOver) return;

            if (gameEngine.CurrentState == GameState.PlayerTurn && gameEngine.IsPlayerTurn)
            {
                if (gameEngine.SelectedSpecialAttack != SpecialAttack.None)
                {
                    await attackHandler.ExecuteSpecialAttack(x, y);
                }
                else if (gameEngine.EnemyBoard[x, y] == CellState.Empty)
                {
                    await p2pServer.SendMessage($"SHOT:{x}:{y}");
                    GameStatusLabel.Text = "You won";
                }
            }
        }

        private void OnLineHorizontalClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.SelectSpecialAttack(SpecialAttack.LineHorizontal);
            SpecialAttackStatusLabel.Text = "Horizontal Line selected - click on enemy board";
        }

        private void OnLineVerticalClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.SelectSpecialAttack(SpecialAttack.LineVertical);
            SpecialAttackStatusLabel.Text = "Vertical Line selected - click on enemy board";
        }

        private void OnArea3x3Clicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.SelectSpecialAttack(SpecialAttack.Area3x3);
            SpecialAttackStatusLabel.Text = "3x3 Area selected - click on enemy board";
        }

        private void UpdateShipsProgress()
        {
            ShipsProgressLabel.Text = $"Ships: {gameEngine.PlacedShipsCount}/{gameEngine.TotalShipsCount} " +
                                    $"(Current: {gameEngine.CurrentShipSize} cells, " +
                                    $"{(gameEngine.IsShipHorizontal ? "Horizontal" : "Vertical")})";
        }

        public void UpdateSpecialAttacks()
        {
            MainThread.BeginInvokeOnMainThread(() =>
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
            });
        }

        private void OnSpecialAttacksUpdated()
        {
            UpdateSpecialAttacks();
        }

        private void OnRotateClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.RotateShip();
            UpdateShipsProgress();
        }

        private async void OnReadyClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;

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
                if (IsGameOver) return;

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

        public async Task ProcessEnemySpecialAttack(string attackType, int startX, int startY)
        {
            await attackHandler.ProcessEnemySpecialAttack(attackType, startX, startY);
        }
    }
}