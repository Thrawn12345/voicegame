using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VoiceGame
{
    public class GameForm : Form
    {
        // Game state
        private Player player = new(PointF.Empty, PointF.Empty);
        private readonly List<Laser> lasers = new();
        private readonly List<Enemy> enemies = new();
        private readonly List<EnemyBullet> enemyBullets = new();
        private int lives = GameConstants.InitialLives;
        private bool gameOver = false;

        // Game systems
        private readonly ObstacleManager obstacleManager = new();
        private GameLogic gameLogic = null!;
        private VoiceController voiceController = null!;
        private readonly AIShootingAgent aiAgent = new();
        private readonly TrainingDataCollector trainingCollector = new();
        private readonly EnemyLearningAgent enemyLearning = new();
        private AIPlayer? aiPlayer = null;
        private bool aiMode = false;

        // Counters for AI training
        private int enemiesDestroyed = 0;
        private int bulletsDestroyed = 0;

        // Timers
        private readonly System.Windows.Forms.Timer gameTimer = new();
        private readonly System.Windows.Forms.Timer aiShootingTimer = new();
        private readonly System.Windows.Forms.Timer enemySpawnTimer = new();
        private readonly System.Windows.Forms.Timer randomMovementTimer = new();

        // Random movement
        private readonly Random random = new();

        public GameForm()
        {
            InitializeForm();
            InitializeGameSystems();
            InitializeTimers();
            StartGame();
        }

        private void InitializeForm()
        {
            Text = "Voice Controlled Game with AI Shooting";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.Black;
            KeyPreview = true;

            Paint += GameForm_Paint;
            KeyDown += GameForm_KeyDown;
        }

        private void InitializeGameSystems()
        {
            // Initialize player
            player = new Player(new PointF(Width / 2, Height / 2), PointF.Empty);

            // Initialize systems
            gameLogic = new GameLogic(obstacleManager);
            voiceController = new VoiceController(OnVelocityChange);

            // Generate obstacles
            obstacleManager.GenerateObstacles(ClientSize, player.Position);
        }

        private void InitializeTimers()
        {
            // Game loop timer (60 FPS)
            gameTimer.Interval = GameConstants.GameTimerInterval;
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Start();

            // AI shooting timer
            aiShootingTimer.Interval = GameConstants.AIShootingInterval;
            aiShootingTimer.Tick += AIShootingTimer_Tick;
            aiShootingTimer.Start();

            // Enemy spawn timer
            enemySpawnTimer.Interval = GameConstants.EnemySpawnInterval;
            enemySpawnTimer.Tick += EnemySpawnTimer_Tick;
            enemySpawnTimer.Start();

            // Random movement timer
            randomMovementTimer.Interval = GameConstants.RandomMovementInterval;
            randomMovementTimer.Tick += RandomMovementTimer_Tick;
            randomMovementTimer.Start();
        }

        private void StartGame()
        {
            trainingCollector.StartEpisode();
        }

        private void OnVelocityChange(PointF velocity)
        {
            player = player with { Velocity = velocity };
        }

        private void GameForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.R && gameOver)
            {
                RestartGame();
            }
            else if (e.KeyCode == Keys.A && !gameOver)
            {
                // Toggle AI mode with 'A' key
                ToggleAIMode();
            }
        }

        private void RestartGame()
        {
            // Reset game state
            gameOver = false;
            lives = GameConstants.InitialLives;
            enemiesDestroyed = 0;
            bulletsDestroyed = 0;

            // Clear game objects
            enemies.Clear();
            lasers.Clear();
            enemyBullets.Clear();

            // Reset player position
            player = new Player(new PointF(Width / 2, Height / 2), PointF.Empty);

            // Regenerate obstacles
            obstacleManager.GenerateObstacles(ClientSize, player.Position);

            // Start new AI training episode
            trainingCollector.StartEpisode();

            Console.WriteLine("🔄 Game restarted - New AI training episode begun!");
            voiceController.Speak("New game started!");
        }

        private void ToggleAIMode()
        {
            // Initialize AI player on first use
            if (aiPlayer == null)
            {
                // Try to load the most recent trained model from training_data folder
                string trainingDir = "training_data";
                if (System.IO.Directory.Exists(trainingDir))
                {
                    string[] modelFiles = System.IO.Directory.GetFiles(trainingDir, "ai_model_*.json");
                    string? latestModel = modelFiles.Length > 0 ? modelFiles.OrderByDescending(f => f).FirstOrDefault() : null;

                    aiPlayer = new AIPlayer(latestModel ?? string.Empty, explorationRate: 0.1f);
                }
                else
                {
                    aiPlayer = new AIPlayer(string.Empty, explorationRate: 0.1f);
                }
            }
            aiMode = !aiMode;

            if (aiMode)
            {
                // Disable voice control when AI takes over
                Text = "Voice Game - AI MODE (Press 'A' to disable)";
            }
            else
            {
                // Re-enable voice control
                Text = "Voice Controlled Game with AI Shooting";
            }

            Console.WriteLine(aiMode ? "🤖 AI Mode ENABLED" : "🎤 Voice Control ENABLED");
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            // AI Mode: Let AI control player movement
            if (aiMode && aiPlayer != null)
            {
                var aiVelocity = aiPlayer.GetRecommendedVelocity(
                    player.Position, player.Velocity,
                    enemies, lasers,
                    lives, gameOver,
                    ClientSize.Width, ClientSize.Height
                );
                player = player with { Velocity = aiVelocity };
            }

            // Update game objects using GameLogic
            player = gameLogic.UpdatePlayerPosition(player, ClientSize);

            var updatedLasers = gameLogic.UpdateLasers(lasers, ClientSize);
            lasers.Clear();
            lasers.AddRange(updatedLasers);

            var updatedBullets = gameLogic.UpdateEnemyBullets(enemyBullets, ClientSize);
            enemyBullets.Clear();
            enemyBullets.AddRange(updatedBullets);

            // Update enemies with learning-based movement
            var updatedEnemies = new List<Enemy>();
            foreach (var enemy in enemies)
            {
                if (enemy.LearningId >= 0)
                {
                    // Use learned movement
                    var learnedVelocity = enemyLearning.GetMovementDecision(
                        enemy.LearningId,
                        enemy.Position,
                        player.Position,
                        player.Velocity,
                        lasers,
                        enemies,
                        ClientSize.Width,
                        ClientSize.Height
                    );

                    // Calculate new position with learned velocity
                    var newPos = new PointF(
                        enemy.Position.X + learnedVelocity.X,
                        enemy.Position.Y + learnedVelocity.Y
                    );

                    // Validate position with collision detection (same as player movement)
                    var validPos = CollisionDetector.GetValidPosition(
                        enemy.Position,
                        newPos,
                        GameConstants.EnemyRadius,
                        obstacleManager.Obstacles.ToList(),
                        ClientSize
                    );

                    var movedEnemy = enemy with
                    {
                        Position = validPos
                    };
                    updatedEnemies.Add(movedEnemy);
                }
                else
                {
                    // Fallback to original behavior
                    updatedEnemies.Add(enemy);
                }
            }

            // Apply collision detection and bounds
            var finalEnemies = gameLogic.UpdateEnemies(updatedEnemies, player, ClientSize);
            enemies.Clear();
            enemies.AddRange(finalEnemies);

            // Handle enemy shooting with learning
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];

                if (enemy.LearningId >= 0)
                {
                    // Use learned shooting
                    bool shouldShoot = enemyLearning.ShouldShoot(
                        enemy.LearningId,
                        enemy.Position,
                        player.Position,
                        player.Velocity,
                        lasers,
                        enemies,
                        ClientSize.Width,
                        ClientSize.Height,
                        out PointF shootDirection
                    );

                    if (shouldShoot && (DateTime.Now - enemy.LastShotTime).TotalMilliseconds > GameConstants.EnemyShootCooldownMs)
                    {
                        var bulletVelocity = new PointF(
                            shootDirection.X * GameConstants.EnemyBulletSpeed,
                            shootDirection.Y * GameConstants.EnemyBulletSpeed
                        );
                        enemyBullets.Add(new EnemyBullet(enemy.Position, bulletVelocity));
                        enemies[i] = enemy with { LastShotTime = DateTime.Now };

                        // Small reward for shooting
                        enemyLearning.RecordReward(enemy.LearningId, 1f);
                    }
                }
                else
                {
                    // Original shooting logic
                    var bullet = gameLogic.CreateEnemyBullet(enemy, player);
                    if (bullet != null)
                    {
                        enemyBullets.Add(bullet);
                        enemies[i] = enemy with { LastShotTime = DateTime.Now };
                    }
                }
            }

            // Process collisions
            var laserEnemyResult = gameLogic.ProcessLaserEnemyCollisions(lasers, enemies, enemiesDestroyed);

            // Track destroyed enemies for learning
            var destroyedCount = laserEnemyResult.enemiesDestroyed - enemiesDestroyed;
            if (destroyedCount > 0)
            {
                // Find which enemies were destroyed and penalize them
                foreach (var enemy in enemies)
                {
                    if (!laserEnemyResult.enemies.Any(e => e.LearningId == enemy.LearningId))
                    {
                        if (enemy.LearningId >= 0)
                        {
                            enemyLearning.EnemyDestroyed(enemy.LearningId);
                        }
                    }
                }
            }

            lasers.Clear();
            lasers.AddRange(laserEnemyResult.lasers);
            enemies.Clear();
            enemies.AddRange(laserEnemyResult.enemies);
            enemiesDestroyed = laserEnemyResult.enemiesDestroyed;

            var laserBulletResult = gameLogic.ProcessLaserBulletCollisions(lasers, enemyBullets, bulletsDestroyed);
            lasers.Clear();
            lasers.AddRange(laserBulletResult.lasers);
            enemyBullets.Clear();
            enemyBullets.AddRange(laserBulletResult.bullets);
            bulletsDestroyed = laserBulletResult.bulletsDestroyed;

            // Check enemy-player collisions
            CheckEnemyPlayerCollisions();

            // Check bullet-player collisions
            CheckBulletPlayerCollisions();

            Invalidate();
        }

        private void CheckEnemyPlayerCollisions()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (CollisionDetector.CheckPlayerEnemyCollision(player, enemies[i]))
                {
                    // No damage from enemy collision anymore
                    // Just remove the enemy when it touches the player
                    enemies.RemoveAt(i);
                    Console.WriteLine("👥 Enemy removed on player contact (no damage)");
                }
            }
        }

        private void CheckBulletPlayerCollisions()
        {
            for (int i = enemyBullets.Count - 1; i >= 0; i--)
            {
                if (CollisionDetector.CheckBulletPlayerCollision(enemyBullets[i], player))
                {
                    lives--;
                    enemyBullets.RemoveAt(i);

                    // Reward nearby enemies for successful hit
                    foreach (var enemy in enemies)
                    {
                        float distance = CollisionDetector.Distance(enemy.Position, player.Position);
                        if (distance < 200f && enemy.LearningId >= 0)
                        {
                            enemyLearning.EnemyHitPlayer(enemy.LearningId);
                        }
                    }

                    if (lives <= 0)
                    {
                        EndGame();
                    }
                }
            }
        }

        private void EndGame()
        {
            gameOver = true;
            voiceController.Speak("Game Over!");

            var finalSnapshot = new GameStateSnapshot(lives, enemiesDestroyed, bulletsDestroyed, DateTime.Now);
            trainingCollector.EndEpisode(finalSnapshot);

            // Save enemy learning models
            enemyLearning.SaveModels();
            Console.WriteLine("🧠 Enemy learning data saved");
        }

        private void AIShootingTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            // Get current game state for AI
            var state = aiAgent.GetStateVector(player, enemies, enemyBullets, ClientSize);
            var action = aiAgent.ChooseAction(state);
            var laserVelocity = aiAgent.ExecuteAction(action, player, enemies);

            if (laserVelocity.HasValue)
            {
                lasers.Add(new Laser(player.Position, laserVelocity.Value));
                Console.WriteLine($"🎯 AI Action: {action}");
            }

            // Record training data
            var snapshot = new GameStateSnapshot(lives, enemiesDestroyed, bulletsDestroyed, DateTime.Now);
            trainingCollector.RecordStep(state, action, snapshot);
        }

        private void EnemySpawnTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            var newEnemy = gameLogic.SpawnEnemy(player, ClientSize, enemyLearning);
            if (newEnemy != null)
            {
                enemies.Add(newEnemy);
            }
        }

        private void RandomMovementTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            // Generate random movement direction
            var directions = new[]
            {
                new PointF(0, -GameConstants.PlayerSpeed), // North
                new PointF(0, GameConstants.PlayerSpeed),  // South
                new PointF(GameConstants.PlayerSpeed, 0),  // East
                new PointF(-GameConstants.PlayerSpeed, 0), // West
                new PointF(GameConstants.PlayerSpeed, -GameConstants.PlayerSpeed), // Northeast
                new PointF(-GameConstants.PlayerSpeed, -GameConstants.PlayerSpeed), // Northwest
                new PointF(GameConstants.PlayerSpeed, GameConstants.PlayerSpeed), // Southeast
                new PointF(-GameConstants.PlayerSpeed, GameConstants.PlayerSpeed), // Southwest
                PointF.Empty // Stop
            };

            var randomDirection = directions[random.Next(directions.Length)];
            player = player with { Velocity = randomDirection };

            Console.WriteLine($"🎲 Random movement: {GetDirectionName(randomDirection)}");
        }

        private string GetDirectionName(PointF velocity)
        {
            if (velocity == PointF.Empty) return "STOP";
            if (velocity.X == 0 && velocity.Y < 0) return "NORTH";
            if (velocity.X == 0 && velocity.Y > 0) return "SOUTH";
            if (velocity.X > 0 && velocity.Y == 0) return "EAST";
            if (velocity.X < 0 && velocity.Y == 0) return "WEST";
            if (velocity.X > 0 && velocity.Y < 0) return "NORTHEAST";
            if (velocity.X < 0 && velocity.Y < 0) return "NORTHWEST";
            if (velocity.X > 0 && velocity.Y > 0) return "SOUTHEAST";
            if (velocity.X < 0 && velocity.Y > 0) return "SOUTHWEST";
            return "UNKNOWN";
        }

        private void GameForm_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Black);

            // Draw obstacles first (background)
            obstacleManager.DrawObstacles(g);

            // Draw player
            g.FillEllipse(Brushes.Cyan,
                player.Position.X - GameConstants.PlayerRadius,
                player.Position.Y - GameConstants.PlayerRadius,
                GameConstants.PlayerRadius * 2,
                GameConstants.PlayerRadius * 2);

            // Draw lasers
            foreach (var laser in lasers)
            {
                var endPoint = new PointF(laser.Position.X + laser.Velocity.X * 2, laser.Position.Y + laser.Velocity.Y * 2);
                g.DrawLine(Pens.Magenta, laser.Position, endPoint);
            }

            // Draw enemies
            foreach (var enemy in enemies)
            {
                float distanceToPlayer = CollisionDetector.Distance(enemy.Position, player.Position);
                Brush enemyBrush = distanceToPlayer <= GameConstants.EnemyShootRange ? Brushes.Yellow : Brushes.Red;
                g.FillEllipse(enemyBrush,
                    enemy.Position.X - GameConstants.EnemyRadius,
                    enemy.Position.Y - GameConstants.EnemyRadius,
                    GameConstants.EnemyRadius * 2,
                    GameConstants.EnemyRadius * 2);
            }

            // Draw enemy bullets
            foreach (var bullet in enemyBullets)
            {
                g.FillEllipse(Brushes.Orange, bullet.Position.X - 3, bullet.Position.Y - 3, 6, 6);
            }

            DrawUI(g);
        }

        private void DrawUI(Graphics g)
        {
            var font = new Font("Arial", 14, FontStyle.Bold);
            var smallFont = new Font("Arial", 10, FontStyle.Regular);

            g.DrawString($"Lives: {lives}", font, Brushes.White, 10, 10);
            g.DrawString($"Enemies Destroyed: {enemiesDestroyed}", smallFont, Brushes.Lime, 10, 35);
            g.DrawString($"Bullets Destroyed: {bulletsDestroyed}", smallFont, Brushes.Cyan, 10, 50);
            g.DrawString("AI is learning to shoot!", smallFont, Brushes.Yellow, 10, 70);
            g.DrawString("Random movement enabled!", smallFont, Brushes.Orange, 10, 85);
            g.DrawString("Enemies don't damage on contact", smallFont, Brushes.LightGreen, 10, 100);
            g.DrawString("Voice: north/south/east/west to move", smallFont, Brushes.White, 10, ClientSize.Height - 40);

            if (gameOver)
            {
                var largeFont = new Font("Arial", 40, FontStyle.Bold);
                var mediumFont = new Font("Arial", 16, FontStyle.Bold);
                g.DrawString("GAME OVER", largeFont, Brushes.Red, ClientSize.Width / 2 - 150, ClientSize.Height / 2 - 40);
                g.DrawString("Press 'R' to restart and continue AI training", mediumFont, Brushes.White, ClientSize.Width / 2 - 200, ClientSize.Height / 2 + 20);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            gameTimer.Stop();
            aiShootingTimer.Stop();
            enemySpawnTimer.Stop();
            randomMovementTimer.Stop();

            var finalSnapshot = new GameStateSnapshot(lives, enemiesDestroyed, bulletsDestroyed, DateTime.Now);
            trainingCollector.EndEpisode(finalSnapshot);

            voiceController.Dispose();
            base.OnFormClosing(e);
        }
    }
}