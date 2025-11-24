using System;
using System.Linq;

namespace SeaBattle.Models
{
    // Перечисление состояний игры
    public enum GameState { ShipPlacement, PlayerTurn, EnemyTurn, GameOver }

    // Перечисление состояний клетки на игровом поле
    public enum CellState { Empty, Ship, Hit, Miss }

    // Перечисление типов кораблей с указанием их размера
    public enum ShipType { FourDecker = 4, ThreeDecker = 3, ThreeDecker2 = 3 }

    // Перечисление типов специальных атак
    public enum SpecialAttack { None, LineHorizontal, LineVertical, Area3x3 }

    // Класс, представляющий корабль
    public class Ship
    {
        // Тип корабля (определяет его размер)
        public ShipType Type { get; set; }

        // Координата X начальной позиции корабля
        public int X { get; set; }

        // Координата Y начальной позиции корабля
        public int Y { get; set; }

        // Флаг, указывающий горизонтальную ориентацию корабля
        public bool IsHorizontal { get; set; }

        // Флаг, указывающий что корабль размещен на поле
        public bool IsPlaced { get; set; }
    }

    // Основной класс игрового движка
    public class GameEngine
    {
        // Игровая доска игрока (10x10 клеток)
        public CellState[,] PlayerBoard { get; private set; }

        // Игровая доска противника (10x10 клеток)
        public CellState[,] EnemyBoard { get; private set; }

        // Текущее состояние игры
        public GameState CurrentState { get; private set; }

        // Флаг, указывающий чей сейчас ход
        public bool IsPlayerTurn { get; private set; }

        // Флаг готовности игрока к игре
        public bool IsPlayerReady { get; private set; }

        // Флаг готовности противника к игре
        public bool IsEnemyReady { get; private set; }

        // Количество размещенных кораблей
        public int PlacedShipsCount => ships.Count(s => s.IsPlaced);

        // Общее количество кораблей для размещения
        public int TotalShipsCount => ships.Length;

        // Размер текущего корабля для размещения
        public int CurrentShipSize => currentShipIndex < ships.Length ? (int)ships[currentShipIndex].Type : 0;

        // Флаг горизонтальной ориентации текущего корабля
        public bool IsShipHorizontal => isShipHorizontal;

        // Специальные атаки - флаги доступности каждой атаки
        public bool HasLineHorizontalAttack { get; private set; } = true;
        public bool HasLineVerticalAttack { get; private set; } = true;
        public bool HasArea3x3Attack { get; private set; } = true;

        // Выбранная в данный момент специальная атака
        public SpecialAttack SelectedSpecialAttack { get; set; } = SpecialAttack.None;

        // Массив кораблей для размещения
        private Ship[] ships;

        // Индекс текущего корабля для размещения
        private int currentShipIndex = 0;

        // Флаг горизонтальной ориентации корабля
        private bool isShipHorizontal = false;

        // События для уведомления об изменениях в игре
        public event Action<GameState> GameStateChanged;
        public event Action BoardUpdated;
        public event Action SpecialAttacksUpdated;

        // Конструктор игрового движка
        public GameEngine()
        {
            ResetGame();
        }

        // Сброс игры к начальному состоянию
        public void ResetGame()
        {
            // Инициализация пустых игровых досок
            PlayerBoard = new CellState[10, 10];
            EnemyBoard = new CellState[10, 10];

            // Установка начального состояния - расстановка кораблей
            CurrentState = GameState.ShipPlacement;
            IsPlayerTurn = false;
            IsPlayerReady = false;
            IsEnemyReady = false;
            currentShipIndex = 0;
            isShipHorizontal = false;

            // Сбрасываем специальные атаки - делаем все доступными
            HasLineHorizontalAttack = true;
            HasLineVerticalAttack = true;
            HasArea3x3Attack = true;
            SelectedSpecialAttack = SpecialAttack.None;

            // Инициализация кораблей
            InitializeShips();

            // Уведомление подписчиков об изменениях
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
        }

        // Инициализация массива кораблей
        private void InitializeShips()
        {
            ships = new Ship[]
            {
                new Ship { Type = ShipType.FourDecker },   // 4-х клеточный корабль
                new Ship { Type = ShipType.ThreeDecker },  // 3-х клеточный корабль
                new Ship { Type = ShipType.ThreeDecker2 }  // второй 3-х клеточный корабль
            };
        }

        // Поворот текущего корабля
        public void RotateShip()
        {
            isShipHorizontal = !isShipHorizontal;
        }

        // Размещение корабля на поле игрока
        public bool PlaceShip(int x, int y)
        {
            // Проверка что есть корабли для размещения
            if (currentShipIndex >= ships.Length)
                return false;

            var ship = ships[currentShipIndex];

            // Проверка возможности размещения корабля в указанной позиции
            if (!CanPlaceShip(x, y, ship.Type, isShipHorizontal))
                return false;

            // Размещение корабля на поле
            for (int i = 0; i < (int)ship.Type; i++)
            {
                int posX = isShipHorizontal ? x + i : x;
                int posY = isShipHorizontal ? y : y + i;

                if (posX < 10 && posY < 10)
                {
                    PlayerBoard[posX, posY] = CellState.Ship;
                }
            }

            // Сохранение информации о размещенном корабле
            ship.X = x;
            ship.Y = y;
            ship.IsHorizontal = isShipHorizontal;
            ship.IsPlaced = true;
            currentShipIndex++;

            // Уведомление об обновлении доски
            BoardUpdated?.Invoke();
            return true;
        }

        // Проверка возможности размещения корабля
        private bool CanPlaceShip(int x, int y, ShipType shipType, bool isHorizontal)
        {
            int size = (int)shipType;

            // Проверка выхода за границы поля
            if (isHorizontal)
            {
                if (x + size > 10) return false;
            }
            else
            {
                if (y + size > 10) return false;
            }

            // Проверка что все клетки под корабль свободны
            for (int i = 0; i < size; i++)
            {
                int checkX = isHorizontal ? x + i : x;
                int checkY = isHorizontal ? y : y + i;

                if (checkX >= 10 || checkY >= 10 || PlayerBoard[checkX, checkY] != CellState.Empty)
                    return false;
            }

            return true;
        }

        // Проверка что все корабли размещены
        public bool AllShipsPlaced()
        {
            return ships.All(s => s.IsPlaced);
        }

        // Установка флага готовности игрока
        public void SetPlayerReady(bool ready)
        {
            IsPlayerReady = ready;
        }

        // Установка флага готовности противника
        public void SetEnemyReady(bool ready)
        {
            IsEnemyReady = ready;

            // Если оба игрока готовы - начинаем игру
            if (IsPlayerReady && IsEnemyReady)
            {
                StartGame();
            }
        }

        // Начало игры
        public void StartGame()
        {
            CurrentState = GameState.PlayerTurn;
            IsPlayerTurn = true;
            GameStateChanged?.Invoke(CurrentState);
        }

        // Установка чей сейчас ход
        public void SetPlayerTurn(bool playerTurn)
        {
            IsPlayerTurn = playerTurn;
            CurrentState = playerTurn ? GameState.PlayerTurn : GameState.EnemyTurn;
            GameStateChanged?.Invoke(CurrentState);
        }

        // Обработка выстрела противника по полю игрока
        public bool ProcessEnemyShot(int x, int y)
        {
            // Проверка что по этой клетке еще не стреляли
            if (PlayerBoard[x, y] == CellState.Hit || PlayerBoard[x, y] == CellState.Miss)
                return false;

            // Проверка попадания по кораблю
            if (PlayerBoard[x, y] == CellState.Ship)
            {
                PlayerBoard[x, y] = CellState.Hit;
                BoardUpdated?.Invoke();
                return true;
            }
            // Промах
            else if (PlayerBoard[x, y] == CellState.Empty)
            {
                PlayerBoard[x, y] = CellState.Miss;
                BoardUpdated?.Invoke();
                return false;
            }
            return false;
        }

        // Отметка результата нашего выстрела по полю противника
        public void MarkShotResult(int x, int y, bool hit)
        {
            // Проверка что по этой клетке еще не стреляли
            if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                return;

            // Отметка попадания или промаха
            EnemyBoard[x, y] = hit ? CellState.Hit : CellState.Miss;
            BoardUpdated?.Invoke();
        }

        // Специальная атака - горизонтальная линия
        // Теперь атакует ВСЕ клетки на горизонтальной линии
        public (int x, int y, bool hit)[] ExecuteLineHorizontalAttack(int startX, int startY)
        {
            if (!HasLineHorizontalAttack)
                return Array.Empty<(int, int, bool)>();

            var shots = new System.Collections.Generic.List<(int x, int y, bool hit)>();

            // Атакуем ВСЮ горизонтальную линию
            for (int x = 0; x < 10; x++)
            {
                // Пропускаем уже отмеченные клетки
                if (EnemyBoard[x, startY] == CellState.Hit || EnemyBoard[x, startY] == CellState.Miss)
                    continue;

                // Проверяем клетку на наличие корабля
                if (EnemyBoard[x, startY] == CellState.Ship)
                {
                    // Попадание по кораблю
                    EnemyBoard[x, startY] = CellState.Hit;
                    shots.Add((x, startY, true));
                }
                else if (EnemyBoard[x, startY] == CellState.Empty)
                {
                    // Промах
                    EnemyBoard[x, startY] = CellState.Miss;
                    shots.Add((x, startY, false));
                }
            }

            HasLineHorizontalAttack = false;
            SelectedSpecialAttack = SpecialAttack.None;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }

        // Специальная атака - вертикальная линия  
        // Теперь атакует ВСЕ клетки на вертикальной линии
        public (int x, int y, bool hit)[] ExecuteLineVerticalAttack(int startX, int startY)
        {
            if (!HasLineVerticalAttack)
                return Array.Empty<(int, int, bool)>();

            var shots = new System.Collections.Generic.List<(int x, int y, bool hit)>();

            // Атакуем ВЕСЬ вертикальный столбец
            for (int y = 0; y < 10; y++)
            {
                // Пропускаем уже отмеченные клетки
                if (EnemyBoard[startX, y] == CellState.Hit || EnemyBoard[startX, y] == CellState.Miss)
                    continue;

                // Проверяем клетку на наличие корабля
                if (EnemyBoard[startX, y] == CellState.Ship)
                {
                    // Попадание по кораблю
                    EnemyBoard[startX, y] = CellState.Hit;
                    shots.Add((startX, y, true));
                }
                else if (EnemyBoard[startX, y] == CellState.Empty)
                {
                    // Промах
                    EnemyBoard[startX, y] = CellState.Miss;
                    shots.Add((startX, y, false));
                }
            }

            HasLineVerticalAttack = false;
            SelectedSpecialAttack = SpecialAttack.None;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }

        // Специальная атака - область 3x3 (остается без изменений)
        public (int x, int y, bool hit)[] ExecuteArea3x3Attack(int centerX, int centerY)
        {
            if (!HasArea3x3Attack)
                return Array.Empty<(int, int, bool)>();

            var shots = new System.Collections.Generic.List<(int x, int y, bool hit)>();

            // Бьем по области 3x3 вокруг указанной точки
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

        // Выбор специальной атаки
        public void SelectSpecialAttack(SpecialAttack attack)
        {
            // Можно выбирать атаку только во время хода игрока
            if (CurrentState != GameState.PlayerTurn || !IsPlayerTurn)
                return;

            SelectedSpecialAttack = attack;
            SpecialAttacksUpdated?.Invoke();
        }

        // Проверка условия победы (все корабли игрока потоплены)
        public bool CheckWinCondition()
        {
            // Проверяем что на поле игрока не осталось непотопленных кораблей
            return !PlayerBoard.Cast<CellState>().Any(cell => cell == CellState.Ship);
        }
    }
}