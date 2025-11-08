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
        /// Get AI's recommended action based on current game state.
        /// </summary>
        public string GetRecommendedAction(
            PointF playerPos,
            PointF playerVel,
            List<Enemy> enemies,
            List<Laser> lasers,
            int lives,
            bool gameOver,
            int windowWidth,
            int windowHeight)
        {
            // Encode game state
            var enemyData = enemies.Select(e => (e.Position, (float)GameConstants.EnemyRadius)).ToList();
            var laserData = lasers.Select(l => (l.Position, l.Velocity)).ToList();

            float[] state = dataCollector.EncodeGameState(
                playerPos, playerVel,
                enemyData, laserData,
                lives, gameOver,
                windowWidth, windowHeight
            );

            // Get action from model with epsilon-greedy exploration
            int actionIndex;
            if (!isEnabled || random.NextDouble() < explorationRate)
            {
                // Random exploration
                actionIndex = random.Next(9);
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
            int windowHeight)
        {
            string action = GetRecommendedAction(
                playerPos, playerVel, enemies, lasers,
                lives, gameOver, windowWidth, windowHeight
            );

            return ActionToVelocity(action);
        }

        /// <summary>
        /// Convert action string to velocity vector.
        /// </summary>
        private PointF ActionToVelocity(string action)
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
                _ => new PointF(0, 0)
            };
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
    }
}
