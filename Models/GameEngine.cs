using System;
using System.Linq;

namespace SeaBattle.Models
{
    public enum GameState { ShipPlacement, PlayerTurn, EnemyTurn, GameOver }
    public enum CellState { Empty, Ship, Hit, Miss }
    public enum ShipType { FourDecker = 4, ThreeDecker = 3, ThreeDecker2 = 3 } // Изменили корабли

    public class Ship
    {
        public ShipType Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsHorizontal { get; set; }
        public bool IsPlaced { get; set; }
    }

    public class GameEngine
    {
        public CellState[,] PlayerBoard { get; private set; }
        public CellState[,] EnemyBoard { get; private set; }
        public GameState CurrentState { get; private set; }
        public bool IsPlayerTurn { get; private set; }
        public bool IsPlayerReady { get; private set; }
        public bool IsEnemyReady { get; private set; }

        public int PlacedShipsCount => ships.Count(s => s.IsPlaced);
        public int TotalShipsCount => ships.Length;
        public int CurrentShipSize => currentShipIndex < ships.Length ? (int)ships[currentShipIndex].Type : 0;
        public bool IsShipHorizontal => isShipHorizontal;

        private Ship[] ships;
        private int currentShipIndex = 0;
        private bool isShipHorizontal = false;

        public event Action<GameState> GameStateChanged;
        public event Action BoardUpdated;

        public GameEngine()
        {
            ResetGame();
        }

        public void ResetGame()
        {
            PlayerBoard = new CellState[10, 10];
            EnemyBoard = new CellState[10, 10];
            CurrentState = GameState.ShipPlacement;
            IsPlayerTurn = false;
            IsPlayerReady = false;
            IsEnemyReady = false;
            currentShipIndex = 0;
            isShipHorizontal = false;

            InitializeShips();
            BoardUpdated?.Invoke();
        }

        private void InitializeShips()
        {
            // ИЗМЕНЕНИЕ: Теперь 3 корабля - 1 на 4 клетки и 2 на 3 клетки
            ships = new Ship[]
            {
                new Ship { Type = ShipType.FourDecker },     // 4 клетки
                new Ship { Type = ShipType.ThreeDecker },    // 3 клетки
                new Ship { Type = ShipType.ThreeDecker2 }    // 3 клетки
            };
        }

        public void RotateShip()
        {
            isShipHorizontal = !isShipHorizontal;
        }

        public bool PlaceShip(int x, int y)
        {
            if (currentShipIndex >= ships.Length)
                return false;

            var ship = ships[currentShipIndex];

            if (!CanPlaceShip(x, y, ship.Type, isShipHorizontal))
                return false;

            for (int i = 0; i < (int)ship.Type; i++)
            {
                int posX = isShipHorizontal ? x + i : x;
                int posY = isShipHorizontal ? y : y + i;

                if (posX < 10 && posY < 10)
                {
                    PlayerBoard[posX, posY] = CellState.Ship;
                }
            }

            ship.X = x;
            ship.Y = y;
            ship.IsHorizontal = isShipHorizontal;
            ship.IsPlaced = true;
            currentShipIndex++;

            BoardUpdated?.Invoke();
            return true;
        }

        private bool CanPlaceShip(int x, int y, ShipType shipType, bool isHorizontal)
        {
            int size = (int)shipType;

            if (isHorizontal)
            {
                if (x + size > 10) return false;
            }
            else
            {
                if (y + size > 10) return false;
            }

            for (int i = 0; i < size; i++)
            {
                int checkX = isHorizontal ? x + i : x;
                int checkY = isHorizontal ? y : y + i;

                if (checkX >= 10 || checkY >= 10 || PlayerBoard[checkX, checkY] != CellState.Empty)
                    return false;
            }

            return true;
        }

        public bool AllShipsPlaced()
        {
            return ships.All(s => s.IsPlaced);
        }

        public void SetPlayerReady(bool ready)
        {
            IsPlayerReady = ready;
        }

        public void SetEnemyReady(bool ready)
        {
            IsEnemyReady = ready;
            if (IsPlayerReady && IsEnemyReady)
            {
                StartGame();
            }
        }

        public void StartGame()
        {
            CurrentState = GameState.PlayerTurn;
            IsPlayerTurn = true;
            GameStateChanged?.Invoke(CurrentState);
        }

        public void SetPlayerTurn(bool playerTurn)
        {
            IsPlayerTurn = playerTurn;
            CurrentState = playerTurn ? GameState.PlayerTurn : GameState.EnemyTurn;
            GameStateChanged?.Invoke(CurrentState);
        }

        public bool ProcessEnemyShot(int x, int y)
        {
            // ИСПРАВЛЕНИЕ: Проверяем, не стреляли ли уже сюда
            if (PlayerBoard[x, y] == CellState.Hit || PlayerBoard[x, y] == CellState.Miss)
                return false;

            if (PlayerBoard[x, y] == CellState.Ship)
            {
                PlayerBoard[x, y] = CellState.Hit;
                BoardUpdated?.Invoke();
                return true;
            }
            else if (PlayerBoard[x, y] == CellState.Empty)
            {
                PlayerBoard[x, y] = CellState.Miss;
                BoardUpdated?.Invoke();
                return false;
            }
            return false;
        }

        public void MarkShotResult(int x, int y, bool hit)
        {
            // ИСПРАВЛЕНИЕ: Проверяем, не стреляли ли уже сюда
            if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                return;

            EnemyBoard[x, y] = hit ? CellState.Hit : CellState.Miss;
            BoardUpdated?.Invoke();
        }

        public bool CheckWinCondition()
        {
            return !PlayerBoard.Cast<CellState>().Any(cell => cell == CellState.Ship);
        }
    }
}