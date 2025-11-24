using System;
using System.Linq;

namespace SeaBattle.Models
{
    public enum GameState { ShipPlacement, PlayerTurn, EnemyTurn, GameOver }
    public enum CellState { Empty, Ship, Hit, Miss }
    public enum ShipType { FourDecker = 4, ThreeDecker = 3, ThreeDecker2 = 3 }
    public enum SpecialAttack { None, LineHorizontal, LineVertical, Area3x3 }

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

        // Специальные атаки
        public bool HasLineHorizontalAttack { get; private set; } = true;
        public bool HasLineVerticalAttack { get; private set; } = true;
        public bool HasArea3x3Attack { get; private set; } = true;
        public SpecialAttack SelectedSpecialAttack { get; set; } = SpecialAttack.None;

        private Ship[] ships;
        private int currentShipIndex = 0;
        private bool isShipHorizontal = false;

        public event Action<GameState> GameStateChanged;
        public event Action BoardUpdated;
        public event Action SpecialAttacksUpdated;

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

            // Сбрасываем специальные атаки
            HasLineHorizontalAttack = true;
            HasLineVerticalAttack = true;
            HasArea3x3Attack = true;
            SelectedSpecialAttack = SpecialAttack.None;

            InitializeShips();
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
        }

        private void InitializeShips()
        {
            ships = new Ship[]
            {
                new Ship { Type = ShipType.FourDecker },
                new Ship { Type = ShipType.ThreeDecker },
                new Ship { Type = ShipType.ThreeDecker2 }
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

        // Обычный выстрел
        public bool ProcessEnemyShot(int x, int y)
        {
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
            if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                return;

            EnemyBoard[x, y] = hit ? CellState.Hit : CellState.Miss;
            BoardUpdated?.Invoke();
        }

        // Специальные атаки - теперь возвращают список выстрелов
        public (int x, int y, bool hit)[] ExecuteLineHorizontalAttack(int startX, int startY)
        {
            if (!HasLineHorizontalAttack)
                return Array.Empty<(int, int, bool)>();

            var shots = new System.Collections.Generic.List<(int x, int y, bool hit)>();
            bool hitDetected = false;

            // Бьем по всей горизонтали в обе стороны от стартовой точки
            // Влево от стартовой точки
            for (int x = startX; x >= 0; x--)
            {
                // Пропускаем уже отмеченные клетки (продолжаем движение)
                if (EnemyBoard[x, startY] == CellState.Hit || EnemyBoard[x, startY] == CellState.Miss)
                    continue;

                if (EnemyBoard[x, startY] == CellState.Empty)
                {
                    EnemyBoard[x, startY] = CellState.Miss;
                    shots.Add((x, startY, false));
                }
                else if (EnemyBoard[x, startY] == CellState.Ship)
                {
                    EnemyBoard[x, startY] = CellState.Hit;
                    shots.Add((x, startY, true));
                    hitDetected = true;
                    break; // Останавливаемся после первого попадания
                }
            }

            // Вправо от стартовой точки (начиная со следующей клетки)
            // Только если не было попадания в левой части
            if (!hitDetected)
            {
                for (int x = startX + 1; x < 10; x++)
                {
                    // Пропускаем уже отмеченные клетки (продолжаем движение)
                    if (EnemyBoard[x, startY] == CellState.Hit || EnemyBoard[x, startY] == CellState.Miss)
                        continue;

                    if (EnemyBoard[x, startY] == CellState.Empty)
                    {
                        EnemyBoard[x, startY] = CellState.Miss;
                        shots.Add((x, startY, false));
                    }
                    else if (EnemyBoard[x, startY] == CellState.Ship)
                    {
                        EnemyBoard[x, startY] = CellState.Hit;
                        shots.Add((x, startY, true));
                        hitDetected = true;
                        break; // Останавливаемся после первого попадания
                    }
                }
            }

            HasLineHorizontalAttack = false;
            SelectedSpecialAttack = SpecialAttack.None;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }

        public (int x, int y, bool hit)[] ExecuteLineVerticalAttack(int startX, int startY)
        {
            if (!HasLineVerticalAttack)
                return Array.Empty<(int, int, bool)>();

            var shots = new System.Collections.Generic.List<(int x, int y, bool hit)>();
            bool hitDetected = false;

            // Бьем по всей вертикали в обе стороны от стартовой точки
            // Вверх от стартовой точки
            for (int y = startY; y >= 0; y--)
            {
                // Пропускаем уже отмеченные клетки (продолжаем движение)
                if (EnemyBoard[startX, y] == CellState.Hit || EnemyBoard[startX, y] == CellState.Miss)
                    continue;

                if (EnemyBoard[startX, y] == CellState.Empty)
                {
                    EnemyBoard[startX, y] = CellState.Miss;
                    shots.Add((startX, y, false));
                }
                else if (EnemyBoard[startX, y] == CellState.Ship)
                {
                    EnemyBoard[startX, y] = CellState.Hit;
                    shots.Add((startX, y, true));
                    hitDetected = true;
                    break; // Останавливаемся после первого попадания
                }
            }

            // Вниз от стартовой точки (начиная со следующей клетки)
            // Только если не было попадания в верхней части
            if (!hitDetected)
            {
                for (int y = startY + 1; y < 10; y++)
                {
                    // Пропускаем уже отмеченные клетки (продолжаем движение)
                    if (EnemyBoard[startX, y] == CellState.Hit || EnemyBoard[startX, y] == CellState.Miss)
                        continue;

                    if (EnemyBoard[startX, y] == CellState.Empty)
                    {
                        EnemyBoard[startX, y] = CellState.Miss;
                        shots.Add((startX, y, false));
                    }
                    else if (EnemyBoard[startX, y] == CellState.Ship)
                    {
                        EnemyBoard[startX, y] = CellState.Hit;
                        shots.Add((startX, y, true));
                        hitDetected = true;
                        break; // Останавливаемся после первого попадания
                    }
                }
            }

            HasLineVerticalAttack = false;
            SelectedSpecialAttack = SpecialAttack.None;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }

        public (int x, int y, bool hit)[] ExecuteArea3x3Attack(int centerX, int centerY)
        {
            if (!HasArea3x3Attack)
                return Array.Empty<(int, int, bool)>();

            var shots = new System.Collections.Generic.List<(int x, int y, bool hit)>();

            // Бьем по области 3x3
            for (int x = Math.Max(0, centerX - 1); x <= Math.Min(9, centerX + 1); x++)
            {
                for (int y = Math.Max(0, centerY - 1); y <= Math.Min(9, centerY + 1); y++)
                {
                    // Пропускаем уже отмеченные клетки
                    if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                        continue;

                    if (EnemyBoard[x, y] == CellState.Empty)
                    {
                        EnemyBoard[x, y] = CellState.Miss;
                        shots.Add((x, y, false));
                    }
                    else if (EnemyBoard[x, y] == CellState.Ship)
                    {
                        EnemyBoard[x, y] = CellState.Hit;
                        shots.Add((x, y, true));
                    }
                }
            }

            HasArea3x3Attack = false;
            SelectedSpecialAttack = SpecialAttack.None;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }

        public void SelectSpecialAttack(SpecialAttack attack)
        {
            if (CurrentState != GameState.PlayerTurn || !IsPlayerTurn)
                return;

            SelectedSpecialAttack = attack;
            SpecialAttacksUpdated?.Invoke();
        }

        public bool CheckWinCondition()
        {
            return !PlayerBoard.Cast<CellState>().Any(cell => cell == CellState.Ship);
        }
    }
}