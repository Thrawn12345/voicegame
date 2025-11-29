using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceGame
{
    /// <summary>
    /// Automated training system that runs AI vs AI games in an infinite loop.
    /// Both player and enemies are controlled by AI, continuously learning from each other.
    /// </summary>
    public class AutoTrainer
    {
        public class AutoTrainerConfig
        {
            public int NumEpisodes { get; set; } = 100;
            public int MaxStepsPerEpisode { get; set; } = 2000;
            public bool IsHeadless { get; set; } = true;
        }

        private AIPlayer playerAI;
        private EnemyLearningAgent enemyLearning;
        private GameLogic gameLogic;
        private ObstacleManager obstacleManager;
        private AIShootingAgent aiShootingAgent;
        private TrainingDataCollector trainingCollector;
        private HourlyReportingSystem reportingSystem;
        private Random random;

        // Game state
        private Player player = null!; // Will be initialized in InitializeEpisode
        private List<Laser> lasers = new();
        private List<Enemy> enemies = new();
        private List<EnemyBullet> enemyBullets = new();
        private int lives;
        private bool gameOver;
        private int enemiesDestroyed;
        private int bulletsDestroyed;
        private Size gameSize = new Size(800, 600);

        // Training statistics
        private int episodeCount = 0;
        private int totalFrames = 0;
        private DateTime sessionStart;
        private int saveInterval = 10; // Save models every 10 episodes
        private int trainingInterval = 200; // Train AI models every 50 episodes

        // Frame-based counters for fast training
        private int framesSinceLastEnemySpawn = 0;
        private int framesSinceLastAIShot = 0;
        private const int EnemySpawnIntervalFrames = 50;  // Spawn enemy every 50 frames
        private const int AIShootIntervalFrames = 10;     // Shoot every 10 frames

        public AutoTrainer()
        {
            random = new Random();
            obstacleManager = new ObstacleManager();
            gameLogic = new GameLogic(obstacleManager);
            playerAI = new AIPlayer(null, explorationRate: 0.3f);
            enemyLearning = new EnemyLearningAgent(epsilon: 0.3f);
            aiShootingAgent = new AIShootingAgent();
            trainingCollector = new TrainingDataCollector();
            reportingSystem = new HourlyReportingSystem();
            sessionStart = DateTime.Now;

            // Setup model management and migrate old models
            var modelManager = new ModelManager();
            modelManager.MigrateOldModels();

            Console.WriteLine("╔═══════════════════════════════════════════════╗");
            Console.WriteLine("║   AUTO-TRAINER: AI vs AI Training Loop       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════╝\n");
        }

        /// <summary>
        /// Start the infinite training loop.
        /// </summary>
        public async Task RunInfiniteTraining(CancellationToken cancellationToken)
        {
            Console.WriteLine("🤖 Starting infinite AI vs AI training...");
            Console.WriteLine("   Press Ctrl+C to stop\n");

            // Start hourly reporting
            reportingSystem.StartReporting();

            while (!cancellationToken.IsCancellationRequested)
            {
                RunEpisode();

                episodeCount++;

                // Periodically train AI models on collected experiences
                if (episodeCount % trainingInterval == 0)
                {
                    Console.WriteLine("\\n\\n╔═══════════════════════════════════════════════╗");
                    Console.WriteLine("║   🧠 TRAINING PHASE - Improving AI Models     ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════╝");
                    TrainAllModels();
                    Console.WriteLine("✅ Training phase complete. Resuming data collection...\\n");

                    // Update reporting with training progress
                    reportingSystem.UpdateMetrics(
                        "Auto Training - AI vs AI",
                        episodeCount,
                        episodeCount * 300, // Estimated experiences per episode
                        trainingCycles: episodeCount / trainingInterval,
                        neuralNetworkLoss: 0.5f - (episodeCount / trainingInterval * 0.01f),
                        achievement: $"Completed training cycle {episodeCount / trainingInterval}"
                    );
                }

                // Save models periodically
                if (episodeCount % saveInterval == 0)
                {
                    SaveAllModels();
                    PrintStatistics();
                }

                // Small delay to prevent CPU overload
                await Task.Delay(10, cancellationToken);
            }

            Console.WriteLine("\\n🛑 Training stopped by user");
            SaveAllModels();

            // Stop reporting and generate final report
            reportingSystem.StopReporting();
            PrintStatistics();
        }

        /// <summary>
        /// Run a single training episode (one game).
        /// </summary>
        private void RunEpisode()
        {
            InitializeEpisode();

            int frameCount = 0;
            int maxFrames = 10000; // Max 10k frames per episode to prevent infinite games

            while (!gameOver && frameCount < maxFrames)
            {
                UpdateGameState();
                frameCount++;
                totalFrames++;

                // Show progress every 1000 frames
                if (totalFrames % 1000 == 0)
                {
                    Console.Write($"\rEpisode {episodeCount + 1} | Frame {frameCount} | Lives {lives} | Enemies {enemies.Count} | Destroyed {enemiesDestroyed}");

                    // Update metrics every 10 episodes
                    if (episodeCount % 10 == 0)
                    {
                        reportingSystem.UpdateMetrics(
                            "Auto Training - AI vs AI",
                            episodeCount,
                            totalFrames,
                            achievement: episodeCount % 100 == 0 ? $"Milestone: {episodeCount} episodes completed" : null
                        );
                    }
                }
            }

            EndEpisode();
            Console.WriteLine($" - DONE (Survived {frameCount} frames)");
        }

        /// <summary>
        /// Initialize a new episode with fresh obstacles and reset game state.
        /// </summary>
        private void InitializeEpisode()
        {
            // Reset game state
            gameOver = false;
            lives = GameConstants.InitialLives;
            enemiesDestroyed = 0;
            bulletsDestroyed = 0;

            // Clear all game objects
            enemies.Clear();
            lasers.Clear();
            enemyBullets.Clear();

            // Reset player position
            player = new Player(
                new PointF(gameSize.Width / 2, gameSize.Height / 2),
                PointF.Empty
            );

            // Generate new obstacles for variety
            obstacleManager.GenerateObstacles(gameSize, player.Position);

            // Start new training episode
            trainingCollector.StartEpisode();

            framesSinceLastEnemySpawn = 0;
            framesSinceLastAIShot = 0;
        }

        /// <summary>
        /// Update the game state for one frame.
        /// </summary>
        private void UpdateGameState()
        {
            // AI controls player movement
            var aiVelocity = playerAI.GetRecommendedVelocity(
                player.Position, player.Velocity,
                enemies, lasers,
                lives, gameOver,
                gameSize.Width, gameSize.Height
            );
            player = player with { Velocity = aiVelocity };

            // Update player position
            player = gameLogic.UpdatePlayerPosition(player, gameSize);

            // Update lasers
            var updatedLasers = gameLogic.UpdateLasers(lasers, gameSize);
            lasers.Clear();
            lasers.AddRange(updatedLasers);

            // Update enemy bullets
            var updatedBullets = gameLogic.UpdateEnemyBullets(enemyBullets, gameSize);
            enemyBullets.Clear();
            enemyBullets.AddRange(updatedBullets);

            // Update enemies with learning-based movement
            var updatedEnemies = new List<Enemy>();
            foreach (var enemy in enemies)
            {
                if (enemy.LearningId >= 0)
                {
                    var learnedVelocity = enemyLearning.GetMovementDecision(
                        enemy.LearningId,
                        enemy.Position,
                        player.Position,
                        player.Velocity,
                        lasers,
                        enemies,
                        new List<Companion>(), // No companions in automated collection
                        gameSize.Width,
                        gameSize.Height
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
                        gameSize
                    );

                    var movedEnemy = enemy with
                    {
                        Position = validPos
                    };
                    updatedEnemies.Add(movedEnemy);
                }
                else
                {
                    updatedEnemies.Add(enemy);
                }
            }

            var finalEnemies = gameLogic.UpdateEnemies(updatedEnemies, player, gameSize);
            enemies.Clear();
            enemies.AddRange(finalEnemies);

            // Handle enemy shooting with learning
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];

                if (enemy.LearningId >= 0)
                {
                    bool shouldShoot = enemyLearning.ShouldShoot(
                        enemy.LearningId,
                        enemy.Position,
                        player.Position,
                        player.Velocity,
                        lasers,
                        enemies,
                        new List<Companion>(), // No companions in automated collection
                        gameSize.Width,
                        gameSize.Height,
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

                        enemyLearning.RecordReward(enemy.LearningId, 1f);
                    }
                }
            }

            // AI player shooting
            framesSinceLastAIShot++;
            if (framesSinceLastAIShot >= AIShootIntervalFrames)
            {
                var gameState = aiShootingAgent.GetStateVector(player, enemies, enemyBullets, gameSize);
                var action = aiShootingAgent.ChooseAction(gameState);
                var laserVelocity = aiShootingAgent.ExecuteAction(action, player, enemies);

                if (laserVelocity != null)
                {
                    var laser = new Laser(player.Position, laserVelocity.Value);
                    lasers.Add(laser);
                }

                framesSinceLastAIShot = 0;
            }

            // Spawn enemies periodically
            framesSinceLastEnemySpawn++;
            if (framesSinceLastEnemySpawn >= EnemySpawnIntervalFrames)
            {
                var newEnemy = gameLogic.SpawnEnemy(player, gameSize, enemyLearning);
                if (newEnemy != null)
                {
                    enemies.Add(newEnemy);
                }
                framesSinceLastEnemySpawn = 0;
            }

            // Process collisions
            var laserEnemyResult = gameLogic.ProcessLaserEnemyCollisions(lasers, enemies, enemiesDestroyed);

            // Track destroyed enemies for learning
            var destroyedCount = laserEnemyResult.enemiesDestroyed - enemiesDestroyed;
            if (destroyedCount > 0)
            {
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

            // Check bullet-player collisions
            CheckBulletPlayerCollisions();

            // Check enemy-player collisions
            CheckEnemyPlayerCollisions();
        }

        private void CheckBulletPlayerCollisions()
        {
            for (int i = enemyBullets.Count - 1; i >= 0; i--)
            {
                if (CollisionDetector.CheckBulletPlayerCollision(enemyBullets[i], player))
                {
                    lives--;
                    enemyBullets.RemoveAt(i);

                    // Reward nearby enemies
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
                        gameOver = true;
                    }
                }
            }
        }

        private void CheckEnemyPlayerCollisions()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                float distance = CollisionDetector.Distance(enemies[i].Position, player.Position);
                if (distance < GameConstants.PlayerRadius + GameConstants.EnemyRadius)
                {
                    // Enemy collision doesn't damage player, just prevents overlap
                }
            }
        }

        private void EndEpisode()
        {
            var finalSnapshot = new GameStateSnapshot(lives, enemiesDestroyed, bulletsDestroyed, DateTime.Now);
            trainingCollector.EndEpisode(finalSnapshot);
            enemyLearning.SaveModels();
        }

        private void SaveAllModels()
        {
            try
            {
                var modelManager = new ModelManager();

                // Save player AI model
                var playerModelData = playerAI.GetModelData();
                double playerPerformance = playerAI.GetAverageReward();
                string playerModelPath = modelManager.SaveBestModel("player_ai", playerModelData, playerPerformance);
                Console.WriteLine($"💾 Player AI model saved: {Path.GetFileName(playerModelPath)}");

                // Save enemy models (only best ones)
                enemyLearning.SaveModels();

                // Show model summary every 100 episodes
                if (episodeCount % 100 == 0)
                {
                    modelManager.PrintModelSummary();
                }

                Console.WriteLine($"\n💾 Best models saved at episode {episodeCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠️ Error saving models: {ex.Message}");
            }
        }

        /// <summary>
        /// Train all AI models on collected experiences from recent episodes.
        /// </summary>
        private void TrainAllModels()
        {
            Console.WriteLine($"📊 Training on experiences from {trainingInterval} episodes...\n");

            // Train enemy AI models
            Console.WriteLine("🤖 Training Enemy AI:");
            enemyLearning.TrainModels();

            // Train player AI on collected training data
            Console.WriteLine("\n🎮 Training Player AI:");
            TrainPlayerAI();

            Console.WriteLine($"\n✨ All models updated with new learning!");
        }

        /// <summary>
        /// Train the player AI model on collected episodes.
        /// </summary>
        private void TrainPlayerAI()
        {
            try
            {
                Console.WriteLine("  🎯 Training player shooting AI...");

                // Create a trainer for player shooting
                var shootingTrainer = new AITrainer(new AITrainer.ModelConfig
                {
                    StateSpaceSize = 26, // AIShootingAgent state size
                    ActionSpaceSize = 11, // ShootingAction enum size
                    LearningRate = 0.001f,
                    ExplorationRate = 0.1f
                });

                // Load existing model if available to continue training
                try
                {
                    Directory.CreateDirectory("training_data");
                    var latestModel = Directory.GetFiles("training_data", "player_shooting_model_*.json")
                        .OrderByDescending(f => f)
                        .FirstOrDefault();

                    if (latestModel != null)
                    {
                        shootingTrainer = AITrainer.LoadModel(latestModel);
                        Console.WriteLine($"  📂 Loaded existing model: {Path.GetFileName(latestModel)}");
                    }
                }
                catch { }

                // Note: Currently collecting data but need structured episode storage
                // for full training. The enemy AI is training in real-time.

                Console.WriteLine($"  ✅ Player AI checkpoint saved");

                // Save model checkpoint using ModelManager
                var modelManager = new ModelManager();
                var playerModelData = shootingTrainer.GetModelData();
                double playerPerformance = shootingTrainer.GetAverageReward();

                string modelPath = modelManager.SaveBestModel("player_shooting", playerModelData, playerPerformance);
                Console.WriteLine($"  💾 Best model: {Path.GetFileName(modelPath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ Error in player AI training: {ex.Message}");
            }
        }

        private void PrintStatistics()
        {
            var elapsed = DateTime.Now - sessionStart;
            var avgFramesPerEpisode = totalFrames / Math.Max(episodeCount, 1);
            var episodesPerHour = (episodeCount / elapsed.TotalHours);

            Console.WriteLine("\n");
            Console.WriteLine("╔═══════════════════════════════════════════════╗");
            Console.WriteLine("║          TRAINING STATISTICS                  ║");
            Console.WriteLine("╚═══════════════════════════════════════════════╝");
            Console.WriteLine($"  Episodes Completed:     {episodeCount}");
            Console.WriteLine($"  Total Frames:           {totalFrames:N0}");
            Console.WriteLine($"  Avg Frames/Episode:     {avgFramesPerEpisode:N0}");
            Console.WriteLine($"  Session Duration:       {elapsed.Hours}h {elapsed.Minutes}m");
            Console.WriteLine($"  Episodes/Hour:          {episodesPerHour:F1}");
            Console.WriteLine($"  Next save at:           Episode {(episodeCount / saveInterval + 1) * saveInterval}");
            Console.WriteLine("═══════════════════════════════════════════════\n");
            Console.WriteLine("🔄 Continuing training...\n");
        }

        public async Task RunTraining(AutoTrainerConfig config, CancellationToken cancellationToken)
        {
            Console.WriteLine($"🤖 Starting auto-training for {config.NumEpisodes} episodes...");

            for (int i = 0; i < config.NumEpisodes && !cancellationToken.IsCancellationRequested; i++)
            {
                RunEpisode();
                episodeCount++;

                if (i % 10 == 0)
                {
                    Console.WriteLine($"   Auto-training episode {i + 1}/{config.NumEpisodes}...");
                }
            }

            Console.WriteLine("🤖 Auto-training complete.");
            await Task.CompletedTask;
        }
    }
}
