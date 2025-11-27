using SeaBattle.Models;

namespace SeaBattle.Services
{
    /// <summary>
    /// Менеджер игровых досок - отвечает за создание и обновление игровых сеток
    /// </summary>
    public class GameBoardManager
    {
        private GameEngine gameEngine;
        private Grid playerGrid;
        private Grid enemyGrid;
        private Action<int, int> onPlayerGridClicked;
        private Action<int, int> onEnemyGridClicked;

        /// <summary>
        /// Конструктор менеджера игровых досок
        /// </summary>
        /// <param name="gameEngine">Игровой движок</param>
        /// <param name="playerGrid">Сетка игрока</param>
        /// <param name="enemyGrid">Сетка противника</param>
        /// <param name="onPlayerGridClicked">Обработчик клика по сетке игрока</param>
        /// <param name="onEnemyGridClicked">Обработчик клика по сетке противника</param>
        public GameBoardManager(GameEngine gameEngine, Grid playerGrid, Grid enemyGrid,
                              Action<int, int> onPlayerGridClicked, Action<int, int> onEnemyGridClicked)
        {
            this.gameEngine = gameEngine;
            this.playerGrid = playerGrid;
            this.enemyGrid = enemyGrid;
            this.onPlayerGridClicked = onPlayerGridClicked;
            this.onEnemyGridClicked = onEnemyGridClicked;
        }

        /// <summary>
        /// Создание игровых досок
        /// </summary>
        public void CreateGameBoards()
        {
            CreateGrid(playerGrid, true);
            CreateGrid(enemyGrid, false);
        }

        /// <summary>
        /// Создание игровой сетки
        /// </summary>
        /// <param name="grid">Сетка для создания</param>
        /// <param name="isPlayerGrid">True если это сетка игрока, False если противника</param>
        private void CreateGrid(Grid grid, bool isPlayerGrid)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                grid.Children.Clear();
                grid.RowDefinitions.Clear();
                grid.ColumnDefinitions.Clear();

                // Создание 10x10 сетки
                for (int i = 0; i < 10; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                // Заполнение сетки кнопками
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

                        // Назначение обработчиков кликов в зависимости от типа сетки
                        if (isPlayerGrid)
                        {
                            int currentX = x;
                            int currentY = y;
                            button.Clicked += (s, e) => onPlayerGridClicked(currentX, currentY);
                        }
                        else
                        {
                            int currentX = x;
                            int currentY = y;
                            button.Clicked += (s, e) => onEnemyGridClicked(currentX, currentY);
                        }

                        grid.Children.Add(button);
                        Grid.SetRow(button, y);
                        Grid.SetColumn(button, x);
                    }
                }

                UpdateBoardDisplay();
            });
        }

        /// <summary>
        /// Обновление отображения игровых досок
        /// </summary>
        public void UpdateBoardDisplay()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateGrid(playerGrid, true);
                UpdateGrid(enemyGrid, false);
            });
        }

        /// <summary>
        /// Обновление отдельной игровой сетки
        /// </summary>
        /// <param name="grid">Сетка для обновления</param>
        /// <param name="isPlayerGrid">True если это сетка игрока, False если противника</param>
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