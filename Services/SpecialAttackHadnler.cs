using SeaBattle.Models;

namespace SeaBattle.Services
{
    public class SpecialAttackHandler
    {
        private GameEngine gameEngine;
        private P2PServer p2pServer;
        private Action updateBoardDisplay;
        private Action updateSpecialAttacks;
        private Action<bool> endGame;
        private Func<bool> checkEnemyWinCondition;
        private Action<string> updateGameStatus;

        public SpecialAttackHandler(GameEngine gameEngine, P2PServer p2pServer,
                                  Action updateBoardDisplay, Action updateSpecialAttacks,
                                  Action<bool> endGame, Func<bool> checkEnemyWinCondition,
                                  Action<string> updateGameStatus)
        {
            this.gameEngine = gameEngine;
            this.p2pServer = p2pServer;
            this.updateBoardDisplay = updateBoardDisplay;
            this.updateSpecialAttacks = updateSpecialAttacks;
            this.endGame = endGame;
            this.checkEnemyWinCondition = checkEnemyWinCondition;
            this.updateGameStatus = updateGameStatus;
        }

        public async Task ExecuteSpecialAttack(int x, int y)
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

            await p2pServer.SendMessage($"SPECIAL:{attackType}:{x}:{y}");
            updateBoardDisplay();
            updateSpecialAttacks();

            bool hitSomething = shots.Any(shot => shot.hit);
            int hitCount = shots.Count(shot => shot.hit);

            // Проверяем победу - если выиграли, завершаем игру (победитель не отправляет WIN)
            if (checkEnemyWinCondition())
            {
                endGame(true);
                // Победитель НЕ отправляет WIN - проигравший сам поймет что проиграл
                return;
            }

            if (hitCount > 0)
            {
                updateGameStatus($"{attackName} attack hit {hitCount} time(s)! Your turn continues...");
                gameEngine.SetPlayerTurn(true);
            }
            else
            {
                updateGameStatus($"{attackName} attack missed! Enemy's turn...");
                gameEngine.SetPlayerTurn(false);
            }
        }

        public async Task ProcessEnemySpecialAttack(string attackType, int startX, int startY)
        {
            string attackName = attackType switch
            {
                "HorizontalLine" => "Horizontal Line",
                "VerticalLine" => "Vertical Line",
                "Area3x3" => "3x3 Area",
                _ => "Special Attack"
            };

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

            await p2pServer.SendMessage($"SPECIAL_RESULT:{(hitSomething ? "HIT" : "MISS")}");
            updateBoardDisplay();

            // Проверяем поражение - если проиграли, отправляем WIN противнику
            if (gameEngine.CheckWinCondition())
            {
                endGame(false);
                await p2pServer.SendMessage("WIN"); // Проигравший отправляет WIN победителю
                return;
            }

            if (!hitSomething)
            {
                gameEngine.SetPlayerTurn(true);
                updateGameStatus($"Enemy used {attackName} attack and missed! Your turn!");
            }
            else
            {
                updateGameStatus($"Enemy used {attackName} attack and hit! Their turn continues...");
            }
        }

        private bool ProcessHorizontalLineAttackOnPlayer(int startX, int startY)
        {
            bool hitSomething = false;

            for (int x = 0; x < 10; x++)
            {
                if (gameEngine.PlayerBoard[x, startY] == CellState.Hit || gameEngine.PlayerBoard[x, startY] == CellState.Miss)
                    continue;

                if (gameEngine.PlayerBoard[x, startY] == CellState.Ship)
                {
                    gameEngine.PlayerBoard[x, startY] = CellState.Hit;
                    hitSomething = true;
                }
                else if (gameEngine.PlayerBoard[x, startY] == CellState.Empty)
                {
                    gameEngine.PlayerBoard[x, startY] = CellState.Miss;
                }
            }

            return hitSomething;
        }

        private bool ProcessVerticalLineAttackOnPlayer(int startX, int startY)
        {
            bool hitSomething = false;

            for (int y = 0; y < 10; y++)
            {
                if (gameEngine.PlayerBoard[startX, y] == CellState.Hit || gameEngine.PlayerBoard[startX, y] == CellState.Miss)
                    continue;

                if (gameEngine.PlayerBoard[startX, y] == CellState.Ship)
                {
                    gameEngine.PlayerBoard[startX, y] = CellState.Hit;
                    hitSomething = true;
                }
                else if (gameEngine.PlayerBoard[startX, y] == CellState.Empty)
                {
                    gameEngine.PlayerBoard[startX, y] = CellState.Miss;
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
    }
}