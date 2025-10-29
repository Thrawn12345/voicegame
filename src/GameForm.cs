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

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            // Update game objects using GameLogic
            player = gameLogic.UpdatePlayerPosition(player, ClientSize);

            var updatedLasers = gameLogic.UpdateLasers(lasers, ClientSize);
            lasers.Clear();
            lasers.AddRange(updatedLasers);

            var updatedBullets = gameLogic.UpdateEnemyBullets(enemyBullets, ClientSize);
            enemyBullets.Clear();
            enemyBullets.AddRange(updatedBullets);

            var updatedEnemies = gameLogic.UpdateEnemies(enemies, player, ClientSize);
            enemies.Clear();
            enemies.AddRange(updatedEnemies);

            // Handle enemy shooting
            for (int i = 0; i < enemies.Count; i++)
            {
                var bullet = gameLogic.CreateEnemyBullet(enemies[i], player);
                if (bullet != null)
                {
                    enemyBullets.Add(bullet);
                    enemies[i] = enemies[i] with { LastShotTime = DateTime.Now };
                }
            }

            // Process collisions
            var laserEnemyResult = gameLogic.ProcessLaserEnemyCollisions(lasers, enemies, enemiesDestroyed);
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

            var newEnemy = gameLogic.SpawnEnemy(player, ClientSize);
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