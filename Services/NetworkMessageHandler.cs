using SeaBattle.Models;

namespace SeaBattle.Services
{
    /// <summary>
    /// Обработчик сетевых сообщений - управляет получением и обработкой сообщений от противника
    /// </summary>
    public class NetworkMessageHandler
    {
        private GameEngine gameEngine;
        private P2PServer p2pServer;
        private Func<bool> getIsGameOver;
        private Action<string> updateGameStatus;
        private Action<bool> endGame;
        private Func<bool> checkEnemyWinCondition;
        private Func<string, int, int, Task> processEnemySpecialAttack;

        /// <summary>
        /// Конструктор обработчика сетевых сообщений
        /// </summary>
        /// <param name="gameEngine">Игровой движок</param>
        /// <param name="p2pServer">P2P сервер</param>
        /// <param name="getIsGameOver">Функция проверки завершения игры</param>
        /// <param name="updateGameStatus">Метод обновления статуса игры</param>
        /// <param name="endGame">Метод завершения игры</param>
        /// <param name="checkEnemyWinCondition">Функция проверки победы противника</param>
        /// <param name="processEnemySpecialAttack">Метод обработки специальной атаки противника</param>
        public NetworkMessageHandler(GameEngine gameEngine, P2PServer p2pServer,
                                   Func<bool> getIsGameOver, Action<string> updateGameStatus,
                                   Action<bool> endGame, Func<bool> checkEnemyWinCondition,
                                   Func<string, int, int, Task> processEnemySpecialAttack)
        {
            this.gameEngine = gameEngine;
            this.p2pServer = p2pServer;
            this.getIsGameOver = getIsGameOver;
            this.updateGameStatus = updateGameStatus;
            this.endGame = endGame;
            this.checkEnemyWinCondition = checkEnemyWinCondition;
            this.processEnemySpecialAttack = processEnemySpecialAttack;
        }

        /// <summary>
        /// Обработчик получения сообщения от противника
        /// </summary>
        /// <param name="ip">IP адрес отправителя</param>
        /// <param name="message">Текст сообщения</param>
        public async void OnMessageReceived(string ip, string message)
        {
            if (getIsGameOver()) return;

            if (message.StartsWith("SHOT:"))
            {
                await HandleShotMessage(message);
            }
            else if (message.StartsWith("RESULT:"))
            {
                await HandleResultMessage(message);
            }
            else if (message == "READY")
            {
                HandleReadyMessage();
            }
            else if (message == "WIN")
            {
                await HandleWinMessage();
            }
            else if (message.StartsWith("SPECIAL:"))
            {
                await HandleSpecialAttackMessage(message);
            }
            else if (message.StartsWith("SPECIAL_RESULT:"))
            {
                await HandleSpecialResultMessage(message);
            }
        }

        /// <summary>
        /// Обработка сообщения о выстреле противника
        /// </summary>
        /// <param name="message">Сообщение с координатами выстрела</param>
        private async Task HandleShotMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                bool hit = gameEngine.ProcessEnemyShot(x, y);
                await p2pServer.SendMessage($"RESULT:{x}:{y}:{(hit ? "HIT" : "MISS")}");

                if (!hit)
                {
                    gameEngine.SetPlayerTurn(true);
                    updateGameStatus("Your turn - enemy missed!");
                }
                else
                {
                    updateGameStatus("Enemy hit your ship! Their turn continues...");

                    // Если проиграли - отправляем WIN противнику
                    if (gameEngine.CheckWinCondition())
                    {
                        endGame(false);
                        await p2pServer.SendMessage("WIN"); // Проигравший отправляет WIN
                    }
                }
            }
        }

        /// <summary>
        /// Обработка сообщения о результате нашего выстрела
        /// </summary>
        /// <param name="message">Сообщение с результатом выстрела</param>
        private async Task HandleResultMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length == 4 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                bool hit = parts[3] == "HIT";
                gameEngine.MarkShotResult(x, y, hit);

                if (!hit)
                {
                    gameEngine.SetPlayerTurn(false);
                    updateGameStatus("You missed! Enemy's turn...");
                }
                else
                {
                    updateGameStatus("You hit enemy ship! Your turn continues...");

                    // Если выиграли - просто завершаем игру (победитель не отправляет WIN)
                    if (checkEnemyWinCondition())
                    {
                        endGame(true);
                        // Победитель НЕ отправляет WIN
                    }
                }
            }
        }

        /// <summary>
        /// Обработка сообщения о готовности противника
        /// </summary>
        private void HandleReadyMessage()
        {
            gameEngine.SetEnemyReady(true);
        }

        /// <summary>
        /// Обработка сообщения о победе противника
        /// </summary>
        private async Task HandleWinMessage()
        {
            // Получили WIN от противника - значит мы победили
            endGame(true);
        }

        /// <summary>
        /// Обработка сообщения о специальной атаке противника
        /// </summary>
        /// <param name="message">Сообщение с информацией об атаке</param>
        private async Task HandleSpecialAttackMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length >= 4)
            {
                string attackType = parts[1];
                int startX = int.Parse(parts[2]);
                int startY = int.Parse(parts[3]);

                await processEnemySpecialAttack(attackType, startX, startY);
            }
        }

        /// <summary>
        /// Обработка сообщения о результате специальной атаки
        /// </summary>
        /// <param name="message">Сообщение с результатом атаки</param>
        private async Task HandleSpecialResultMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length >= 2)
            {
                bool hit = parts[1] == "HIT";

                if (!hit)
                {
                    gameEngine.SetPlayerTurn(false);
                    updateGameStatus("Your special attack missed! Enemy's turn!");
                }
                else
                {
                    updateGameStatus("Your special attack hit! Your turn continues...");
                    gameEngine.SetPlayerTurn(true);

                    // Если выиграли - просто завершаем игру (победитель не отправляет WIN)
                    if (checkEnemyWinCondition())
                    {
                        endGame(true);
                        // Победитель НЕ отправляет WIN
                    }
                }
            }
        }
    }
}