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
            int lives, bool gameOver, int windowWidth, int windowHeight)
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
                foreach (var laser in lasers)
                {
                    float dx = laser.pos.X - playerPos.X;
                    float dy = laser.pos.Y - playerPos.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    minDist = Math.Min(minDist, dist);
                }
                state.Add(Math.Min(minDist / 200f, 1f));  // Closest laser distance
            }
            else
            {
                state.Add(1f);  // No lasers
            }

            state.Add(gameOver ? 1f : 0f);

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
                _ => 8  // Default to stop
            };
        }
    }
}
