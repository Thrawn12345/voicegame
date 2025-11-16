using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// AI Player that uses trained model to make movement decisions.
    /// Can run in autonomous mode or provide suggestions to human player.
    /// </summary>
    public class AIPlayer
    {
        private AITrainer trainer;
        private DataCollector dataCollector;
        private Random random;
        private float explorationRate;
        private bool isEnabled;

        public bool IsEnabled => isEnabled;

        public AIPlayer(string? modelPath = null, float explorationRate = 0.05f)
        {
            this.explorationRate = explorationRate;
            this.random = new Random();
            this.dataCollector = new DataCollector();

            // Load trained model if available
            if (!string.IsNullOrEmpty(modelPath))
            {
                try
                {
                    trainer = AITrainer.LoadModel(modelPath);
                    isEnabled = true;
                    Console.WriteLine($"✅ AI Model loaded from {modelPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to load AI model: {ex.Message}");
                    Console.WriteLine("   AI will use random exploration.");
                    trainer = new AITrainer();
                    isEnabled = false;
                }
            }
            else
            {
                trainer = new AITrainer();
                isEnabled = false;
            }
        }

        /// <summary>
        /// Get AI's recommended action based on game state.
        /// </summary>
        public string GetRecommendedAction(
            PointF playerPos,
            PointF playerVel,
            List<Enemy> enemies,
            List<Laser> lasers,
            int lives,
            bool gameOver,
            int windowWidth,
            int windowHeight,
            PointF? targetPos = null,
            List<EnemyBullet>? enemyBullets = null,
            List<Obstacle>? obstacles = null,
            List<Companion>? companions = null,
            FormationType? formationType = null,
            float formationThreatLevel = 0f,
            bool companionFireSupport = false)
        {
            // Encode game state
            var enemyData = enemies.Select(e => (e.Position, (float)GameConstants.EnemyRadius)).ToList();
            var laserData = lasers.Select(l => (l.Position, l.Velocity)).ToList();

            float[] state = dataCollector.EncodeGameState(
                playerPos, playerVel,
                enemyData, laserData,
                lives, gameOver,
                windowWidth, windowHeight,
                targetPos,
                companions,
                formationType,
                formationThreatLevel,
                companionFireSupport
            );

            // Get action from model with epsilon-greedy exploration
            int actionIndex;
            if (!isEnabled || random.NextDouble() < explorationRate)
            {
                // Random exploration - include new target-based actions
                actionIndex = random.Next(12);
            }
            else
            {
                // Use trained model
                actionIndex = trainer.PredictAction(state);
            }

            return DataCollector.ActionToString(actionIndex);
        }

        /// <summary>
        /// Get AI's recommended velocity vector based on game state.
        /// </summary>
        public PointF GetRecommendedVelocity(
            PointF playerPos,
            PointF playerVel,
            List<Enemy> enemies,
            List<Laser> lasers,
            int lives,
            bool gameOver,
            int windowWidth,
            int windowHeight,
            PointF? targetPos = null,
            List<EnemyBullet>? enemyBullets = null,
            List<Obstacle>? obstacles = null,
            List<Companion>? companions = null,
            FormationType? formationType = null,
            float formationThreatLevel = 0f,
            bool companionFireSupport = false)
        {
            string action = GetRecommendedAction(
                playerPos, playerVel, enemies, lasers,
                lives, gameOver, windowWidth, windowHeight, targetPos, enemyBullets, obstacles,
                companions, formationType, formationThreatLevel, companionFireSupport
            );
            
            return ActionToVelocity(action, playerPos, targetPos, enemies, 
                enemyBullets ?? new List<EnemyBullet>(), 
                obstacles ?? new List<Obstacle>(), windowWidth, windowHeight);
        }

        /// <summary>
        /// Convert action string to velocity vector.
        /// </summary>
        private PointF ActionToVelocity(string action, PointF? currentPos = null, PointF? targetPos = null, 
            List<Enemy>? enemies = null, List<EnemyBullet>? bullets = null, List<Obstacle>? obstacles = null,
            int windowWidth = 800, int windowHeight = 600)
        {
            int speed = GameConstants.PlayerSpeed;

            return action.ToUpper() switch
            {
                "NORTH" => new PointF(0, -speed),
                "SOUTH" => new PointF(0, speed),
                "EAST" => new PointF(speed, 0),
                "WEST" => new PointF(-speed, 0),
                "NORTHEAST" => new PointF(speed, -speed),
                "NORTHWEST" => new PointF(-speed, -speed),
                "SOUTHEAST" => new PointF(speed, speed),
                "SOUTHWEST" => new PointF(-speed, speed),
                "STOP" => new PointF(0, 0),
                "TARGET_DIRECT" => GetDirectTargetVelocity(currentPos, targetPos, speed),
                "TARGET_SAFE" => GetSafeTargetVelocity(currentPos, targetPos, enemies, bullets, obstacles, windowWidth, windowHeight, speed),
                "TARGET_DODGE" => GetDodgeTargetVelocity(currentPos, targetPos, enemies, bullets, obstacles, windowWidth, windowHeight, speed),
                _ => new PointF(0, 0)
            };
        }

        /// <summary>
        /// Get direct velocity toward target.
        /// </summary>
        private PointF GetDirectTargetVelocity(PointF? currentPos, PointF? targetPos, int speed)
        {
            if (!currentPos.HasValue || !targetPos.HasValue) return PointF.Empty;

            float dx = targetPos.Value.X - currentPos.Value.X;
            float dy = targetPos.Value.Y - currentPos.Value.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance < 10f) return PointF.Empty;

            return new PointF((dx / distance) * speed, (dy / distance) * speed);
        }

        /// <summary>
        /// Get safe velocity toward target avoiding threats.
        /// </summary>
        private PointF GetSafeTargetVelocity(PointF? currentPos, PointF? targetPos, List<Enemy>? enemies, 
            List<EnemyBullet>? bullets, List<Obstacle>? obstacles, int windowWidth, int windowHeight, int speed)
        {
            if (!currentPos.HasValue || !targetPos.HasValue) return PointF.Empty;

            // Use pathfinding system if we have a reference to it
            var pathfinding = new PathfindingSystem();
            return pathfinding.GetOptimalVelocity(
                currentPos.Value, targetPos.Value,
                enemies ?? new List<Enemy>(),
                bullets ?? new List<EnemyBullet>(),
                obstacles ?? new List<Obstacle>(),
                windowWidth, windowHeight
            );
        }

        /// <summary>
        /// Get evasive velocity toward target with extra dodging.
        /// </summary>
        private PointF GetDodgeTargetVelocity(PointF? currentPos, PointF? targetPos, List<Enemy>? enemies,
            List<EnemyBullet>? bullets, List<Obstacle>? obstacles, int windowWidth, int windowHeight, int speed)
        {
            var safeVelocity = GetSafeTargetVelocity(currentPos, targetPos, enemies, bullets, obstacles, windowWidth, windowHeight, speed);
            
            // Add extra random evasion
            var random = new Random();
            float evasionFactor = 0.3f;
            float evasionX = (float)(random.NextDouble() - 0.5) * speed * evasionFactor;
            float evasionY = (float)(random.NextDouble() - 0.5) * speed * evasionFactor;

            return new PointF(safeVelocity.X + evasionX, safeVelocity.Y + evasionY);
        }

        /// <summary>
        /// Enable or disable AI control.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
        }

        /// <summary>
        /// Set exploration rate (0.0 = always use model, 1.0 = always random).
        /// </summary>
        public void SetExplorationRate(float rate)
        {
            explorationRate = Math.Clamp(rate, 0f, 1f);
        }

        /// <summary>
        /// Get action statistics for debugging/visualization.
        /// </summary>
        public Dictionary<string, float> GetActionConfidence(float[] state)
        {
            if (!isEnabled)
                return new Dictionary<string, float>();

            var qValues = trainer.GetQValues(state);
            var actionConfidence = new Dictionary<string, float>();

            for (int i = 0; i < Math.Min(9, qValues.Length); i++)
            {
                actionConfidence[DataCollector.ActionToString(i)] = qValues[i];
            }

            return actionConfidence;
        }

        /// <summary>
        /// Export the AI model to a file.
        /// </summary>
        public string ExportModel(string modelPath = "player_ai_model.json")
        {
            return trainer.ExportModel(modelPath);
        }

        /// <summary>
        /// Get the underlying trainer's model data.
        /// </summary>
        public object GetModelData()
        {
            return trainer.GetModelData();
        }

        /// <summary>
        /// Get the trainer's average reward for performance tracking.
        /// </summary>
        public double GetAverageReward()
        {
            return trainer.GetAverageReward();
        }
    }
}
