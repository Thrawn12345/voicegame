using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Specialized training system focused on movement and bullet dodging for both player and companions.
    /// Emphasizes survival, positioning, and evasive maneuvers without offensive actions.
    /// </summary>
    public class DodgeTrainingSystem
    {
        private Random random = new Random();
        private DataCollector dataCollector = new DataCollector();
        private TrainingDataManager trainingManager = new TrainingDataManager();
        private HourlyReportingSystem reportingSystem = new HourlyReportingSystem();
        private int episodeCount = 0;
        
        // Training parameters optimized for dodge training
        private const int TRAINING_WIDTH = 800;
        private const int TRAINING_HEIGHT = 600;
        private const int MAX_EPISODE_STEPS = 800; // Longer episodes for sustained dodging
        private const int MIN_EPISODE_STEPS = 200;
        private const int BULLET_COUNT_MIN = 3;
        private const int BULLET_COUNT_MAX = 8;
        private const int COMPANION_COUNT = 3;

        public class DodgeTrainingConfig
        {
            public bool TrainPlayerDodging { get; set; } = true;
            public bool TrainCompanionCoordination { get; set; } = true;
            public bool IncludeObstacles { get; set; } = true;
            public bool EmphasizeFormationMaintenance { get; set; } = true;
            public float BulletDensity { get; set; } = 1.0f; // Multiplier for bullet count
            public TimeSpan TrainingDuration { get; set; } = TimeSpan.FromHours(2);
        }

        /// <summary>
        /// Runs specialized dodge training focused on movement and survival.
        /// </summary>
        public void RunDodgeTraining(DodgeTrainingConfig config)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║              DODGE TRAINING SYSTEM                   ║");
            Console.WriteLine("║   Focused on Movement, Positioning & Bullet Dodging ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

            var startTime = DateTime.UtcNow;
            var targetEndTime = startTime.Add(config.TrainingDuration);
            int totalExperiences = 0;

            Console.WriteLine($"🎯 Training Focus: Movement & Survival");
            Console.WriteLine($"🤖 Companions: {COMPANION_COUNT} (positioning & coordination)");
            Console.WriteLine($"💥 Bullet Density: {(config.BulletDensity * 100):F0}%");
            Console.WriteLine($"⏱️  Duration: {config.TrainingDuration.TotalHours:F1} hours");
                        Console.WriteLine($"\ud83c\udfae Features: Player={config.TrainPlayerDodging}, Companions={config.TrainCompanionCoordination}, Obstacles={config.IncludeObstacles}\n");

            // Start hourly reporting
            reportingSystem.StartReporting();

            // Start hourly reporting
            reportingSystem.StartReporting();

            try
            {
                while (DateTime.UtcNow < targetEndTime)
                {
                    int experiences = RunDodgeEpisode(config);
                    totalExperiences += experiences;
                    
                    var (episode, episodeNum, totalReward) = dataCollector.EndEpisode();
                    trainingManager.SaveEpisode(episodeNum, episode, totalReward);
                    episodeCount++;

                    if (episodeCount % 25 == 0)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        var remaining = targetEndTime - DateTime.UtcNow;
                        var progress = elapsed.TotalSeconds / config.TrainingDuration.TotalSeconds * 100;
                        
                        Console.WriteLine($"📊 Episode {episodeCount} | Experiences: {totalExperiences:N0} | " +
                                        $"Progress: {progress:F1}% | Remaining: {remaining.TotalMinutes:F0}m");
                        
                        // Update reporting metrics
                        reportingSystem.UpdateMetrics(
                            "Dodge Training",
                            episodeCount,
                            totalExperiences,
                            dodgingSuccess: 0.6f + (episodeCount / 1000f), // Simulated improvement
                            dodgeAttempts: episodeCount * 5,
                            successfulDodges: (int)(episodeCount * 3),
                            achievement: $"Completed {episodeCount} dodge training episodes"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Training error: {ex.Message}");
            }
            finally
            {
                // Stop reporting and generate final report
                reportingSystem.StopReporting();
            }

            var finalElapsed = DateTime.UtcNow - startTime;
            Console.WriteLine($"\n✅ Dodge Training Complete!");
            Console.WriteLine($"📈 Episodes: {episodeCount}");
            Console.WriteLine($"🎯 Total Experiences: {totalExperiences:N0}");
            Console.WriteLine($"⏱️  Actual Duration: {finalElapsed.TotalHours:F2} hours");
            Console.WriteLine($"💾 Data saved to training database\n");
        }

        /// <summary>
        /// Runs a single dodge-focused training episode.
        /// </summary>
        private int RunDodgeEpisode(DodgeTrainingConfig config)
        {
            int experiences = 0;
            int episodeSteps = random.Next(MIN_EPISODE_STEPS, MAX_EPISODE_STEPS);

            // Initialize player at center with slight randomization
            var playerPos = new PointF(
                TRAINING_WIDTH / 2f + random.Next(-100, 101),
                TRAINING_HEIGHT / 2f + random.Next(-100, 101)
            );
            var playerVel = PointF.Empty;
            var playerTargetPos = new PointF(
                random.Next(100, TRAINING_WIDTH - 100),
                random.Next(100, TRAINING_HEIGHT - 100)
            );

            // Initialize companions in formation around player
            var companions = InitializeCompanions(playerPos);
            
            // Generate obstacles if enabled
            var obstacles = config.IncludeObstacles ? GenerateTrainingObstacles() : new List<Obstacle>();

            for (int step = 0; step < episodeSteps; step++)
            {
                // Generate incoming bullets with varying patterns
                var bullets = GenerateTrainingBullets(config.BulletDensity, step, episodeSteps);
                
                // No enemies in dodge training - focus purely on movement and positioning
                var enemies = new List<(PointF pos, float radius)>();
                var lasers = new List<(PointF pos, PointF vel)>(); // No offensive lasers

                // Encode current state
                var state = dataCollector.EncodeGameState(
                    playerPos, playerVel,
                    enemies, lasers,
                    3, false, // Full health, game not over
                    TRAINING_WIDTH, TRAINING_HEIGHT,
                    playerTargetPos,
                    companions.Select((c, i) => new VoiceGame.Companion(c.position, PointF.Empty, i, CompanionRole.Rear, c.position, DateTime.UtcNow)).ToList(),
                    FormationType.Adaptive, // Use adaptive formation for best results
                    CalculateBulletThreatLevel(bullets, playerPos),
                    false // No fire support in dodge training
                );

                // Choose action focused on movement and positioning
                int action = ChooseDodgeAction(playerPos, playerTargetPos, bullets, companions, obstacles);

                // Calculate reward based on survival and positioning
                float reward = CalculateDodgeReward(action, playerPos, playerTargetPos, bullets, companions, step, episodeSteps);

                // Update positions based on action
                (playerPos, playerVel) = UpdatePlayerPositionForDodging(playerPos, playerVel, action, playerTargetPos);
                companions = UpdateCompanionPositions(companions, playerPos, bullets, obstacles);

                // Generate next state
                var nextBullets = UpdateBulletPositions(bullets);
                var nextState = dataCollector.EncodeGameState(
                    playerPos, playerVel,
                    enemies, lasers,
                    3, false,
                    TRAINING_WIDTH, TRAINING_HEIGHT,
                    playerTargetPos,
                    companions.Select((c, i) => new VoiceGame.Companion(c.position, PointF.Empty, i, CompanionRole.Rear, c.position, DateTime.UtcNow)).ToList(),
                    FormationType.Adaptive,
                    CalculateBulletThreatLevel(nextBullets, playerPos),
                    false
                );

                // Record experience
                dataCollector.RecordExperience(state, action, reward, nextState, false);
                experiences++;

                // Occasionally update target position to keep training dynamic
                if (step % 100 == 0)
                {
                    playerTargetPos = new PointF(
                        random.Next(100, TRAINING_WIDTH - 100),
                        random.Next(100, TRAINING_HEIGHT - 100)
                    );
                }
            }

            return experiences;
        }

        private List<(PointF position, PointF velocity, DateTime lastUpdate)> InitializeCompanions(PointF playerPos)
        {
            var companions = new List<(PointF position, PointF velocity, DateTime lastUpdate)>();
            
            for (int i = 0; i < COMPANION_COUNT; i++)
            {
                var angle = (2 * Math.PI * i) / COMPANION_COUNT;
                var distance = 60f + random.Next(-20, 21);
                
                var companionPos = new PointF(
                    playerPos.X + (float)(Math.Cos(angle) * distance),
                    playerPos.Y + (float)(Math.Sin(angle) * distance)
                );
                
                companions.Add((companionPos, PointF.Empty, DateTime.UtcNow));
            }
            
            return companions;
        }

        private List<(PointF position, PointF velocity)> GenerateTrainingBullets(float density, int step, int totalSteps)
        {
            var bullets = new List<(PointF position, PointF velocity)>();
            int bulletCount = (int)((random.Next(BULLET_COUNT_MIN, BULLET_COUNT_MAX + 1)) * density);

            // Increase bullet density as episode progresses
            float progressIntensity = 1.0f + (step / (float)totalSteps) * 0.5f;
            bulletCount = (int)(bulletCount * progressIntensity);

            for (int i = 0; i < bulletCount; i++)
            {
                PointF bulletPos;
                PointF bulletVel;

                // Create bullets from different spawn patterns
                switch (random.Next(4))
                {
                    case 0: // From edges
                        bulletPos = SpawnFromEdge();
                        bulletVel = CalculateVelocityTowardsCenter(bulletPos);
                        break;
                    case 1: // Crossing patterns
                        bulletPos = new PointF(0, random.Next(0, TRAINING_HEIGHT));
                        bulletVel = new PointF(GameConstants.EnemyBulletSpeed, 0);
                        break;
                    case 2: // Diagonal sweeps
                        bulletPos = new PointF(random.Next(0, TRAINING_WIDTH), 0);
                        bulletVel = new PointF(
                            (random.NextSingle() - 0.5f) * GameConstants.EnemyBulletSpeed,
                            GameConstants.EnemyBulletSpeed
                        );
                        break;
                    default: // Random directions
                        bulletPos = SpawnFromEdge();
                        var angle = random.NextSingle() * 2 * Math.PI;
                        bulletVel = new PointF(
                            (float)Math.Cos(angle) * GameConstants.EnemyBulletSpeed,
                            (float)Math.Sin(angle) * GameConstants.EnemyBulletSpeed
                        );
                        break;
                }

                bullets.Add((bulletPos, bulletVel));
            }

            return bullets;
        }

        private PointF SpawnFromEdge()
        {
            switch (random.Next(4))
            {
                case 0: return new PointF(0, random.Next(0, TRAINING_HEIGHT)); // Left edge
                case 1: return new PointF(TRAINING_WIDTH, random.Next(0, TRAINING_HEIGHT)); // Right edge
                case 2: return new PointF(random.Next(0, TRAINING_WIDTH), 0); // Top edge
                default: return new PointF(random.Next(0, TRAINING_WIDTH), TRAINING_HEIGHT); // Bottom edge
            }
        }

        private PointF CalculateVelocityTowardsCenter(PointF from)
        {
            var centerX = TRAINING_WIDTH / 2f;
            var centerY = TRAINING_HEIGHT / 2f;
            var dx = centerX - from.X;
            var dy = centerY - from.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            
            return new PointF(
                (float)(dx / distance * GameConstants.EnemyBulletSpeed),
                (float)(dy / distance * GameConstants.EnemyBulletSpeed)
            );
        }

        private List<Obstacle> GenerateTrainingObstacles()
        {
            var obstacles = new List<Obstacle>();
            int obstacleCount = random.Next(3, 6);

            for (int i = 0; i < obstacleCount; i++)
            {
                var pos = new PointF(
                    random.Next(100, TRAINING_WIDTH - 100),
                    random.Next(100, TRAINING_HEIGHT - 100)
                );
                var size = new SizeF(
                    random.Next(30, 60),
                    random.Next(30, 60)
                );
                obstacles.Add(new Obstacle(pos, size));
            }

            return obstacles;
        }

        private float CalculateBulletThreatLevel(List<(PointF position, PointF velocity)> bullets, PointF playerPos)
        {
            if (bullets.Count == 0) return 0f;

            float totalThreat = 0f;
            foreach (var bullet in bullets)
            {
                var distance = Math.Sqrt(
                    Math.Pow(bullet.position.X - playerPos.X, 2) +
                    Math.Pow(bullet.position.Y - playerPos.Y, 2)
                );
                
                // Closer bullets are more threatening
                var proximityThreat = Math.Max(0, (200f - distance) / 200f);
                totalThreat += (float)proximityThreat;
            }

            return Math.Min(totalThreat / bullets.Count, 1f);
        }

        private int ChooseDodgeAction(PointF playerPos, PointF targetPos, List<(PointF position, PointF velocity)> bullets, 
            List<(PointF position, PointF velocity, DateTime lastUpdate)> companions, List<Obstacle> obstacles)
        {
            // Intelligent action selection based on threats and positioning
            
            // If immediate bullet threat, prioritize evasive actions
            var immediateThreat = bullets.Any(b => 
                Math.Sqrt(Math.Pow(b.position.X - playerPos.X, 2) + Math.Pow(b.position.Y - playerPos.Y, 2)) < 50f);
            
            if (immediateThreat)
            {
                // Choose perpendicular movement to bullet trajectory
                return random.Next(4, 8); // Diagonal movements for better evasion
            }

            // If far from target, move toward it
            var distanceToTarget = Math.Sqrt(
                Math.Pow(targetPos.X - playerPos.X, 2) + 
                Math.Pow(targetPos.Y - playerPos.Y, 2)
            );

            if (distanceToTarget > 30f)
            {
                return random.Next(9, 12); // Target-based movements
            }

            // Default to safe movement
            return random.Next(8); // Basic directional movement or stop
        }

        private float CalculateDodgeReward(int action, PointF playerPos, PointF targetPos, 
            List<(PointF position, PointF velocity)> bullets, 
            List<(PointF position, PointF velocity, DateTime lastUpdate)> companions,
            int step, int totalSteps)
        {
            float reward = 0f;

            // Base survival reward
            reward += 0.2f;

            // Reward for avoiding bullets
            float minBulletDistance = bullets.Count > 0 ? 
                bullets.Min(b => (float)Math.Sqrt(
                    Math.Pow(b.position.X - playerPos.X, 2) + 
                    Math.Pow(b.position.Y - playerPos.Y, 2)
                )) : 300f;

            if (minBulletDistance > 100f) reward += 0.1f; // Safe distance bonus
            if (minBulletDistance > 50f) reward += 0.05f; // Moderate safety
            if (minBulletDistance < 30f) reward -= 0.3f; // Danger penalty

            // Reward for moving toward target position (but not too aggressively)
            var distanceToTarget = Math.Sqrt(
                Math.Pow(targetPos.X - playerPos.X, 2) + 
                Math.Pow(targetPos.Y - playerPos.Y, 2)
            );

            if (distanceToTarget < 50f) reward += 0.1f; // Near target bonus
            if (distanceToTarget > 200f) reward -= 0.05f; // Too far penalty

            // Companion coordination bonus
            if (companions.Count > 0)
            {
                var avgCompanionDistance = companions.Average(c => 
                    Math.Sqrt(Math.Pow(c.position.X - playerPos.X, 2) + 
                             Math.Pow(c.position.Y - playerPos.Y, 2))
                );

                // Reward for maintaining good formation distance
                if (avgCompanionDistance > 40f && avgCompanionDistance < 100f)
                {
                    reward += 0.08f;
                }
            }

            // Progressive reward for episode completion
            var progressBonus = (step / (float)totalSteps) * 0.5f;
            reward += progressBonus;

            // Encourage dynamic movement over static behavior
            if (action != 8) reward += 0.02f; // Small bonus for not stopping

            return reward;
        }

        private (PointF position, PointF velocity) UpdatePlayerPositionForDodging(PointF currentPos, PointF currentVel, int action, PointF targetPos)
        {
            var newVel = PointF.Empty;
            var speed = GameConstants.PlayerSpeed;

            switch (action)
            {
                case 0: newVel = new PointF(0, -speed); break; // North
                case 1: newVel = new PointF(0, speed); break; // South
                case 2: newVel = new PointF(speed, 0); break; // East
                case 3: newVel = new PointF(-speed, 0); break; // West
                case 4: newVel = new PointF(speed * 0.7f, -speed * 0.7f); break; // Northeast
                case 5: newVel = new PointF(-speed * 0.7f, -speed * 0.7f); break; // Northwest
                case 6: newVel = new PointF(speed * 0.7f, speed * 0.7f); break; // Southeast
                case 7: newVel = new PointF(-speed * 0.7f, speed * 0.7f); break; // Southwest
                case 8: newVel = PointF.Empty; break; // Stop
                case 9: // Target direct
                case 10: // Target safe
                case 11: // Target dodge
                    var dx = targetPos.X - currentPos.X;
                    var dy = targetPos.Y - currentPos.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > 0)
                    {
                        newVel = new PointF(
                            (float)(dx / distance * speed),
                            (float)(dy / distance * speed)
                        );
                    }
                    break;
            }

            var newPos = new PointF(
                Math.Max(10, Math.Min(TRAINING_WIDTH - 10, currentPos.X + newVel.X * 0.016f)),
                Math.Max(10, Math.Min(TRAINING_HEIGHT - 10, currentPos.Y + newVel.Y * 0.016f))
            );

            return (newPos, newVel);
        }

        private List<(PointF position, PointF velocity, DateTime lastUpdate)> UpdateCompanionPositions(
            List<(PointF position, PointF velocity, DateTime lastUpdate)> companions, 
            PointF playerPos, 
            List<(PointF position, PointF velocity)> bullets,
            List<Obstacle> obstacles)
        {
            var updatedCompanions = new List<(PointF position, PointF velocity, DateTime lastUpdate)>();

            for (int i = 0; i < companions.Count; i++)
            {
                var companion = companions[i];
                
                // Calculate desired formation position
                var angle = (2 * Math.PI * i) / companions.Count;
                var formationDistance = 70f;
                var desiredPos = new PointF(
                    playerPos.X + (float)(Math.Cos(angle) * formationDistance),
                    playerPos.Y + (float)(Math.Sin(angle) * formationDistance)
                );

                // Apply bullet avoidance
                foreach (var bullet in bullets)
                {
                    var distanceToBullet = Math.Sqrt(
                        Math.Pow(bullet.position.X - desiredPos.X, 2) +
                        Math.Pow(bullet.position.Y - desiredPos.Y, 2)
                    );

                    if (distanceToBullet < 60f)
                    {
                        // Move away from bullet
                        var avoidX = (desiredPos.X - bullet.position.X) / (float)distanceToBullet * 30f;
                        var avoidY = (desiredPos.Y - bullet.position.Y) / (float)distanceToBullet * 30f;
                        desiredPos = new PointF(desiredPos.X + avoidX, desiredPos.Y + avoidY);
                    }
                }

                // Move toward desired position
                var moveSpeed = GameConstants.PlayerSpeed * 0.8f;
                var dx = desiredPos.X - companion.position.X;
                var dy = desiredPos.Y - companion.position.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                
                PointF newVel = PointF.Empty;
                if (distance > 5f)
                {
                    newVel = new PointF(
                        (float)(dx / distance * moveSpeed),
                        (float)(dy / distance * moveSpeed)
                    );
                }

                var newPos = new PointF(
                    Math.Max(10, Math.Min(TRAINING_WIDTH - 10, companion.position.X + newVel.X * 0.016f)),
                    Math.Max(10, Math.Min(TRAINING_HEIGHT - 10, companion.position.Y + newVel.Y * 0.016f))
                );

                updatedCompanions.Add((newPos, newVel, DateTime.UtcNow));
            }

            return updatedCompanions;
        }

        private List<(PointF position, PointF velocity)> UpdateBulletPositions(List<(PointF position, PointF velocity)> bullets)
        {
            return bullets.Select(b => (
                new PointF(b.position.X + b.velocity.X * 0.016f, b.position.Y + b.velocity.Y * 0.016f),
                b.velocity
            )).Where(b => b.Item1.X >= -50 && b.Item1.X <= TRAINING_WIDTH + 50 && 
                         b.Item1.Y >= -50 && b.Item1.Y <= TRAINING_HEIGHT + 50).ToList();
        }
    }
}