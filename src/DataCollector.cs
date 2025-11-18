using System;
using System.Collections.Generic;
using System.Drawing;

namespace VoiceGame
{
    /// <summary>
    /// Captures game states, actions, and rewards during gameplay for AI training.
    /// </summary>
    public class DataCollector
    {
        private List<Experience> currentEpisode;
        private int episodeCount = 0;
        private float currentEpisodeReward = 0f;

        public struct Experience
        {
            public float[] State { get; set; }
            public int Action { get; set; }
            public float Reward { get; set; }
            public float[] NextState { get; set; }
            public bool IsDone { get; set; }
            public float Confidence { get; set; }
            public long Timestamp { get; set; }
        }

        public DataCollector()
        {
            currentEpisode = new List<Experience>();
        }

        /// <summary>
        /// Encodes game state into a normalized float array for neural network input.
        /// </summary>
        public float[] EncodeGameState(PointF playerPos, PointF playerVel, 
            List<(PointF pos, float radius)> enemies,
            List<(PointF pos, PointF vel)> lasers,
            int lives, bool gameOver, int windowWidth, int windowHeight,
            PointF? targetPos = null,
            List<Companion>? companions = null,
            FormationType? formationType = null,
            float formationThreatLevel = 0f,
            bool companionFireSupport = false)
        {
            List<float> state = new List<float>();

            // Player normalized position (2 values)
            state.Add(playerPos.X / windowWidth);
            state.Add(playerPos.Y / windowHeight);

            // Player normalized velocity (2 values)
            state.Add(playerVel.X / 10f);  // Normalize to typical speed range
            state.Add(playerVel.Y / 10f);

            // Lives (1 value, normalized 0-1)
            state.Add(lives / 3f);

            // Enemy proximity features (max 5 enemies tracked)
            for (int i = 0; i < 5; i++)
            {
                if (i < enemies.Count)
                {
                    var enemy = enemies[i];
                    float distX = (enemy.pos.X - playerPos.X) / windowWidth;
                    float distY = (enemy.pos.Y - playerPos.Y) / windowHeight;
                    float distance = (float)Math.Sqrt(distX * distX + distY * distY);

                    state.Add(distX);
                    state.Add(distY);
                    state.Add(distance);
                    state.Add(enemy.radius / 20f);  // Enemy size
                }
                else
                {
                    // Padding for missing enemies
                    state.Add(0f);
                    state.Add(0f);
                    state.Add(1f);  // Far away
                    state.Add(0f);
                }
            }

            // Laser count and closest laser info (4 values)
            state.Add(Math.Min(lasers.Count, 10) / 10f);
            if (lasers.Count > 0)
            {
                float minDist = float.MaxValue;
                PointF closestLaserVel = new PointF(0, 0);
                foreach (var laser in lasers)
                {
                    float dx = laser.pos.X - playerPos.X;
                    float dy = laser.pos.Y - playerPos.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestLaserVel = laser.vel;
                    }
                }
                state.Add(Math.Min(minDist / 200f, 1f));  // Closest laser distance
                state.Add(closestLaserVel.X / 10f);  // Closest laser velocity X
                state.Add(closestLaserVel.Y / 10f);  // Closest laser velocity Y
            }
            else
            {
                state.Add(1f);  // No lasers
                state.Add(0f);  // No laser velocity X
                state.Add(0f);  // No laser velocity Y
            }

            state.Add(gameOver ? 1f : 0f);

            // Target-based movement features (4 values)
            if (targetPos.HasValue)
            {
                float targetDx = (targetPos.Value.X - playerPos.X) / windowWidth;
                float targetDy = (targetPos.Value.Y - playerPos.Y) / windowHeight;
                float targetDistance = (float)Math.Sqrt(targetDx * targetDx + targetDy * targetDy);
                
                state.Add(targetDx);  // Target X offset (normalized)
                state.Add(targetDy);  // Target Y offset (normalized)
                state.Add(Math.Min(targetDistance, 1f));  // Distance to target
                state.Add(1f);  // Has target flag
            }
            else
            {
                state.Add(0f);  // No target X
                state.Add(0f);  // No target Y
                state.Add(0f);  // No distance
                state.Add(0f);  // No target flag
            }

            // Enhanced companion features (8 values) - formation, coordination, and survival state
            state.Add((companions?.Count ?? 0) / 3f);  // Companion count (normalized)
            
            if (companions?.Count > 0)
            {
                // Average companion distance from player
                float avgCompanionDist = 0f;
                int livingCompanions = 0;
                int activelyShooting = 0;
                
                foreach (var companion in companions)
                {
                    float dx = companion.Position.X - playerPos.X;
                    float dy = companion.Position.Y - playerPos.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    avgCompanionDist += Math.Min(dist / 100f, 1f);  // Normalized distance
                    
                    // Check if companion is actively shooting (within last 2 seconds)
                    var timeSinceShot = DateTime.UtcNow - companion.LastShotTime;
                    if (timeSinceShot.TotalSeconds < 2.0) activelyShooting++;
                    
                    livingCompanions++;
                }
                
                if (livingCompanions > 0)
                {
                    avgCompanionDist /= livingCompanions;
                }
                
                state.Add(avgCompanionDist);  // Average companion distance
                state.Add(livingCompanions / 3f);  // Living companions ratio
                state.Add(formationType.HasValue ? (int)formationType.Value / 4f : 0f);  // Formation type
                state.Add(formationThreatLevel);  // Formation threat assessment
                state.Add(companionFireSupport ? 1f : 0f);  // Companion fire support active
                state.Add(activelyShooting / 3f);  // Actively shooting companions ratio
                state.Add(Math.Min(companions.Count / 3f, 1f));  // Companion survival effectiveness
            }
            else
            {
                // No companions
                state.Add(0f);  // No average distance
                state.Add(0f);  // No living companions
                state.Add(0f);  // No formation
                state.Add(0f);  // No threat assessment
                state.Add(0f);  // No fire support
                state.Add(0f);  // No active shooting
                state.Add(0f);  // No survival effectiveness
            }

            return state.ToArray();
        }

        /// <summary>
        /// Records an experience during gameplay.
        /// </summary>
        public void RecordExperience(float[] state, int action, float reward, 
            float[] nextState, bool isDone, float confidence = 1f)
        {
            var experience = new Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = isDone,
                Confidence = confidence,
                Timestamp = DateTime.UtcNow.Ticks
            };

            currentEpisode.Add(experience);
            currentEpisodeReward += reward;
        }

        /// <summary>
        /// Ends current episode and returns it. Starts a new episode.
        /// </summary>
        public (List<Experience> episode, int episodeNumber, float totalReward) EndEpisode()
        {
            episodeCount++;
            var completedEpisode = currentEpisode;
            float episodeReward = currentEpisodeReward;

            currentEpisode = new List<Experience>();
            currentEpisodeReward = 0f;

            return (completedEpisode, episodeCount, episodeReward);
        }

        /// <summary>
        /// Gets current episode stats without ending it.
        /// </summary>
        public (int count, float reward) GetCurrentEpisodeStats()
        {
            return (currentEpisode.Count, currentEpisodeReward);
        }

        /// <summary>
        /// Converts action index to action description for logging.
        /// </summary>
        public static string ActionToString(int action)
        {
            return action switch
            {
                0 => "NORTH",
                1 => "SOUTH", 
                2 => "EAST",
                3 => "WEST",
                4 => "NORTHEAST",
                5 => "NORTHWEST",
                6 => "SOUTHEAST",
                7 => "SOUTHWEST",
                8 => "STOP",
                9 => "TARGET_DIRECT",     // Move directly toward target
                10 => "TARGET_SAFE",     // Move toward target avoiding threats
                11 => "TARGET_DODGE",    // Move toward target with evasive maneuvers
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// Converts action description to index.
        /// </summary>
        public static int StringToAction(string action)
        {
            return action.ToUpper() switch
            {
                "NORTH" => 0,
                "SOUTH" => 1,
                "EAST" => 2,
                "WEST" => 3,
                "NORTHEAST" => 4,
                "NORTHWEST" => 5,
                "SOUTHEAST" => 6,
                "SOUTHWEST" => 7,
                "STOP" => 8,
                "TARGET_DIRECT" => 9,
                "TARGET_SAFE" => 10,
                "TARGET_DODGE" => 11,
                _ => 8  // Default to stop
            };
        }

        /// <summary>
        /// Get shooting training data for AI training
        /// </summary>
        public List<ShootingTrainingData> GetShootingTrainingData()
        {
            var shootingData = new List<ShootingTrainingData>();
            
            try
            {
                // Convert episodes to shooting training data
                foreach (var experience in currentEpisode)
                {
                    if (experience.State != null && experience.State.Length >= 20)
                    {
                        var data = new ShootingTrainingData
                        {
                            PlayerX = experience.State[0] * 800,
                            PlayerY = experience.State[1] * 600,
                            EnemyX = experience.State.Length > 4 ? experience.State[4] * 800 : 0,
                            EnemyY = experience.State.Length > 5 ? experience.State[5] * 600 : 0,
                            EnemyDistance = experience.State.Length > 6 ? experience.State[6] * 1000 : 1000,
                            EnemyVelocityX = experience.State.Length > 7 ? experience.State[7] * 10 : 0,
                            EnemyVelocityY = experience.State.Length > 8 ? experience.State[8] * 10 : 0,
                            PlayerHealth = experience.State.Length > 15 ? experience.State[15] * 100 : 100,
                            AmmoCount = experience.State.Length > 16 ? experience.State[16] * 30 : 30,
                            DidShoot = experience.Action >= 9, // Shooting actions are 9+
                            ShotDirection = new PointF(
                                experience.State.Length > 17 ? experience.State[17] * 800 : 0,
                                experience.State.Length > 18 ? experience.State[18] * 600 : 0
                            ),
                            WasHit = experience.Reward > 0,
                            Timestamp = DateTime.FromBinary(experience.Timestamp)
                        };
                        
                        shootingData.Add(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting shooting training data: {ex.Message}");
            }
            
            return shootingData;
        }
    }

    /// <summary>
    /// Training data for shooting AI
    /// </summary>
    public class ShootingTrainingData
    {
        public double PlayerX { get; set; }
        public double PlayerY { get; set; }
        public double EnemyX { get; set; }
        public double EnemyY { get; set; }
        public double EnemyDistance { get; set; }
        public double EnemyVelocityX { get; set; }
        public double EnemyVelocityY { get; set; }
        public double PlayerHealth { get; set; }
        public double AmmoCount { get; set; }
        public bool DidShoot { get; set; }
        public PointF ShotDirection { get; set; }
        public bool WasHit { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
