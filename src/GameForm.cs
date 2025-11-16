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
        private readonly List<Laser> companionBullets = new(); // Companion bullets (separate from player lasers)
        private readonly List<Companion> companions = new();
        private int lives = GameConstants.InitialLives;
        private bool gameOver = false;

        // Game systems
        private readonly ObstacleManager obstacleManager = new();
        private GameLogic gameLogic = null!;
        private VoiceController voiceController = null!;
        private readonly AIShootingAgent aiAgent = new();
        private readonly TrainingDataCollector trainingCollector = new();
        private readonly EnemyLearningAgent enemyLearning = new();
        private readonly PathfindingSystem pathfinding = new();
        private readonly FormationAI formationAI = new();
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
            
            // Make the game fullscreen
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            
            DoubleBuffered = true;
            BackColor = Color.Black;
            KeyPreview = true;

            Paint += GameForm_Paint;
            KeyDown += GameForm_KeyDown;
            MouseDown += GameForm_MouseDown;
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
            
            // Make sure player is positioned first, then initialize companions
            if (player.Position == PointF.Empty)
            {
                player = new Player(new PointF(ClientSize.Width / 2, ClientSize.Height / 2), PointF.Empty);
            }
            
            InitializeCompanions();
        }

        private void InitializeCompanions()
        {
            companions.Clear();
            
            // Create 3 companions with different roles
            float playerX = ClientSize.Width / 2f;
            float playerY = ClientSize.Height / 2f;
            
            // Companion 1: Left flank (left side support)
            companions.Add(new Companion(
                Position: new PointF(playerX - 80, playerY - 40),
                Velocity: PointF.Empty,
                Id: 1,
                Role: CompanionRole.LeftFlank,
                FormationTarget: PointF.Empty,
                LastShotTime: DateTime.MinValue
            ));
            
            // Companion 2: Right flank (right side support)
            companions.Add(new Companion(
                Position: new PointF(playerX + 80, playerY - 40),
                Velocity: PointF.Empty,
                Id: 2,
                Role: CompanionRole.RightFlank,
                FormationTarget: PointF.Empty,
                LastShotTime: DateTime.MinValue
            ));
            
            // Companion 3: Rear support (rear positioning)
            companions.Add(new Companion(
                Position: new PointF(playerX, playerY - 100),
                Velocity: PointF.Empty,
                Id: 3,
                Role: CompanionRole.Rear,
                FormationTarget: PointF.Empty,
                LastShotTime: DateTime.MinValue
            ));
            
            Console.WriteLine($"🤖 Initialized {companions.Count} AI companions at screen center ({playerX}, {playerY})");
            
            // Debug: Print companion positions
            foreach (var companion in companions)
            {
                Console.WriteLine($"   Companion {companion.Id} ({companion.Role}): Position ({companion.Position.X:F1}, {companion.Position.Y:F1})");
            }
        }

        private void OnVelocityChange(PointF velocity)
        {
            // Only allow manual velocity changes when AI is disabled
            if (!aiMode)
            {
                // Cancel target movement if manual velocity change
                player = player with { Velocity = velocity, TargetPosition = null, IsMovingToTarget = false };
                Console.WriteLine($"🎤 Manual movement: velocity set to ({velocity.X:F1}, {velocity.Y:F1})");
            }
            else
            {
                Console.WriteLine("🤖 AI Mode active - ignoring manual input");
            }
        }

        private void GameForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (!aiMode)
                {
                    // Set target position for intelligent movement (manual mode only)
                    PointF targetPos = new PointF(e.X, e.Y);
                    player = player with { TargetPosition = targetPos, IsMovingToTarget = true };
                    Console.WriteLine($"🎯 Target set to: ({e.X}, {e.Y})");
                }
                else
                {
                    Console.WriteLine("🤖 AI Mode active - right-click disabled");
                }
            }
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
            else if (e.KeyCode == Keys.Escape)
            {
                // Exit fullscreen or close application
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = FormBorderStyle.Sizable;
                    TopMost = false;
                    Size = new Size(1200, 800); // Larger windowed size
                    StartPosition = FormStartPosition.CenterScreen;
                    Console.WriteLine("🖼️ Switched to windowed mode");
                }
                else
                {
                    Close();
                }
            }
            else if (e.KeyCode == Keys.F11)
            {
                // Toggle fullscreen
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = FormBorderStyle.Sizable;
                    TopMost = false;
                    Size = new Size(1200, 800);
                    StartPosition = FormStartPosition.CenterScreen;
                    Console.WriteLine("🖼️ Switched to windowed mode");
                }
                else
                {
                    WindowState = FormWindowState.Maximized;
                    FormBorderStyle = FormBorderStyle.None;
                    TopMost = true;
                    Console.WriteLine("🖥️ Switched to fullscreen mode");
                }
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
            companionBullets.Clear();
            companions.Clear();

            // Reset player position
            player = new Player(new PointF(Width / 2, Height / 2), PointF.Empty);

            // Regenerate obstacles
            obstacleManager.GenerateObstacles(ClientSize, player.Position);

            // Reinitialize companions
            InitializeCompanions();

            // Start new AI training episode with companion awareness
            trainingCollector.StartEpisode();
            Console.WriteLine($"🔄 New training episode started with {companions.Count} companions");

            Console.WriteLine("🔄 Game restarted - New AI training episode begun!");
            voiceController.Speak("New game started!");
        }

        private void ToggleAIMode()
        {
            // Initialize AI player on first use
            if (aiPlayer == null)
            {
                // Try to load the best player model from models folder
                var modelManager = new ModelManager();
                string bestModelPath = modelManager.GetBestModelPath("player_ai");
                
                if (string.IsNullOrEmpty(bestModelPath))
                {
                    // Fallback to old location for backward compatibility
                    string trainingDir = "training_data";
                    if (System.IO.Directory.Exists(trainingDir))
                    {
                        string[] modelFiles = System.IO.Directory.GetFiles(trainingDir, "ai_model_*.json");
                        bestModelPath = modelFiles.Length > 0 ? modelFiles.OrderByDescending(f => f).FirstOrDefault() ?? string.Empty : string.Empty;
                    }
                }

                aiPlayer = new AIPlayer(bestModelPath, explorationRate: 0.1f);
            }
            
            aiMode = !aiMode;

            // Reset player state when switching modes
            player = player with { 
                Velocity = PointF.Empty, 
                TargetPosition = null, 
                IsMovingToTarget = false 
            };

            if (aiMode)
            {
                // Disable voice control when AI takes over
                Text = "🤖 VOICE GAME - AI CONTROL MODE 🤖 (Press 'A' to disable)";
                voiceController.SetEnabled(false);  // Disable voice recognition
            }
            else
            {
                // Re-enable voice control
                Text = "🎤 VOICE GAME - MANUAL CONTROL MODE 🎤 (Press 'A' for AI)";
                voiceController.SetEnabled(true);   // Enable voice recognition
            }

            Console.WriteLine(aiMode ? "🤖 AI MODE ENABLED - All manual input disabled" : "🎤 MANUAL MODE ENABLED - Voice & right-click active");
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            // Clear movement if switching from AI to manual mid-game
            if (!aiMode && !player.IsMovingToTarget)
            {
                // In manual mode, only move if explicitly commanded (voice or target)
                // This prevents residual AI movement
            }

            // AI Mode: Let AI control player movement (highest priority)
            if (aiMode && aiPlayer != null)
            {
                var aiVelocity = aiPlayer.GetRecommendedVelocity(
                    player.Position, player.Velocity,
                    enemies, lasers,
                    lives, gameOver,
                    ClientSize.Width, ClientSize.Height,
                    player.TargetPosition,
                    enemyBullets,
                    obstacleManager.GetObstacles(),
                    companions,
                    formationAI.GetCurrentFormation(),
                    CalculateFormationThreatLevel(),
                    companions.Count > 0
                );
                player = player with { Velocity = aiVelocity, TargetPosition = null, IsMovingToTarget = false };
            }
            // Manual Mode: Handle target-based movement and position holding
            else if (!aiMode && player.TargetPosition.HasValue)
            {
                // If not actively moving to target, maintain position near target with small patrol movements
                if (!player.IsMovingToTarget)
                {
                    var holdPosition = player.TargetPosition.Value;
                    var distanceFromHoldPosition = Math.Sqrt(
                        Math.Pow(holdPosition.X - player.Position.X, 2) + 
                        Math.Pow(holdPosition.Y - player.Position.Y, 2)
                    );
                    
                    // If drifted too far from hold position, gently move back
                    if (distanceFromHoldPosition > 25f)
                    {
                        var returnDirection = pathfinding.GetOptimalVelocity(
                            player.Position, 
                            holdPosition,
                            enemies,
                            enemyBullets,
                            obstacleManager.GetObstacles(),
                            ClientSize.Width,
                            ClientSize.Height
                        );
                        player = player with { Velocity = new PointF(returnDirection.X * 0.3f, returnDirection.Y * 0.3f) }; // Slow return speed
                    }
                    else
                    {
                        // Small random patrol movements to stay alert
                        if (Random.Shared.Next(100) < 5) // 5% chance each frame
                        {
                            var patrolVel = new PointF(
                                (Random.Shared.NextSingle() - 0.5f) * GameConstants.PlayerSpeed * 0.2f,
                                (Random.Shared.NextSingle() - 0.5f) * GameConstants.PlayerSpeed * 0.2f
                            );
                            player = player with { Velocity = patrolVel };
                        }
                    }
                }
            }
            else if (!aiMode && player.IsMovingToTarget && player.TargetPosition.HasValue)
            {
                var optimalVelocity = pathfinding.GetOptimalVelocity(
                    player.Position,
                    player.TargetPosition.Value,
                    enemies,
                    enemyBullets,
                    obstacleManager.GetObstacles(),
                    ClientSize.Width,
                    ClientSize.Height
                );

                player = player with { Velocity = optimalVelocity };

                // Check if target reached
                float distanceToTarget = (float)Math.Sqrt(
                    Math.Pow(player.TargetPosition.Value.X - player.Position.X, 2) +
                    Math.Pow(player.TargetPosition.Value.Y - player.Position.Y, 2)
                );

                if (distanceToTarget < 15f)
                {
                    // Close enough to target, slow down but maintain position near target
                    var holdPosition = player.TargetPosition.Value;
                    player = player with { 
                        TargetPosition = holdPosition, 
                        IsMovingToTarget = false, 
                        Velocity = new PointF(optimalVelocity.X * 0.1f, optimalVelocity.Y * 0.1f) // Slow movement to maintain position
                    };
                    Console.WriteLine("🎯 Target reached! Holding position.");
                }
            }
            // Manual Mode: Voice control only (when no AI and no target movement)
            else if (!aiMode)
            {
                // Voice control velocity is set via OnVelocityChange
                // No additional processing needed here
            }

            // Update game objects using GameLogic
            player = gameLogic.UpdatePlayerPosition(player, ClientSize);

            // Update companions with formation AI
            var updatedCompanions = formationAI.UpdateCompanions(
                companions,
                player,
                enemies,
                enemyBullets,
                obstacleManager.GetObstacles(),
                ClientSize.Width,
                ClientSize.Height
            );
            companions.Clear();
            companions.AddRange(updatedCompanions);
            
            Console.WriteLine($"🤖 Updated {companions.Count} companions with formation AI");

            // Handle companion collisions with enemies and bullets
            HandleCompanionCollisions();

            var updatedLasers = gameLogic.UpdateLasers(lasers, ClientSize);
            lasers.Clear();
            lasers.AddRange(updatedLasers);

            // Update companion bullets
            var updatedCompanionBullets = gameLogic.UpdateLasers(companionBullets, ClientSize);
            companionBullets.Clear();
            companionBullets.AddRange(updatedCompanionBullets);

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

            // Enhanced final snapshot with companion performance and bullet effectiveness
            int companionsLost = 3 - companions.Count;
            int companionBulletsUsed = companionBullets.Count;
            var survivalBonus = (int)(GetCompanionSurvivalEffectiveness() * 10);
            
            var finalSnapshot = new GameStateSnapshot(
                lives, 
                enemiesDestroyed + (companions.Count * 2) + survivalBonus, // Bonus for survival and effectiveness
                bulletsDestroyed + companionBulletsUsed, // Include companion bullets in training data
                DateTime.Now
            );
            trainingCollector.EndEpisode(finalSnapshot);
            
            Console.WriteLine($"🏆 Training Episode Complete: {enemiesDestroyed} enemies, {companionsLost} companions lost, {companionBulletsUsed} companion shots");

            // Save enemy learning models
            enemyLearning.SaveModels();
            Console.WriteLine("🧠 Enemy learning data saved");
        }

        private void AIShootingTimer_Tick(object? sender, EventArgs e)
        {
            if (gameOver) return;

            // Player AI shooting (with companion-aware state including bullets)
            var enhancedState = GetEnhancedStateForTraining();
            var allBullets = enemyBullets.Concat(companionBullets.Select(cb => new EnemyBullet(cb.Position, cb.Velocity))).ToList();
            var state = aiAgent.GetStateVector(player, enemies, allBullets, ClientSize);
            var action = aiAgent.ChooseAction(state);
            var laserVelocity = aiAgent.ExecuteAction(action, player, enemies);

            if (laserVelocity.HasValue)
            {
                lasers.Add(new Laser(player.Position, laserVelocity.Value));
                Console.WriteLine($"🎯 AI Action: {action}");
            }

            // Companion AI shooting
            HandleCompanionShooting();
        }

        private void HandleCompanionShooting()
        {
            foreach (var companion in companions.ToList())
            {
                if (ShouldCompanionShoot(companion))
                {
                    var target = FindBestTargetForCompanion(companion);
                    if (target != null)
                    {
                        // Calculate shooting direction
                        float dx = target.Position.X - companion.Position.X;
                        float dy = target.Position.Y - companion.Position.Y;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (distance > 0)
                        {
                            // Normalize and apply laser speed
                            float laserSpeed = GameConstants.LaserSpeed;
                            var laserVel = new PointF(
                                (dx / distance) * laserSpeed,
                                (dy / distance) * laserSpeed
                            );

                            // Create companion bullet (different color from player lasers)
                            companionBullets.Add(new Laser(companion.Position, laserVel));

                            // Update last shot time
                            var updatedCompanion = companion with { LastShotTime = DateTime.UtcNow };
                            int index = companions.IndexOf(companion);
                            companions[index] = updatedCompanion;

                            Console.WriteLine($"🤖 Companion {companion.Id} ({companion.Role}) fired at enemy");
                        }
                    }
                }
            }
        }

        private bool ShouldCompanionShoot(Companion companion)
        {
            // Check cooldown period - more frequent shooting
            var timeSinceLastShot = DateTime.UtcNow - companion.LastShotTime;
            var cooldownPeriod = TimeSpan.FromMilliseconds(200); // Faster shooting for more action
            
            if (timeSinceLastShot < cooldownPeriod) return false;
            
            // Higher chance to shoot if enemies are present
            if (enemies.Count > 0)
            {
                // Find closest enemy within shooting range
                var closestEnemyDistance = enemies.Min(e => 
                    Math.Sqrt(Math.Pow(e.Position.X - companion.Position.X, 2) + 
                             Math.Pow(e.Position.Y - companion.Position.Y, 2)));
                
                // Shoot more aggressively when enemies are close
                if (closestEnemyDistance < 150) return true;  // Always shoot if enemy is close
                if (closestEnemyDistance < 300) return Random.Shared.Next(100) < 70; // 70% chance
                return Random.Shared.Next(100) < 40; // 40% chance for distant enemies
            }
            
            return false;
        }

        private Enemy? FindBestTargetForCompanion(Companion companion)
        {
            Enemy? bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (var enemy in enemies)
            {
                float distance = (float)Math.Sqrt(
                    Math.Pow(enemy.Position.X - companion.Position.X, 2) +
                    Math.Pow(enemy.Position.Y - companion.Position.Y, 2)
                );

                // Companions have limited range
                if (distance <= 200f)
                {
                    // Score based on distance and threat level
                    float score = distance;
                    
                    // Prefer enemies closer to player (higher threat)
                    float distanceToPlayer = (float)Math.Sqrt(
                        Math.Pow(enemy.Position.X - player.Position.X, 2) +
                        Math.Pow(enemy.Position.Y - player.Position.Y, 2)
                    );
                    score -= distanceToPlayer * 0.5f; // Reduce score for enemies closer to player

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTarget = enemy;
                    }
                }
            }

            return bestTarget;
        }

        private void HandleCompanionCollisions()
        {
            var companionsToRemove = new List<Companion>();

            foreach (var companion in companions)
            {
                // Check collision with enemies
                foreach (var enemy in enemies)
                {
                    float distance = CollisionDetector.Distance(companion.Position, enemy.Position);
                    if (distance <= GameConstants.PlayerRadius + GameConstants.EnemyRadius)
                    {
                        companionsToRemove.Add(companion);
                        Console.WriteLine($"💀 Companion {companion.Id} ({companion.Role}) destroyed by enemy contact!");
                        break;
                    }
                }

                // Check collision with enemy bullets
                if (!companionsToRemove.Contains(companion))
                {
                    foreach (var bullet in enemyBullets.ToList())
                    {
                        float distance = CollisionDetector.Distance(companion.Position, bullet.Position);
                        if (distance <= GameConstants.PlayerRadius + 3f) // Bullet radius is ~3
                        {
                            companionsToRemove.Add(companion);
                            enemyBullets.Remove(bullet);
                            Console.WriteLine($"💀 Companion {companion.Id} ({companion.Role}) destroyed by enemy bullet!");
                            break;
                        }
                    }
                }
            }

            // Remove destroyed companions
            foreach (var companion in companionsToRemove)
            {
                companions.Remove(companion);
            }
        }

        private float CalculateFormationThreatLevel()
        {
            if (companions.Count == 0) return 0f;
            
            float threatLevel = 0f;
            
            // Count nearby enemies and bullets
            int nearbyEnemies = enemies.Count(e => 
                Math.Sqrt(Math.Pow(e.Position.X - player.Position.X, 2) + 
                         Math.Pow(e.Position.Y - player.Position.Y, 2)) < 150);
            
            int nearbyBullets = enemyBullets.Count(b => 
                Math.Sqrt(Math.Pow(b.Position.X - player.Position.X, 2) + 
                         Math.Pow(b.Position.Y - player.Position.Y, 2)) < 100);
            
            // Calculate threat based on enemy density and bullet count
            threatLevel = (nearbyEnemies * 0.2f) + (nearbyBullets * 0.1f);
            
            // Bonus threat reduction if companions are alive and in good formation
            float companionBonus = companions.Count * 0.1f;
            threatLevel = Math.Max(0, threatLevel - companionBonus);
            
            return Math.Min(threatLevel, 1f); // Cap at 1.0
        }

        private float GetCompanionSurvivalEffectiveness()
        {
            if (companions.Count == 0) return 0f;
            
            float effectiveness = 0f;
            
            foreach (var companion in companions)
            {
                // Reward companions that are alive and actively shooting
                var timeSinceLastShot = DateTime.UtcNow - companion.LastShotTime;
                bool isActivelyShooting = timeSinceLastShot.TotalSeconds < 2.0;
                
                // Reward good positioning (not too close to enemies)
                float minEnemyDistance = enemies.Count > 0 ? 
                    enemies.Min(e => (float)Math.Sqrt(Math.Pow(e.Position.X - companion.Position.X, 2) + 
                                                     Math.Pow(e.Position.Y - companion.Position.Y, 2))) : 100f;
                
                bool isSafelyPositioned = minEnemyDistance > 50f;
                
                // Calculate effectiveness score
                float companionScore = 0.2f; // Base survival bonus
                if (isActivelyShooting) companionScore += 0.15f;
                if (isSafelyPositioned) companionScore += 0.1f;
                
                effectiveness += companionScore;
            }
            
            return Math.Min(effectiveness, 1f);
        }

        private bool IsCompanionFireSupportActive()
        {
            if (companions.Count == 0) return false;
            
            // Check if any companions have shot recently (within last 3 seconds)
            foreach (var companion in companions)
            {
                var timeSinceLastShot = DateTime.UtcNow - companion.LastShotTime;
                if (timeSinceLastShot.TotalSeconds < 3.0)
                {
                    return true;
                }
            }
            
            return false;
        }

        private float[] GetEnhancedStateForTraining()
        {
            // Create enhanced state vector with companion information including bullets
            var enemyData = enemies.Select(e => (e.Position, (float)GameConstants.EnemyRadius)).ToList();
            var allLaserData = lasers.Concat(companionBullets).Select(l => (l.Position, l.Velocity)).ToList();
            
            var dataCollector = new DataCollector();
            return dataCollector.EncodeGameState(
                player.Position, 
                player.Velocity,
                enemyData, 
                allLaserData,
                lives, 
                gameOver,
                ClientSize.Width, 
                ClientSize.Height,
                player.TargetPosition,
                companions,
                formationAI.GetCurrentFormation(),
                CalculateFormationThreatLevel(),
                IsCompanionFireSupportActive()
            );
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

            // Draw target position if set
            if (player.IsMovingToTarget && player.TargetPosition.HasValue)
            {
                var target = player.TargetPosition.Value;
                
                // Draw target crosshair
                using var targetPen = new Pen(Color.LimeGreen, 3);
                float crossSize = 10f;
                g.DrawLine(targetPen, target.X - crossSize, target.Y, target.X + crossSize, target.Y);
                g.DrawLine(targetPen, target.X, target.Y - crossSize, target.X, target.Y + crossSize);
                
                // Draw circle around target
                g.DrawEllipse(targetPen, target.X - 15, target.Y - 15, 30, 30);
                
                // Draw path line from player to target
                using var pathPen = new Pen(Color.LimeGreen, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(pathPen, player.Position, target);
            }

            // Draw player lasers
            foreach (var laser in lasers)
            {
                var endPoint = new PointF(laser.Position.X + laser.Velocity.X * 2, laser.Position.Y + laser.Velocity.Y * 2);
                g.DrawLine(Pens.Magenta, laser.Position, endPoint);
            }

            // Draw companion bullets (different color)
            foreach (var bullet in companionBullets)
            {
                var endPoint = new PointF(bullet.Position.X + bullet.Velocity.X * 2, bullet.Position.Y + bullet.Velocity.Y * 2);
                g.DrawLine(Pens.Cyan, bullet.Position, endPoint); // Cyan for companion bullets
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

            // Draw companions
            DrawCompanions(g);

            DrawUI(g);
        }

        private void DrawCompanions(Graphics g)
        {
            if (companions.Count == 0)
            {
                // Debug: Show when no companions exist
                using var debugFont = new Font("Arial", 10, FontStyle.Bold);
                g.DrawString("No companions found!", debugFont, Brushes.Red, 10, 240);
                return;
            }

            // Debug: Show companion count
            using var countFont = new Font("Arial", 10, FontStyle.Bold);
            g.DrawString($"Drawing {companions.Count} companions", countFont, Brushes.White, 10, 240);

            foreach (var companion in companions)
            {
                // Choose color based on role
                Brush companionBrush = companion.Role switch
                {
                    CompanionRole.LeftFlank => Brushes.LightBlue,
                    CompanionRole.RightFlank => Brushes.LightGreen,
                    CompanionRole.Rear => Brushes.LightCoral,
                    _ => Brushes.White
                };

                // Draw companion as smaller circle than player
                float companionRadius = GameConstants.PlayerRadius * 0.8f;
                g.FillEllipse(companionBrush,
                    companion.Position.X - companionRadius,
                    companion.Position.Y - companionRadius,
                    companionRadius * 2,
                    companionRadius * 2);

                // Draw role indicator
                using var rolePen = new Pen(Color.White, 2);
                g.DrawEllipse(rolePen,
                    companion.Position.X - companionRadius,
                    companion.Position.Y - companionRadius,
                    companionRadius * 2,
                    companionRadius * 2);

                // Draw formation target if visible (debug mode)
                if (companion.FormationTarget != PointF.Empty)
                {
                    using var formationPen = new Pen(Color.Yellow, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                    g.DrawLine(formationPen, companion.Position, companion.FormationTarget);
                    g.FillEllipse(Brushes.Yellow, 
                        companion.FormationTarget.X - 2, 
                        companion.FormationTarget.Y - 2, 4, 4);
                }

                // Draw companion ID number
                using var font = new Font("Arial", 8, FontStyle.Bold);
                g.DrawString(companion.Id.ToString(), font, Brushes.Black,
                    companion.Position.X - 4, companion.Position.Y - 4);
            }
        }

        private void DrawUI(Graphics g)
        {
            var font = new Font("Arial", 14, FontStyle.Bold);
            var smallFont = new Font("Arial", 10, FontStyle.Regular);
            var mediumFont = new Font("Arial", 12, FontStyle.Bold);

            g.DrawString($"Lives: {lives}", font, Brushes.White, 10, 10);
            g.DrawString($"Enemies Destroyed: {enemiesDestroyed}", smallFont, Brushes.Lime, 10, 35);
            g.DrawString($"Bullets Destroyed: {bulletsDestroyed}", smallFont, Brushes.Cyan, 10, 50);
            g.DrawString("AI is learning to shoot!", smallFont, Brushes.Yellow, 10, 70);
            g.DrawString("Random movement enabled!", smallFont, Brushes.Orange, 10, 85);
            g.DrawString("Enemies don't damage on contact", smallFont, Brushes.LightGreen, 10, 100);
            
            // Prominent AI Mode Toggle Display
            if (aiMode)
            {
                g.DrawString("🤖 AI CONTROL: ON", mediumFont, Brushes.Red, 10, 115);
                g.DrawString("(Press 'A' to switch to Manual)", smallFont, Brushes.Red, 10, 135);
                g.DrawString("Voice & Right-click DISABLED", smallFont, Brushes.Gray, 10, 150);
            }
            else
            {
                g.DrawString("🎤 MANUAL CONTROL: ON", mediumFont, Brushes.Lime, 10, 115);
                g.DrawString("(Press 'A' to switch to AI)", smallFont, Brushes.Lime, 10, 135);
                g.DrawString("Right-click to set target position!", smallFont, Brushes.LimeGreen, 10, 150);
                g.DrawString("Voice: north/south/east/west to move", smallFont, Brushes.White, 10, 165);
            }
            
            // Movement status
            if (!aiMode && player.IsMovingToTarget && player.TargetPosition.HasValue)
            {
                g.DrawString("🎯 Moving to target with intelligent pathfinding...", smallFont, Brushes.LimeGreen, 10, 185);
            }
            else if (aiMode)
            {
                g.DrawString("🤖 AI is controlling player movement", smallFont, Brushes.Yellow, 10, 185);
            }

            // Companion formation info
            if (companions.Count > 0)
            {
                g.DrawString($"Formation: {formationAI.GetCurrentFormation()}", smallFont, Brushes.Cyan, 10, 205);
                g.DrawString("Companions: Blue=Left, Green=Right, Coral=Rear", smallFont, Brushes.White, 10, 220);
            }

            // Fullscreen controls
            g.DrawString("ESC: Exit fullscreen | F11: Toggle fullscreen", smallFont, Brushes.Gray, 10, ClientSize.Height - 60);
            g.DrawString($"Screen: {ClientSize.Width}x{ClientSize.Height}", smallFont, Brushes.Gray, 10, ClientSize.Height - 80);

            if (gameOver)
            {
                var largeFont = new Font("Arial", 40, FontStyle.Bold);
                var gameOverFont = new Font("Arial", 16, FontStyle.Bold);
                g.DrawString("GAME OVER", largeFont, Brushes.Red, ClientSize.Width / 2 - 150, ClientSize.Height / 2 - 40);
                g.DrawString("Press 'R' to restart and continue AI training", gameOverFont, Brushes.White, ClientSize.Width / 2 - 200, ClientSize.Height / 2 + 20);
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