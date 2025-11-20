using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Specialized learning agent for boss AI.
    /// Learns more sophisticated combat patterns and strategies.
    /// </summary>
    public class BossLearningAgent
    {
        private AITrainer movementTrainer;
        private AITrainer attackTrainer;
        private DataCollector dataCollector;
        private Random random;
        private float epsilon;

        // Boss experience tracking
        private Dictionary<int, BossExperience> bossExperiences;
        private int nextBossId = 0;

        private class BossExperience
        {
            public float[] LastState { get; set; } = Array.Empty<float>();
            public int LastMovementAction { get; set; }
            public int LastAttackAction { get; set; }
            public float AccumulatedReward { get; set; }
            public bool IsActive { get; set; } = true;
            public PointF LastPosition { get; set; }
            public DateTime LastMoveTime { get; set; }
            public int StationaryFrames { get; set; }
        }

        public BossLearningAgent(float epsilon = 0.25f)
        {
            this.epsilon = epsilon;
            this.random = new Random();
            this.dataCollector = new DataCollector();
            this.bossExperiences = new Dictionary<int, BossExperience>();

            // Boss trainers with larger capacity
            var movementConfig = new AITrainer.ModelConfig
            {
                StateSpaceSize = 30,  // More complex state for boss
                ActionSpaceSize = 12, // 8 directions + stop + 3 special moves
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            var attackConfig = new AITrainer.ModelConfig
            {
                StateSpaceSize = 30,
                ActionSpaceSize = 5,  // Normal attack, burst fire, predictive, defensive, special
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            movementTrainer = new AITrainer(movementConfig);
            attackTrainer = new AITrainer(attackConfig);

            Console.WriteLine("👹 Boss Learning Agent initialized");
        }

        public int RegisterBoss()
        {
            int bossId = nextBossId++;
            bossExperiences[bossId] = new BossExperience 
            { 
                LastMoveTime = DateTime.UtcNow,
                LastPosition = PointF.Empty
            };
            return bossId;
        }

        private float[] EncodeBossState(
            PointF bossPos,
            PointF playerPos,
            PointF playerVel,
            List<Laser> playerLasers,
            List<Companion> companions,
            int windowWidth,
            int windowHeight,
            int bossHealth,
            int maxHealth)
        {
            List<float> state = new List<float>();

            // Boss normalized position with edge awareness
            float normalizedX = bossPos.X / windowWidth;
            float normalizedY = bossPos.Y / windowHeight;
            state.Add(normalizedX);
            state.Add(normalizedY);

            // Edge proximity (penalty zone)
            float edgeMargin = 100f;
            float leftDist = bossPos.X / edgeMargin;
            float rightDist = (windowWidth - bossPos.X) / edgeMargin;
            float topDist = bossPos.Y / edgeMargin;
            float bottomDist = (windowHeight - bossPos.Y) / edgeMargin;
            state.Add(Math.Min(1f, leftDist));
            state.Add(Math.Min(1f, rightDist));
            state.Add(Math.Min(1f, topDist));
            state.Add(Math.Min(1f, bottomDist));

            // Player relative position
            float dx = (playerPos.X - bossPos.X) / windowWidth;
            float dy = (playerPos.Y - bossPos.Y) / windowHeight;
            float playerDistance = (float)Math.Sqrt(dx * dx + dy * dy);
            state.Add(dx);
            state.Add(dy);
            state.Add(playerDistance);

            // Player velocity
            state.Add(playerVel.X / GameConstants.PlayerSpeed);
            state.Add(playerVel.Y / GameConstants.PlayerSpeed);

            // Boss health ratio
            state.Add((float)bossHealth / maxHealth);

            // Threat assessment from player lasers
            int nearbyLasers = 0;
            float closestLaser = float.MaxValue;
            foreach (var laser in playerLasers)
            {
                float laserDist = (float)Math.Sqrt(
                    Math.Pow(laser.Position.X - bossPos.X, 2) +
                    Math.Pow(laser.Position.Y - bossPos.Y, 2));
                if (laserDist < 150f) nearbyLasers++;
                if (laserDist < closestLaser) closestLaser = laserDist;
            }
            state.Add(nearbyLasers / 10f);
            state.Add(Math.Min(1f, closestLaser / 200f));

            // Companion threat analysis
            int activeCompanions = companions.Count;
            float avgCompanionDist = 0f;
            if (activeCompanions > 0)
            {
                avgCompanionDist = companions.Average(c =>
                    (float)Math.Sqrt(
                        Math.Pow(c.Position.X - bossPos.X, 2) +
                        Math.Pow(c.Position.Y - bossPos.Y, 2))) / windowWidth;
            }
            state.Add(activeCompanions / 3f);
            state.Add(avgCompanionDist);

            // Pad to state size
            while (state.Count < 30)
                state.Add(0f);

            return state.Take(30).ToArray();
        }

        public PointF GetMovementDecision(
            int bossId,
            PointF bossPos,
            PointF playerPos,
            PointF playerVel,
            List<Laser> playerLasers,
            List<Companion> companions,
            int windowWidth,
            int windowHeight,
            int bossHealth,
            int maxHealth)
        {
            if (!bossExperiences.ContainsKey(bossId))
                return PointF.Empty;

            var state = EncodeBossState(bossPos, playerPos, playerVel, playerLasers, companions,
                windowWidth, windowHeight, bossHealth, maxHealth);

            var exp = bossExperiences[bossId];
            exp.LastState = state;

            // Choose action (epsilon-greedy)
            int action;
            if (random.NextDouble() < epsilon)
                action = random.Next(12);
            else
                action = movementTrainer.PredictAction(state);

            exp.LastMovementAction = action;

            // Apply wall avoidance penalty
            float edgeMargin = 100f;
            bool nearWall = bossPos.X < edgeMargin || bossPos.X > windowWidth - edgeMargin ||
                           bossPos.Y < edgeMargin || bossPos.Y > windowHeight - edgeMargin;
            
            if (nearWall)
            {
                RecordReward(bossId, -5f); // Penalty for being near walls
            }

            // Check for stationary behavior
            float moveDist = (float)Math.Sqrt(
                Math.Pow(bossPos.X - exp.LastPosition.X, 2) +
                Math.Pow(bossPos.Y - exp.LastPosition.Y, 2));
            
            if (moveDist < 5f)
            {
                exp.StationaryFrames++;
                if (exp.StationaryFrames > 30) // More than ~0.5 seconds
                {
                    RecordReward(bossId, -3f); // Penalty for staying in one place
                }
            }
            else
            {
                exp.StationaryFrames = 0;
                RecordReward(bossId, 1f); // Small reward for movement
            }

            exp.LastPosition = bossPos;
            exp.LastMoveTime = DateTime.UtcNow;

            // Convert action to velocity
            return ActionToVelocity(action);
        }

        private PointF ActionToVelocity(int action)
        {
            float speed = GameConstants.BossSpeed;
            return action switch
            {
                0 => new PointF(0, -speed),           // North
                1 => new PointF(speed, -speed),       // Northeast
                2 => new PointF(speed, 0),            // East
                3 => new PointF(speed, speed),        // Southeast
                4 => new PointF(0, speed),            // South
                5 => new PointF(-speed, speed),       // Southwest
                6 => new PointF(-speed, 0),           // West
                7 => new PointF(-speed, -speed),      // Northwest
                8 => PointF.Empty,                    // Stop
                9 => new PointF(0, -speed * 1.5f),    // Fast north
                10 => new PointF(speed * 1.5f, 0),    // Fast east
                11 => new PointF(-speed * 1.5f, 0),   // Fast west
                _ => PointF.Empty
            };
        }

        public bool ShouldAttack(
            int bossId,
            PointF bossPos,
            PointF playerPos,
            PointF playerVel,
            List<Laser> playerLasers,
            List<Companion> companions,
            int windowWidth,
            int windowHeight,
            int bossHealth,
            int maxHealth,
            out int attackType)
        {
            attackType = 0;

            if (!bossExperiences.ContainsKey(bossId))
                return false;

            var state = EncodeBossState(bossPos, playerPos, playerVel, playerLasers, companions,
                windowWidth, windowHeight, bossHealth, maxHealth);

            int action;
            if (random.NextDouble() < epsilon)
                action = random.Next(5);
            else
                action = attackTrainer.PredictAction(state);

            bossExperiences[bossId].LastAttackAction = action;
            attackType = action;

            return action > 0; // 0 = don't attack, 1-4 = different attack types
        }

        public void RecordReward(int bossId, float reward)
        {
            if (bossExperiences.ContainsKey(bossId))
            {
                bossExperiences[bossId].AccumulatedReward += reward;
            }
        }

        public void BossDefeated(int bossId)
        {
            if (!bossExperiences.ContainsKey(bossId))
                return;

            var exp = bossExperiences[bossId];
            
            // Large penalty for being defeated
            RecordReward(bossId, -50f);

            // Create terminal experience
            var experience = new DataCollector.Experience
            {
                State = exp.LastState,
                Action = exp.LastMovementAction,
                Reward = exp.AccumulatedReward,
                NextState = new float[30],
                IsDone = true
            };

            movementTrainer.TrainOnBatch(new List<DataCollector.Experience> { experience });

            bossExperiences.Remove(bossId);
        }

        public void BossHitPlayer(int bossId)
        {
            RecordReward(bossId, 15f); // Reward for hitting player
        }

        public void BossHitByLaser(int bossId)
        {
            RecordReward(bossId, -5f); // Penalty for getting hit
        }

        public void SaveModels()
        {
            try
            {
                // Export models directly to files
                movementTrainer.ExportModel("models/boss_movement_model.json");
                attackTrainer.ExportModel("models/boss_attack_model.json");
                Console.WriteLine("👹 Boss AI models saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to save boss models: {ex.Message}");
            }
        }

        public void LoadModels()
        {
            // Model loading not implemented yet - boss will start from scratch
            Console.WriteLine("👹 Boss AI starting with fresh models");
        }
    }
}
