using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Learning agent for enemy AI that learns both movement and shooting strategies.
    /// Uses reinforcement learning to improve enemy behavior over time.
    /// </summary>
    public class EnemyLearningAgent
    {
        private AITrainer movementTrainer;
        private AITrainer shootingTrainer;
        private DataCollector dataCollector;
        private Random random;
        private float epsilon;
        private bool isEnabled;

        // Experience tracking for each enemy
        private Dictionary<int, EnemyExperience> enemyExperiences;
        private int nextEnemyId = 0;

        private class EnemyExperience
        {
            public float[] LastState { get; set; } = Array.Empty<float>();
            public int LastMovementAction { get; set; }
            public int LastShootingAction { get; set; }
            public float AccumulatedReward { get; set; }
            public bool IsActive { get; set; } = true;
        }

        public EnemyLearningAgent(float epsilon = 0.3f)
        {
            this.epsilon = epsilon;
            this.random = new Random();
            this.dataCollector = new DataCollector();
            this.enemyExperiences = new Dictionary<int, EnemyExperience>();

            // Separate trainers for movement and shooting
            var movementConfig = new AITrainer.ModelConfig
            {
                StateSpaceSize = 23,  // Expanded to include nearest companion
                ActionSpaceSize = 9,  // 8 directions + stop
                LearningRate = 0.002f,
                ExplorationRate = epsilon
            };

            var shootingConfig = new AITrainer.ModelConfig
            {
                StateSpaceSize = 23,  // Added 3 for nearest companion
                ActionSpaceSize = 3,  // Don't shoot, shoot at target, shoot predictively
                LearningRate = 0.002f,
                ExplorationRate = epsilon
            };

            movementTrainer = new AITrainer(movementConfig);
            shootingTrainer = new AITrainer(shootingConfig);
            isEnabled = true;

            Console.WriteLine("🧠 Enemy Learning Agent initialized");
        }

        /// <summary>
        /// Register a new enemy to track its learning.
        /// </summary>
        public int RegisterEnemy()
        {
            int enemyId = nextEnemyId++;
            enemyExperiences[enemyId] = new EnemyExperience();
            return enemyId;
        }

        /// <summary>
        /// Encode enemy's perspective of the game state.
        /// </summary>
        private float[] EncodeEnemyState(
            PointF enemyPos,
            PointF playerPos,
            PointF playerVel,
            List<Laser> playerLasers,
            List<Enemy> otherEnemies,
            List<Companion> companions,
            int windowWidth,
            int windowHeight)
        {
            List<float> state = new List<float>();

            // Enemy's normalized position
            state.Add(enemyPos.X / windowWidth);
            state.Add(enemyPos.Y / windowHeight);

            // Player relative position and velocity
            float dx = (playerPos.X - enemyPos.X) / windowWidth;
            float dy = (playerPos.Y - enemyPos.Y) / windowHeight;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            state.Add(dx);
            state.Add(dy);
            state.Add(distance);
            state.Add(playerVel.X / 10f);
            state.Add(playerVel.Y / 10f);

            // Nearest player laser threat (3 values)
            if (playerLasers.Count > 0)
            {
                var nearestLaser = playerLasers
                    .OrderBy(l => Math.Sqrt(Math.Pow(l.Position.X - enemyPos.X, 2) + Math.Pow(l.Position.Y - enemyPos.Y, 2)))
                    .First();

                float laserDx = (nearestLaser.Position.X - enemyPos.X) / windowWidth;
                float laserDy = (nearestLaser.Position.Y - enemyPos.Y) / windowHeight;
                float laserDist = (float)Math.Sqrt(laserDx * laserDx + laserDy * laserDy);

                state.Add(laserDx);
                state.Add(laserDy);
                state.Add(laserDist);
            }
            else
            {
                state.Add(0f);
                state.Add(0f);
                state.Add(1f);
            }

            // Nearest ally enemy (3 values)
            var nearbyEnemies = otherEnemies
                .OrderBy(e => Math.Sqrt(Math.Pow(e.Position.X - enemyPos.X, 2) + Math.Pow(e.Position.Y - enemyPos.Y, 2)))
                .Take(1)
                .ToList();

            if (nearbyEnemies.Count > 0)
            {
                var ally = nearbyEnemies[0];
                float allyDx = (ally.Position.X - enemyPos.X) / windowWidth;
                float allyDy = (ally.Position.Y - enemyPos.Y) / windowHeight;
                float allyDist = (float)Math.Sqrt(allyDx * allyDx + allyDy * allyDy);

                state.Add(allyDx);
                state.Add(allyDy);
                state.Add(allyDist);
            }
            else
            {
                state.Add(0f);
                state.Add(0f);
                state.Add(1f);
            }

            // Number of allies and threats
            state.Add(Math.Min(otherEnemies.Count, 10) / 10f);
            state.Add(Math.Min(playerLasers.Count, 10) / 10f);

            // Nearest companion (3 values) - prioritize companions if player is dead/far
            if (companions != null && companions.Count > 0)
            {
                var nearestCompanion = companions
                    .OrderBy(c => Math.Sqrt(Math.Pow(c.Position.X - enemyPos.X, 2) + Math.Pow(c.Position.Y - enemyPos.Y, 2)))
                    .First();

                float compDx = (nearestCompanion.Position.X - enemyPos.X) / windowWidth;
                float compDy = (nearestCompanion.Position.Y - enemyPos.Y) / windowHeight;
                float compDist = (float)Math.Sqrt(compDx * compDx + compDy * compDy);

                state.Add(compDx);
                state.Add(compDy);
                state.Add(compDist);
            }
            else
            {
                state.Add(0f);
                state.Add(0f);
                state.Add(2f);
            }

            // Padding to reach 23 dimensions (20 + 3 for companion)
            while (state.Count < 23)
            {
                state.Add(0f);
            }

            return state.Take(23).ToArray();
        }

        /// <summary>
        /// Get learned movement decision for an enemy.
        /// </summary>
        public PointF GetMovementDecision(
            int enemyId,
            PointF enemyPos,
            PointF playerPos,
            PointF playerVel,
            List<Laser> playerLasers,
            List<Enemy> otherEnemies,
            List<Companion> companions,
            int windowWidth,
            int windowHeight)
        {
            if (!isEnabled || !enemyExperiences.ContainsKey(enemyId))
            {
                return GetDefaultMovement(enemyPos, playerPos);
            }

            float[] state = EncodeEnemyState(enemyPos, playerPos, playerVel, playerLasers, otherEnemies, companions, windowWidth, windowHeight);
            enemyExperiences[enemyId].LastState = state;

            int actionIndex;
            if (random.NextDouble() < epsilon)
            {
                // Exploration
                actionIndex = random.Next(9);
            }
            else
            {
                // Exploitation
                actionIndex = movementTrainer.PredictAction(state);
            }

            enemyExperiences[enemyId].LastMovementAction = actionIndex;

            return ActionToVelocity(actionIndex);
        }

        /// <summary>
        /// Get learned shooting decision for an enemy.
        /// </summary>
        public bool ShouldShoot(
            int enemyId,
            PointF enemyPos,
            PointF playerPos,
            PointF playerVel,
            List<Laser> playerLasers,
            List<Enemy> otherEnemies,
            List<Companion> companions,
            int windowWidth,
            int windowHeight,
            out PointF shootDirection)
        {
            shootDirection = PointF.Empty;

            if (!isEnabled || !enemyExperiences.ContainsKey(enemyId))
            {
                return false;
            }

            // Find best target (player or nearest companion)
            PointF targetPos = playerPos;
            PointF targetVel = playerVel;

            // If player is dead or far, prioritize companions
            float playerDist = (float)Math.Sqrt(Math.Pow(playerPos.X - enemyPos.X, 2) + Math.Pow(playerPos.Y - enemyPos.Y, 2));

            if (companions != null && companions.Count > 0)
            {
                var nearestCompanion = companions
                    .OrderBy(c => Math.Sqrt(Math.Pow(c.Position.X - enemyPos.X, 2) + Math.Pow(c.Position.Y - enemyPos.Y, 2)))
                    .FirstOrDefault();

                if (nearestCompanion != null)
                {
                    float companionDist = (float)Math.Sqrt(Math.Pow(nearestCompanion.Position.X - enemyPos.X, 2) + Math.Pow(nearestCompanion.Position.Y - enemyPos.Y, 2));

                    // Target companion if it's closer or if player is dead (at -1000, -1000)
                    if (companionDist < playerDist || playerPos.X < 0 || playerPos.Y < 0)
                    {
                        targetPos = nearestCompanion.Position;
                        targetVel = nearestCompanion.Velocity;
                    }
                }
            }

            float[] state = EncodeEnemyState(enemyPos, playerPos, playerVel, playerLasers, otherEnemies, companions, windowWidth, windowHeight);

            int actionIndex;
            if (random.NextDouble() < epsilon)
            {
                actionIndex = random.Next(3);
            }
            else
            {
                actionIndex = shootingTrainer.PredictAction(state);
            }

            enemyExperiences[enemyId].LastShootingAction = actionIndex;

            switch (actionIndex)
            {
                case 0: // Don't shoot
                    return false;

                case 1: // Shoot directly at target (player or companion)
                    float dx = targetPos.X - enemyPos.X;
                    float dy = targetPos.Y - enemyPos.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist > 0)
                    {
                        shootDirection = new PointF(dx / dist, dy / dist);
                        return true;
                    }
                    return false;

                case 2: // Predictive shooting at target
                    float predX = targetPos.X + targetVel.X * 3;
                    float predY = targetPos.Y + targetVel.Y * 3;
                    float pdx = predX - enemyPos.X;
                    float pdy = predY - enemyPos.Y;
                    float pdist = (float)Math.Sqrt(pdx * pdx + pdy * pdy);
                    if (pdist > 0)
                    {
                        shootDirection = new PointF(pdx / pdist, pdy / pdist);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Provide reward feedback for an enemy's actions.
        /// </summary>
        public void RecordReward(int enemyId, float reward)
        {
            if (enemyExperiences.ContainsKey(enemyId))
            {
                enemyExperiences[enemyId].AccumulatedReward += reward;
            }
        }

        /// <summary>
        /// Enemy was destroyed - finalize learning with penalty.
        /// </summary>
        public void EnemyDestroyed(int enemyId)
        {
            if (enemyExperiences.ContainsKey(enemyId))
            {
                var exp = enemyExperiences[enemyId];
                exp.AccumulatedReward -= 100f; // Large penalty for dying
                exp.IsActive = false;

                // Store experience for batch training
                // This would be expanded to save to files like player training
            }
        }

        /// <summary>
        /// Enemy hit the player - positive reward.
        /// </summary>
        public void EnemyHitPlayer(int enemyId)
        {
            RecordReward(enemyId, 50f);
        }

        /// <summary>
        /// Enemy hit a companion - positive reward.
        /// </summary>
        public void EnemyHitCompanion(int enemyId)
        {
            RecordReward(enemyId, 30f);
        }

        /// <summary>
        /// Enemy avoided player laser - small positive reward.
        /// </summary>
        public void EnemyAvoidedLaser(int enemyId)
        {
            RecordReward(enemyId, 5f);
        }

        private PointF ActionToVelocity(int actionIndex)
        {
            float speed = GameConstants.EnemySpeed;

            return actionIndex switch
            {
                0 => new PointF(0, -speed),        // North
                1 => new PointF(0, speed),         // South
                2 => new PointF(speed, 0),         // East
                3 => new PointF(-speed, 0),        // West
                4 => new PointF(speed, -speed),    // Northeast
                5 => new PointF(-speed, -speed),   // Northwest
                6 => new PointF(speed, speed),     // Southeast
                7 => new PointF(-speed, speed),    // Southwest
                8 => new PointF(0, 0),             // Stop
                _ => new PointF(0, 0)
            };
        }

        private PointF GetDefaultMovement(PointF enemyPos, PointF playerPos)
        {
            // Simple approach behavior as fallback
            float dx = playerPos.X - enemyPos.X;
            float dy = playerPos.Y - enemyPos.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist > 0)
            {
                return new PointF(
                    (dx / dist) * GameConstants.EnemySpeed,
                    (dy / dist) * GameConstants.EnemySpeed
                );
            }

            return PointF.Empty;
        }

        /// <summary>
        /// Save enemy learning models to models folder (only best models).
        /// </summary>
        public void SaveModels()
        {
            var modelManager = new ModelManager();

            // Calculate performance metrics from actual enemy experiences
            double movementPerformance = CalculateMovementPerformance();
            double shootingPerformance = CalculateShootingPerformance();

            // Save only if these are the best models
            var movementData = movementTrainer.GetModelData();
            var shootingData = shootingTrainer.GetModelData();

            modelManager.SaveBestModel("enemy_movement", movementData, movementPerformance);
            modelManager.SaveBestModel("enemy_shooting", shootingData, shootingPerformance);

            Console.WriteLine("💾 Enemy learning models saved (best only)");
        }

        /// <summary>
        /// Calculate movement performance based on accumulated rewards.
        /// </summary>
        private double CalculateMovementPerformance()
        {
            if (enemyExperiences.Count == 0) return 0.0;

            var recentExperiences = enemyExperiences.Values.TakeLast(20); // Last 20 enemies
            return recentExperiences.Any() ? recentExperiences.Average(e => e.AccumulatedReward) : 0.0;
        }

        /// <summary>
        /// Calculate shooting performance based on successful hits vs attempts.
        /// </summary>
        private double CalculateShootingPerformance()
        {
            if (enemyExperiences.Count == 0) return 0.0;

            // Simple heuristic: positive rewards indicate successful actions
            var recentExperiences = enemyExperiences.Values.TakeLast(20);
            var positiveRewards = recentExperiences.Where(e => e.AccumulatedReward > 0);

            return positiveRewards.Any() ? positiveRewards.Average(e => e.AccumulatedReward) : 0.0;
        }

        /// <summary>
        /// Load pre-trained enemy models.
        /// </summary>
        public void LoadModels(string movementModelPath, string shootingModelPath)
        {
            try
            {
                movementTrainer = AITrainer.LoadModel(movementModelPath);
                shootingTrainer = AITrainer.LoadModel(shootingModelPath);
                Console.WriteLine($"✅ Enemy models loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to load enemy models: {ex.Message}");
            }
        }

        /// <summary>
        /// Train the enemy AI models on collected experiences.
        /// </summary>
        public void TrainModels()
        {
            var movementExperiences = new List<DataCollector.Experience>();
            var shootingExperiences = new List<DataCollector.Experience>();

            // Convert enemy experiences to training data
            foreach (var kvp in enemyExperiences)
            {
                var exp = kvp.Value;
                if (exp.LastState.Length > 0)
                {
                    // Create movement experience
                    var movementExp = new DataCollector.Experience
                    {
                        State = exp.LastState,
                        Action = exp.LastMovementAction,
                        Reward = exp.AccumulatedReward,
                        NextState = exp.LastState, // Simplified
                        IsDone = !exp.IsActive,
                        Timestamp = DateTime.UtcNow.Ticks
                    };
                    movementExperiences.Add(movementExp);

                    // Create shooting experience
                    var shootingExp = new DataCollector.Experience
                    {
                        State = exp.LastState,
                        Action = exp.LastShootingAction,
                        Reward = exp.AccumulatedReward * 0.5f, // Scale reward differently for shooting
                        NextState = exp.LastState,
                        IsDone = !exp.IsActive,
                        Timestamp = DateTime.UtcNow.Ticks
                    };
                    shootingExperiences.Add(shootingExp);
                }
            }

            // Train both models
            if (movementExperiences.Count > 0)
            {
                Console.WriteLine($"  🎯 Training enemy movement AI on {movementExperiences.Count} experiences...");
                movementTrainer.TrainOnBatch(movementExperiences);
            }

            if (shootingExperiences.Count > 0)
            {
                Console.WriteLine($"  🎯 Training enemy shooting AI on {shootingExperiences.Count} experiences...");
                shootingTrainer.TrainOnBatch(shootingExperiences);
            }

            // Clear old experiences after training
            enemyExperiences.Clear();
            Console.WriteLine($"  ✅ Enemy AI training complete");
        }
    }
}
