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

        /// <summary>
        /// Конструктор игрового движка
        /// </summary>
        public GameEngine()
        {
            ResetGame();
        }

        /// <summary>
        /// Сброс игры к начальному состоянию
        /// </summary>
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

        /// <summary>
        /// Инициализация массива кораблей
        /// </summary>
        private void InitializeShips()
        {
            ships = new Ship[]
            {
                new Ship { Type = ShipType.FourDecker },   // 4-х клеточный корабль
                new Ship { Type = ShipType.ThreeDecker },  // 3-х клеточный корабль
                new Ship { Type = ShipType.ThreeDecker2 }  // второй 3-х клеточный корабль
            };
        }

        /// <summary>
        /// Поворот текущего корабля
        /// </summary>
        public void RotateShip()
        {
            isShipHorizontal = !isShipHorizontal;
        }

        /// <summary>
        /// Размещение корабля на поле игрока
        /// </summary>
        /// <param name="x">Координата X</param>
        /// <param name="y">Координата Y</param>
        /// <returns>True если корабль успешно размещен, иначе False</returns>
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

        /// <summary>
        /// Проверка возможности размещения корабля с валидацией расстояния
        /// </summary>
        /// <param name="x">Координата X</param>
        /// <param name="y">Координата Y</param>
        /// <param name="shipType">Тип корабля</param>
        /// <param name="isHorizontal">Ориентация корабля</param>
        /// <returns>True если корабль можно разместить, иначе False</returns>
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

            // Проверка что все клетки под корабль свободны и вокруг них нет других кораблей
            for (int i = 0; i < size; i++)
            {
                int checkX = isHorizontal ? x + i : x;
                int checkY = isHorizontal ? y : y + i;

                // Проверка выхода за границы
                if (checkX >= 10 || checkY >= 10)
                    return false;

                // Проверка что клетка свободна
                if (PlayerBoard[checkX, checkY] != CellState.Empty)
                    return false;

                // Проверка области вокруг клетки (включая диагонали)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int aroundX = checkX + dx;
                        int aroundY = checkY + dy;

                        // Проверяем только клетки внутри поля
                        if (aroundX >= 0 && aroundX < 10 && aroundY >= 0 && aroundY < 10)
                        {
                            // Если рядом уже есть корабль - нельзя ставить
                            if (PlayerBoard[aroundX, aroundY] == CellState.Ship)
                                return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Проверка что все корабли размещены
        /// </summary>
        /// <returns>True если все корабли размещены, иначе False</returns>
        public bool AllShipsPlaced()
        {
            return ships.All(s => s.IsPlaced);
        }

        /// <summary>
        /// Установка флага готовности игрока
        /// </summary>
        /// <param name="ready">Флаг готовности</param>
        public void SetPlayerReady(bool ready)
        {
            IsPlayerReady = ready;
        }

        /// <summary>
        /// Установка флага готовности противника
        /// </summary>
        /// <param name="ready">Флаг готовности</param>
        public void SetEnemyReady(bool ready)
        {
            IsEnemyReady = ready;

            // Если оба игрока готовы - начинаем игру
            if (IsPlayerReady && IsEnemyReady)
            {
                StartGame();
            }
        }

        /// <summary>
        /// Начало игры
        /// </summary>
        public void StartGame()
        {
            CurrentState = GameState.PlayerTurn;
            IsPlayerTurn = true;
            GameStateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Установка чей сейчас ход
        /// </summary>
        /// <param name="playerTurn">True если ход игрока, False если ход противника</param>
        public void SetPlayerTurn(bool playerTurn)
        {
            IsPlayerTurn = playerTurn;
            CurrentState = playerTurn ? GameState.PlayerTurn : GameState.EnemyTurn;
            GameStateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Обработка выстрела противника по полю игрока
        /// </summary>
        /// <param name="x">Координата X</param>
        /// <param name="y">Координата Y</param>
        /// <returns>True если попадание, False если промах</returns>
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

        /// <summary>
        /// Отметка результата нашего выстрела по полю противника
        /// </summary>
        /// <param name="x">Координата X</param>
        /// <param name="y">Координата Y</param>
        /// <param name="hit">True если попадание, False если промах</param>
        public void MarkShotResult(int x, int y, bool hit)
        {
            // Проверка что по этой клетке еще не стреляли
            if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                return;

            // Отметка попадания или промаха
            EnemyBoard[x, y] = hit ? CellState.Hit : CellState.Miss;
            BoardUpdated?.Invoke();
        }

        /// <summary>
        /// Специальная атака - горизонтальная линия (атакует ВСЕ клетки на горизонтальной линии)
        /// </summary>
        /// <param name="startX">Координата X начальной точки</param>
        /// <param name="startY">Координата Y начальной точки</param>
        /// <returns>Массив результатов выстрелов (x, y, hit)</returns>
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

        /// <summary>
        /// Специальная атака - вертикальная линия (атакует ВСЕ клетки на вертикальной линии)
        /// </summary>
        /// <param name="startX">Координата X начальной точки</param>
        /// <param name="startY">Координата Y начальной точки</param>
        /// <returns>Массив результатов выстрелов (x, y, hit)</returns>
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

        /// <summary>
        /// Специальная атака - область 3x3
        /// </summary>
        /// <param name="centerX">Координата X центра области</param>
        /// <param name="centerY">Координата Y центра области</param>
        /// <returns>Массив результатов выстрелов (x, y, hit)</returns>
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

        /// <summary>
        /// Выбор специальной атаки
        /// </summary>
        /// <param name="attack">Тип специальной атаки</param>
        public void SelectSpecialAttack(SpecialAttack attack)
        {
            // Можно выбирать атаку только во время хода игрока
            if (CurrentState != GameState.PlayerTurn || !IsPlayerTurn)
                return;

            SelectedSpecialAttack = attack;
            SpecialAttacksUpdated?.Invoke();
        }

        /// <summary>
        /// Проверка условия победы (все корабли игрока потоплены)
        /// </summary>
        /// <returns>True если все корабли игрока потоплены, иначе False</returns>
        public bool CheckWinCondition()
        {
            // Проверяем что на поле игрока не осталось непотопленных кораблей
            return !PlayerBoard.Cast<CellState>().Any(cell => cell == CellState.Ship);
        }
    }
}