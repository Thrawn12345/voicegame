using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace VoiceGame
{
    // AI Shooting System
    public enum ShootingAction
    {
        DontShoot,
        ShootNorth,
        ShootSouth,
        ShootEast,
        ShootWest,
        ShootNorthEast,
        ShootNorthWest,
        ShootSouthEast,
        ShootSouthWest,
        ShootAtNearestEnemy,
        ShootDefensively
    }

    public class AIShootingAgent
    {
        private readonly Random random = new();
        private float epsilon = 0.8f; // Start with high exploration

        // Convert game state to neural network input
        public float[] GetStateVector(Player player, List<Enemy> enemies, List<EnemyBullet> bullets, Size screenSize)
        {
            var state = new List<float>();

            // Player info (normalized to 0-1)
            state.Add(player.Position.X / screenSize.Width);
            state.Add(player.Position.Y / screenSize.Height);
            state.Add(player.Velocity.X / 10f);
            state.Add(player.Velocity.Y / 10f);

            // Nearest 3 enemies (distance, angle, relative position)
            var nearestEnemies = enemies
                .OrderBy(e => Distance(player.Position, e.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < nearestEnemies.Count)
                {
                    var enemy = nearestEnemies[i];
                    var distance = Distance(player.Position, enemy.Position) / 400f;
                    var angle = Math.Atan2(enemy.Position.Y - player.Position.Y,
                                         enemy.Position.X - player.Position.X) / Math.PI;

                    state.Add(distance);
                    state.Add((float)angle);
                    state.Add((enemy.Position.X - player.Position.X) / 400f);
                    state.Add((enemy.Position.Y - player.Position.Y) / 400f);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 0f, 0f, 0f });
                }
            }

            // Incoming bullet threat assessment
            var dangerousBullets = bullets
                .Where(b => Distance(player.Position, b.Position) < 100f)
                .OrderBy(b => Distance(player.Position, b.Position))
                .Take(2)
                .ToList();

            for (int i = 0; i < 2; i++)
            {
                if (i < dangerousBullets.Count)
                {
                    var bullet = dangerousBullets[i];
                    state.Add(Distance(player.Position, bullet.Position) / 100f);
                    state.Add(bullet.Velocity.X / 10f);
                    state.Add(bullet.Velocity.Y / 10f);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 0f, 0f });
                }
            }

            return state.ToArray();
        }

        // During training: choose action using epsilon-greedy
        public ShootingAction ChooseAction(float[] state, float[]? qValues = null)
        {
            if (qValues == null || random.NextSingle() < epsilon)
            {
                // Random exploration
                return (ShootingAction)random.Next(Enum.GetValues<ShootingAction>().Length);
            }

            // Exploit: choose best action based on Q-values
            int bestAction = 0;
            for (int i = 1; i < qValues.Length; i++)
            {
                if (qValues[i] > qValues[bestAction])
                    bestAction = i;
            }

            return (ShootingAction)bestAction;
        }

        // Execute the chosen shooting action
        public PointF? ExecuteAction(ShootingAction action, Player player, List<Enemy> enemies)
        {
            const int laserSpeed = GameConstants.LaserSpeed;

            return action switch
            {
                ShootingAction.DontShoot => null,
                ShootingAction.ShootNorth => new PointF(0, -laserSpeed),
                ShootingAction.ShootSouth => new PointF(0, laserSpeed),
                ShootingAction.ShootEast => new PointF(laserSpeed, 0),
                ShootingAction.ShootWest => new PointF(-laserSpeed, 0),
                ShootingAction.ShootNorthEast => new PointF(laserSpeed, -laserSpeed),
                ShootingAction.ShootNorthWest => new PointF(-laserSpeed, -laserSpeed),
                ShootingAction.ShootSouthEast => new PointF(laserSpeed, laserSpeed),
                ShootingAction.ShootSouthWest => new PointF(-laserSpeed, laserSpeed),
                ShootingAction.ShootAtNearestEnemy => GetVelocityToNearestEnemy(player, enemies),
                ShootingAction.ShootDefensively => GetDefensiveVelocity(player, enemies),
                _ => null
            };
        }

        private PointF? GetVelocityToNearestEnemy(Player player, List<Enemy> enemies)
        {
            if (!enemies.Any()) return null;

            var nearest = enemies.OrderBy(e => Distance(player.Position, e.Position)).First();
            var dx = nearest.Position.X - player.Position.X;
            var dy = nearest.Position.Y - player.Position.Y;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance == 0) return null;

            const int laserSpeed = GameConstants.LaserSpeed;
            return new PointF((dx / distance) * laserSpeed, (dy / distance) * laserSpeed);
        }

        private PointF? GetDefensiveVelocity(Player player, List<Enemy> enemies)
        {
            // Simple defensive strategy: shoot at closest threatening enemy
            return GetVelocityToNearestEnemy(player, enemies);
        }

        private static float Distance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void UpdateEpsilon(int episode)
        {
            // Decay exploration rate over time
            epsilon = Math.Max(0.1f, 0.8f * (float)Math.Pow(0.995, episode));
        }
    }

    public class TrainingDataCollector
    {
        private readonly List<(float[] state, int action, float reward, float[] nextState, bool done)> experiences = new();
        private GameStateSnapshot? lastSnapshot;
        private float[]? lastState;
        private ShootingAction lastAction;
        private int episodeCount = 0;

        public void StartEpisode()
        {
            episodeCount++;
            lastSnapshot = null;
            lastState = null;
        }

        public void RecordStep(float[] state, ShootingAction action, GameStateSnapshot snapshot)
        {
            if (lastSnapshot != null && lastState != null)
            {
                var reward = CalculateReward(lastSnapshot, snapshot, lastAction);
                var done = snapshot.PlayerHealth <= 0;

                experiences.Add((lastState, (int)lastAction, reward, state, done));

                Console.WriteLine($"AI Action: {lastAction}, Reward: {reward:F2}");
            }

            lastState = state;
            lastAction = action;
            lastSnapshot = snapshot;

            // Save periodically
            if (experiences.Count % GameConstants.ExperienceSaveInterval == 0 && experiences.Count > 0)
            {
                SaveExperiencesToFile();
            }
        }

        private float CalculateReward(GameStateSnapshot before, GameStateSnapshot after, ShootingAction action)
        {
            float reward = 0;

            // Reward for hitting enemies
            if (after.EnemiesDestroyed > before.EnemiesDestroyed)
                reward += 100 * (after.EnemiesDestroyed - before.EnemiesDestroyed);

            // Reward for destroying enemy bullets
            if (after.BulletsDestroyed > before.BulletsDestroyed)
                reward += 25 * (after.BulletsDestroyed - before.BulletsDestroyed);

            // Penalty for taking damage
            if (after.PlayerHealth < before.PlayerHealth)
                reward -= 50;

            // Small survival bonus
            reward += 1;

            // Efficiency: reward appropriate action selection
            if (action != ShootingAction.DontShoot)
            {
                bool hitSomething = after.EnemiesDestroyed > before.EnemiesDestroyed ||
                                   after.BulletsDestroyed > before.BulletsDestroyed;
                reward += hitSomething ? 15 : -2; // Bonus for hit, small penalty for miss
            }

            return reward;
        }

        private void SaveExperiencesToFile()
        {
            try
            {
                var data = new
                {
                    episode = episodeCount,
                    experiences_count = experiences.Count,
                    experiences = experiences.TakeLast(GameConstants.ExperienceSaveInterval).Select(exp => new
                    {
                        state = exp.state,
                        action = exp.action,
                        reward = exp.reward,
                        next_state = exp.nextState,
                        done = exp.done
                    })
                };

                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var filename = $"training_data_ep{episodeCount}_{DateTime.Now:HHmmss}.json";
                File.WriteAllText(filename, json);
                Console.WriteLine($"💾 Saved {experiences.Count} training examples to {filename}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving training data: {ex.Message}");
            }
        }

        public void EndEpisode(GameStateSnapshot finalSnapshot)
        {
            if (lastSnapshot != null && lastState != null)
            {
                var reward = CalculateReward(lastSnapshot, finalSnapshot, lastAction);
                reward -= 100; // Large penalty for game over

                experiences.Add((lastState, (int)lastAction, reward, new float[lastState.Length], true));
                Console.WriteLine($"🎯 Episode {episodeCount} ended. Final reward: {reward:F2}");
            }

            SaveExperiencesToFile();
        }
    }
}