using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Separated Q-learning agents for each entity type and behavior.
    /// 8 total agents: Player Movement, Player Shooting, Companion Movement, Companion Shooting,
    /// Enemy Movement, Enemy Shooting, Boss Movement, Boss Shooting
    /// </summary>
    /// 
    public class PlayerMovementAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public PlayerMovementAgent(float epsilon = 0.2f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 21,  // 2 pos + 3*5 bullets + 4 walls
                ActionSpaceSize = 9,  // 8 directions + stop
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🎮 Player Movement Agent initialized");
        }

        public float[] EncodeState(PointF playerPos, List<EnemyBullet> bullets, Size windowSize)
        {
            List<float> state = new List<float>();

            // Normalized position
            state.Add(playerPos.X / windowSize.Width);
            state.Add(playerPos.Y / windowSize.Height);

            // Closest 3 bullets
            var nearestBullets = bullets
                .OrderBy(b => Distance(playerPos, b.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < nearestBullets.Count)
                {
                    var bullet = nearestBullets[i];
                    float dx = (bullet.Position.X - playerPos.X) / windowSize.Width;
                    float dy = (bullet.Position.Y - playerPos.Y) / windowSize.Height;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float velX = bullet.Velocity.X / 10f;
                    float velY = bullet.Velocity.Y / 10f;

                    state.Add(dx);
                    state.Add(dy);
                    state.Add(dist);
                    state.Add(velX);
                    state.Add(velY);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 1f, 2f, 0f, 0f });
                }
            }

            // Wall distances
            state.Add(playerPos.X / 100f);
            state.Add(playerPos.Y / 100f);
            state.Add((windowSize.Width - playerPos.X) / 100f);
            state.Add((windowSize.Height - playerPos.Y) / 100f);

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            float speed = GameConstants.PlayerSpeed;
            return action switch
            {
                0 => new PointF(0, -speed),      // North
                1 => new PointF(speed, -speed),  // NE
                2 => new PointF(speed, 0),       // East
                3 => new PointF(speed, speed),   // SE
                4 => new PointF(0, speed),       // South
                5 => new PointF(-speed, speed),  // SW
                6 => new PointF(-speed, 0),      // West
                7 => new PointF(-speed, -speed), // NW
                8 => new PointF(0, 0),           // Stop
                _ => new PointF(0, 0)
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                // Re-create trainer with proper config instead of using default
                trainer = new AITrainer(config);
                // In future, deserialize actual model weights here
            }
        }
    }

    public class PlayerShootingAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public PlayerShootingAgent(float epsilon = 0.2f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 14,  // 2 pos + 3*4 targets
                ActionSpaceSize = 10,  // 8 directions + don't shoot + shoot nearest
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🎯 Player Shooting Agent initialized");
        }

        public float[] EncodeState(PointF playerPos, List<Enemy> enemies, List<Boss> bosses, Size windowSize)
        {
            List<float> state = new List<float>();

            // Normalized position
            state.Add(playerPos.X / windowSize.Width);
            state.Add(playerPos.Y / windowSize.Height);

            // Combine enemies and bosses
            var allTargets = enemies.Select(e => (e.Position, e.Health))
                .Concat(bosses.Select(b => (b.Position, b.Health)))
                .OrderBy(t => Distance(playerPos, t.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < allTargets.Count)
                {
                    var target = allTargets[i];
                    float dx = (target.Position.X - playerPos.X) / windowSize.Width;
                    float dy = (target.Position.Y - playerPos.Y) / windowSize.Height;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float health = target.Health / 100f;

                    state.Add(dx);
                    state.Add(dy);
                    state.Add(dist);
                    state.Add(health);
                }
                else
                {
                    state.AddRange(new float[] { 2f, 2f, 3f, 0f });
                }
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF? ActionToVelocity(int action, PointF playerPos, List<Enemy> enemies, List<Boss> bosses)
        {
            float speed = GameConstants.LaserSpeed;

            if (action == 0) return null; // Don't shoot

            if (action == 9) // Shoot at nearest
            {
                var allTargets = enemies.Select(e => e.Position)
                    .Concat(bosses.Select(b => b.Position))
                    .ToList();

                if (allTargets.Count == 0) return null;

                var nearest = allTargets.OrderBy(t => Distance(playerPos, t)).First();
                float dx = nearest.X - playerPos.X;
                float dy = nearest.Y - playerPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist == 0) return null;

                return new PointF((dx / dist) * speed, (dy / dist) * speed);
            }

            // Actions 1-8: directional shooting
            return (action - 1) switch
            {
                0 => new PointF(0, -speed),      // North
                1 => new PointF(speed, -speed),  // NE
                2 => new PointF(speed, 0),       // East
                3 => new PointF(speed, speed),   // SE
                4 => new PointF(0, speed),       // South
                5 => new PointF(-speed, speed),  // SW
                6 => new PointF(-speed, 0),      // West
                7 => new PointF(-speed, -speed), // NW
                _ => null
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    public class CompanionMovementAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public CompanionMovementAgent(float epsilon = 0.2f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 32,  // 2 pos + 3 player + 3*5 bullets + 4 walls + 2*4 obstacles
                ActionSpaceSize = 9,
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🤖 Companion Movement Agent initialized");
        }

        public float[] EncodeState(PointF companionPos, PointF playerPos, List<EnemyBullet> bullets, Size windowSize, List<Obstacle>? obstacles = null)
        {
            List<float> state = new List<float>();

            // Companion position
            state.Add(companionPos.X / windowSize.Width);
            state.Add(companionPos.Y / windowSize.Height);

            // Player relative position
            float dx = (playerPos.X - companionPos.X) / windowSize.Width;
            float dy = (playerPos.Y - companionPos.Y) / windowSize.Height;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            state.Add(dx);
            state.Add(dy);
            state.Add(dist);

            // Closest 3 bullets
            var nearestBullets = bullets
                .OrderBy(b => Distance(companionPos, b.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < nearestBullets.Count)
                {
                    var bullet = nearestBullets[i];
                    float bdx = (bullet.Position.X - companionPos.X) / windowSize.Width;
                    float bdy = (bullet.Position.Y - companionPos.Y) / windowSize.Height;
                    float bdist = (float)Math.Sqrt(bdx * bdx + bdy * bdy);
                    float velX = bullet.Velocity.X / 10f;
                    float velY = bullet.Velocity.Y / 10f;

                    state.Add(bdx);
                    state.Add(bdy);
                    state.Add(bdist);
                    state.Add(velX);
                    state.Add(velY);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 1f, 2f, 0f, 0f });
                }
            }

            // Wall distances
            state.Add(companionPos.X / 100f);
            state.Add(companionPos.Y / 100f);
            state.Add((windowSize.Width - companionPos.X) / 100f);
            state.Add((windowSize.Height - companionPos.Y) / 100f);

            // Closest 2 obstacles (8 features)
            if (obstacles != null && obstacles.Count > 0)
            {
                var nearestObstacles = obstacles
                    .OrderBy(o => Distance(companionPos, o.Position))
                    .Take(2)
                    .ToList();

                for (int i = 0; i < 2; i++)
                {
                    if (i < nearestObstacles.Count)
                    {
                        var obstacle = nearestObstacles[i];
                        float odx = (obstacle.Position.X - companionPos.X) / windowSize.Width;
                        float ody = (obstacle.Position.Y - companionPos.Y) / windowSize.Height;
                        float odist = (float)Math.Sqrt(odx * odx + ody * ody);
                        float osize = (obstacle.Size.Width + obstacle.Size.Height) / (windowSize.Width + windowSize.Height);

                        state.Add(odx);
                        state.Add(ody);
                        state.Add(odist);
                        state.Add(osize);
                    }
                    else
                    {
                        state.AddRange(new float[] { 1f, 1f, 2f, 0f });
                    }
                }
            }
            else
            {
                state.AddRange(new float[] { 1f, 1f, 2f, 0f, 1f, 1f, 2f, 0f });
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            float speed = GameConstants.PlayerSpeed * 0.9f; // Slightly slower than player
            return action switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                8 => new PointF(0, 0),
                _ => new PointF(0, 0)
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    public class CompanionShootingAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public CompanionShootingAgent(float epsilon = 0.2f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 14,  // 2 pos + 3*4 targets
                ActionSpaceSize = 10,
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🎯 Companion Shooting Agent initialized");
        }

        public float[] EncodeState(PointF companionPos, List<Enemy> enemies, List<Boss> bosses, Size windowSize)
        {
            List<float> state = new List<float>();

            state.Add(companionPos.X / windowSize.Width);
            state.Add(companionPos.Y / windowSize.Height);

            var allTargets = enemies.Select(e => (e.Position, e.Health))
                .Concat(bosses.Select(b => (b.Position, b.Health)))
                .OrderBy(t => Distance(companionPos, t.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < allTargets.Count)
                {
                    var target = allTargets[i];
                    float dx = (target.Position.X - companionPos.X) / windowSize.Width;
                    float dy = (target.Position.Y - companionPos.Y) / windowSize.Height;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float health = target.Health / 100f;

                    state.Add(dx);
                    state.Add(dy);
                    state.Add(dist);
                    state.Add(health);
                }
                else
                {
                    state.AddRange(new float[] { 2f, 2f, 3f, 0f });
                }
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF? ActionToVelocity(int action, PointF companionPos, List<Enemy> enemies, List<Boss> bosses)
        {
            float speed = GameConstants.LaserSpeed;

            if (action == 0) return null;

            if (action == 9)
            {
                var allTargets = enemies.Select(e => e.Position)
                    .Concat(bosses.Select(b => b.Position))
                    .ToList();

                if (allTargets.Count == 0) return null;

                var nearest = allTargets.OrderBy(t => Distance(companionPos, t)).First();
                float dx = nearest.X - companionPos.X;
                float dy = nearest.Y - companionPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist == 0) return null;

                return new PointF((dx / dist) * speed, (dy / dist) * speed);
            }

            return (action - 1) switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                _ => null
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    public class EnemyMovementAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public EnemyMovementAgent(float epsilon = 0.3f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 20,  // 2 pos + 3 player + 3 companion + 2*5 lasers + 2 walls
                ActionSpaceSize = 9,
                LearningRate = 0.002f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("👾 Enemy Movement Agent initialized");
        }

        public float[] EncodeState(PointF enemyPos, PointF playerPos, List<Companion> companions, List<Laser> playerLasers, Size windowSize)
        {
            List<float> state = new List<float>();

            state.Add(enemyPos.X / windowSize.Width);
            state.Add(enemyPos.Y / windowSize.Height);

            // Player relative position
            float dx = (playerPos.X - enemyPos.X) / windowSize.Width;
            float dy = (playerPos.Y - enemyPos.Y) / windowSize.Height;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            state.Add(dx);
            state.Add(dy);
            state.Add(dist);

            // Nearest companion
            if (companions.Count > 0)
            {
                var nearest = companions.OrderBy(c => Distance(enemyPos, c.Position)).First();
                float cdx = (nearest.Position.X - enemyPos.X) / windowSize.Width;
                float cdy = (nearest.Position.Y - enemyPos.Y) / windowSize.Height;
                float cdist = (float)Math.Sqrt(cdx * cdx + cdy * cdy);
                state.Add(cdx);
                state.Add(cdy);
                state.Add(cdist);
            }
            else
            {
                state.AddRange(new float[] { 2f, 2f, 3f });
            }

            // Incoming lasers
            var nearLasers = playerLasers
                .OrderBy(l => Distance(enemyPos, l.Position))
                .Take(2)
                .ToList();

            for (int i = 0; i < 2; i++)
            {
                if (i < nearLasers.Count)
                {
                    var laser = nearLasers[i];
                    float ldx = (laser.Position.X - enemyPos.X) / windowSize.Width;
                    float ldy = (laser.Position.Y - enemyPos.Y) / windowSize.Height;
                    float ldist = (float)Math.Sqrt(ldx * ldx + ldy * ldy);
                    float velX = laser.Velocity.X / 10f;
                    float velY = laser.Velocity.Y / 10f;

                    state.Add(ldx);
                    state.Add(ldy);
                    state.Add(ldist);
                    state.Add(velX);
                    state.Add(velY);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 1f, 2f, 0f, 0f });
                }
            }

            // Wall distances
            state.Add(enemyPos.X / 100f);
            state.Add(enemyPos.Y / 100f);

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            float speed = GameConstants.EnemySpeed;
            return action switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                8 => new PointF(0, 0),
                _ => new PointF(0, 0)
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    public class EnemyShootingAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public EnemyShootingAgent(float epsilon = 0.3f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 13,  // 2 pos + 5 player + 2*3 companions
                ActionSpaceSize = 11,  // Don't shoot + 8 directions + shoot at player + predictive
                LearningRate = 0.002f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("👾 Enemy Shooting Agent initialized");
        }

        public float[] EncodeState(PointF enemyPos, PointF playerPos, PointF playerVel, List<Companion> companions, Size windowSize)
        {
            List<float> state = new List<float>();

            state.Add(enemyPos.X / windowSize.Width);
            state.Add(enemyPos.Y / windowSize.Height);

            // Player info
            float dx = (playerPos.X - enemyPos.X) / windowSize.Width;
            float dy = (playerPos.Y - enemyPos.Y) / windowSize.Height;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            state.Add(dx);
            state.Add(dy);
            state.Add(dist);
            state.Add(playerVel.X / 10f);
            state.Add(playerVel.Y / 10f);

            // Closest 2 companions
            var nearestCompanions = companions
                .OrderBy(c => Distance(enemyPos, c.Position))
                .Take(2)
                .ToList();

            for (int i = 0; i < 2; i++)
            {
                if (i < nearestCompanions.Count)
                {
                    var companion = nearestCompanions[i];
                    float cdx = (companion.Position.X - enemyPos.X) / windowSize.Width;
                    float cdy = (companion.Position.Y - enemyPos.Y) / windowSize.Height;
                    float cdist = (float)Math.Sqrt(cdx * cdx + cdy * cdy);

                    state.Add(cdx);
                    state.Add(cdy);
                    state.Add(cdist);
                }
                else
                {
                    state.AddRange(new float[] { 2f, 2f, 3f });
                }
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF? ActionToVelocity(int action, PointF enemyPos, PointF playerPos, PointF playerVel)
        {
            float speed = GameConstants.EnemyBulletSpeed;

            if (action == 0) return null;

            if (action == 9) // Shoot at player
            {
                float dx = playerPos.X - enemyPos.X;
                float dy = playerPos.Y - enemyPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist == 0) return null;
                return new PointF((dx / dist) * speed, (dy / dist) * speed);
            }

            if (action == 10) // Predictive shooting
            {
                float dx = (playerPos.X + playerVel.X * 10) - enemyPos.X;
                float dy = (playerPos.Y + playerVel.Y * 10) - enemyPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist == 0) return null;
                return new PointF((dx / dist) * speed, (dy / dist) * speed);
            }

            return (action - 1) switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                _ => null
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    public class BossMovementAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public BossMovementAgent(float epsilon = 0.25f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 32,  // 2 pos + 5 player + 2*3 companions + 3*3 lasers + 4 walls + 2*4 obstacles
                ActionSpaceSize = 12,  // 8 directions + stop + 3 special patterns
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("👹 Boss Movement Agent initialized");
        }

        public float[] EncodeState(PointF bossPos, PointF playerPos, List<Companion> companions, List<Laser> playerLasers, Size windowSize, List<Obstacle>? obstacles = null)
        {
            List<float> state = new List<float>();

            state.Add(bossPos.X / windowSize.Width);
            state.Add(bossPos.Y / windowSize.Height);

            // Player
            float dx = (playerPos.X - bossPos.X) / windowSize.Width;
            float dy = (playerPos.Y - bossPos.Y) / windowSize.Height;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            state.Add(dx);
            state.Add(dy);
            state.Add(dist);

            // Companions
            var nearestCompanions = companions
                .OrderBy(c => Distance(bossPos, c.Position))
                .Take(2)
                .ToList();

            for (int i = 0; i < 2; i++)
            {
                if (i < nearestCompanions.Count)
                {
                    var companion = nearestCompanions[i];
                    float cdx = (companion.Position.X - bossPos.X) / windowSize.Width;
                    float cdy = (companion.Position.Y - bossPos.Y) / windowSize.Height;
                    float cdist = (float)Math.Sqrt(cdx * cdx + cdy * cdy);
                    state.Add(cdx);
                    state.Add(cdy);
                    state.Add(cdist);
                }
                else
                {
                    state.AddRange(new float[] { 2f, 2f, 3f });
                }
            }

            // Incoming lasers
            var nearLasers = playerLasers
                .OrderBy(l => Distance(bossPos, l.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < nearLasers.Count)
                {
                    var laser = nearLasers[i];
                    float ldx = (laser.Position.X - bossPos.X) / windowSize.Width;
                    float ldy = (laser.Position.Y - bossPos.Y) / windowSize.Height;
                    float ldist = (float)Math.Sqrt(ldx * ldx + ldy * ldy);
                    state.Add(ldx);
                    state.Add(ldy);
                    state.Add(ldist);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 1f, 2f });
                }
            }

            // Walls
            state.Add(bossPos.X / 100f);
            state.Add(bossPos.Y / 100f);
            state.Add((windowSize.Width - bossPos.X) / 100f);
            state.Add((windowSize.Height - bossPos.Y) / 100f);

            // Nearest 2 obstacles (for tactical maneuvering and cover)
            if (obstacles != null && obstacles.Count > 0)
            {
                var nearestObstacles = obstacles
                    .OrderBy(o => Distance(bossPos, new PointF(o.Position.X + o.Size.Width / 2, o.Position.Y + o.Size.Height / 2)))
                    .Take(2)
                    .ToList();

                for (int i = 0; i < 2; i++)
                {
                    if (i < nearestObstacles.Count)
                    {
                        var obs = nearestObstacles[i];
                        float centerX = obs.Position.X + obs.Size.Width / 2;
                        float centerY = obs.Position.Y + obs.Size.Height / 2;
                        float odx = (centerX - bossPos.X) / windowSize.Width;
                        float ody = (centerY - bossPos.Y) / windowSize.Height;
                        float odist = (float)Math.Sqrt(odx * odx + ody * ody);
                        float osize = Math.Max(obs.Size.Width, obs.Size.Height) / 100f;

                        state.Add(odx);
                        state.Add(ody);
                        state.Add(odist);
                        state.Add(osize);
                    }
                    else
                    {
                        state.AddRange(new float[] { 2f, 2f, 3f, 0f });
                    }
                }
            }
            else
            {
                // No obstacles - add empty data
                state.AddRange(new float[] { 2f, 2f, 3f, 0f, 2f, 2f, 3f, 0f });
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            float speed = GameConstants.BossSpeed;
            return action switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                8 => new PointF(0, 0),
                9 => new PointF(speed * 1.5f, 0),   // Dash right
                10 => new PointF(-speed * 1.5f, 0),  // Dash left
                11 => new PointF(0, speed * 1.5f),   // Dash down
                _ => new PointF(0, 0)
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    public class BossShootingAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public BossShootingAgent(float epsilon = 0.25f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 16,  // 2 pos + 5 player + 3*3 companions
                ActionSpaceSize = 13,  // Don't shoot + 8 directions + shoot at player + predictive + burst + spread
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("👹 Boss Shooting Agent initialized");
        }

        public float[] EncodeState(PointF bossPos, PointF playerPos, PointF playerVel, List<Companion> companions, Size windowSize)
        {
            List<float> state = new List<float>();

            state.Add(bossPos.X / windowSize.Width);
            state.Add(bossPos.Y / windowSize.Height);

            // Player
            float dx = (playerPos.X - bossPos.X) / windowSize.Width;
            float dy = (playerPos.Y - bossPos.Y) / windowSize.Height;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            state.Add(dx);
            state.Add(dy);
            state.Add(dist);
            state.Add(playerVel.X / 10f);
            state.Add(playerVel.Y / 10f);

            // Companions
            var nearestCompanions = companions
                .OrderBy(c => Distance(bossPos, c.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < nearestCompanions.Count)
                {
                    var companion = nearestCompanions[i];
                    float cdx = (companion.Position.X - bossPos.X) / windowSize.Width;
                    float cdy = (companion.Position.Y - bossPos.Y) / windowSize.Height;
                    float cdist = (float)Math.Sqrt(cdx * cdx + cdy * cdy);
                    state.Add(cdx);
                    state.Add(cdy);
                    state.Add(cdist);
                }
                else
                {
                    state.AddRange(new float[] { 2f, 2f, 3f });
                }
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public List<PointF> ActionToVelocities(int action, PointF bossPos, PointF playerPos, PointF playerVel)
        {
            float speed = GameConstants.BossBulletSpeed;
            List<PointF> velocities = new List<PointF>();

            if (action == 0) return velocities; // Don't shoot

            if (action == 9) // Shoot at player
            {
                float dx = playerPos.X - bossPos.X;
                float dy = playerPos.Y - bossPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0)
                    velocities.Add(new PointF((dx / dist) * speed, (dy / dist) * speed));
                return velocities;
            }

            if (action == 10) // Predictive
            {
                float dx = (playerPos.X + playerVel.X * 15) - bossPos.X;
                float dy = (playerPos.Y + playerVel.Y * 15) - bossPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0)
                    velocities.Add(new PointF((dx / dist) * speed, (dy / dist) * speed));
                return velocities;
            }

            if (action == 11) // Burst (3 bullets toward player)
            {
                float dx = playerPos.X - bossPos.X;
                float dy = playerPos.Y - bossPos.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0)
                {
                    PointF baseVel = new PointF((dx / dist) * speed, (dy / dist) * speed);
                    velocities.Add(baseVel);
                    velocities.Add(new PointF(baseVel.X * 0.9f + baseVel.Y * 0.1f, baseVel.Y * 0.9f - baseVel.X * 0.1f));
                    velocities.Add(new PointF(baseVel.X * 0.9f - baseVel.Y * 0.1f, baseVel.Y * 0.9f + baseVel.X * 0.1f));
                }
                return velocities;
            }

            if (action == 12) // Spread (5 bullets in cone)
            {
                float dx = playerPos.X - bossPos.X;
                float dy = playerPos.Y - bossPos.Y;
                float baseAngle = (float)Math.Atan2(dy, dx);
                for (int i = -2; i <= 2; i++)
                {
                    float angle = baseAngle + i * 0.2f;
                    velocities.Add(new PointF((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed));
                }
                return velocities;
            }

            // Directional (actions 1-8)
            PointF? vel = (action - 1) switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                _ => null
            };

            if (vel.HasValue)
                velocities.Add(vel.Value);

            return velocities;
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    /// <summary>
    /// Companion Solo Movement Agent - for when player is dead and companions fight alone
    /// </summary>
    public class CompanionSoloMovementAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public CompanionSoloMovementAgent(float epsilon = 0.2f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 27,  // 2 pos + 3*5 bullets + 2*3 enemies + 4 walls
                ActionSpaceSize = 9,  // 8 directions + stop
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🤖💀 Companion Solo Movement Agent initialized");
        }

        public float[] EncodeState(PointF companionPos, List<EnemyBullet> bullets, List<Enemy> enemies, List<Boss> bosses, Size windowSize)
        {
            List<float> state = new List<float>();

            // Companion position
            state.Add(companionPos.X / windowSize.Width);
            state.Add(companionPos.Y / windowSize.Height);

            // Closest 3 bullets
            var nearestBullets = bullets
                .OrderBy(b => Distance(companionPos, b.Position))
                .Take(3)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                if (i < nearestBullets.Count)
                {
                    var bullet = nearestBullets[i];
                    float bdx = (bullet.Position.X - companionPos.X) / windowSize.Width;
                    float bdy = (bullet.Position.Y - companionPos.Y) / windowSize.Height;
                    float bdist = (float)Math.Sqrt(bdx * bdx + bdy * bdy);
                    float velX = bullet.Velocity.X / 10f;
                    float velY = bullet.Velocity.Y / 10f;

                    state.Add(bdx);
                    state.Add(bdy);
                    state.Add(bdist);
                    state.Add(velX);
                    state.Add(velY);
                }
                else
                {
                    state.AddRange(new float[] { 1f, 1f, 2f, 0f, 0f });
                }
            }

            // Nearest 2 enemies (to know where threats are)
            var allEnemies = enemies.Select(e => e.Position)
                .Concat(bosses.Select(b => b.Position))
                .OrderBy(p => Distance(companionPos, p))
                .Take(2)
                .ToList();

            for (int i = 0; i < 2; i++)
            {
                if (i < allEnemies.Count)
                {
                    var enemyPos = allEnemies[i];
                    float edx = (enemyPos.X - companionPos.X) / windowSize.Width;
                    float edy = (enemyPos.Y - companionPos.Y) / windowSize.Height;
                    float edist = (float)Math.Sqrt(edx * edx + edy * edy);

                    state.Add(edx);
                    state.Add(edy);
                    state.Add(edist);
                }
                else
                {
                    state.AddRange(new float[] { 2f, 2f, 3f });
                }
            }

            // Wall distances
            state.Add(companionPos.X / 100f);
            state.Add(companionPos.Y / 100f);
            state.Add((windowSize.Width - companionPos.X) / 100f);
            state.Add((windowSize.Height - companionPos.Y) / 100f);

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            float speed = GameConstants.PlayerSpeed * 0.9f; // Same as regular companion
            return action switch
            {
                0 => new PointF(0, -speed),
                1 => new PointF(speed, -speed),
                2 => new PointF(speed, 0),
                3 => new PointF(speed, speed),
                4 => new PointF(0, speed),
                5 => new PointF(-speed, speed),
                6 => new PointF(-speed, 0),
                7 => new PointF(-speed, -speed),
                8 => new PointF(0, 0),
                _ => new PointF(0, 0)
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    /// <summary>
    /// Player Stealth Movement Agent - sneaking past enemies with limited detection range
    /// State: player pos (2) + closest 5 enemies (15: pos, velocity, detection status) + walls (4) = 21
    /// </summary>
    public class PlayerStealthMovementAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public PlayerStealthMovementAgent(float epsilon = 0.2f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 21,  // 2 pos + 5*3 enemies + 4 walls
                ActionSpaceSize = 9,  // 8 directions + stop
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🥷 Player Stealth Movement Agent initialized");
        }

        public float[] EncodeState(PointF playerPos, List<Enemy> enemies, Size windowSize)
        {
            List<float> state = new List<float>();

            // Normalized position
            state.Add(playerPos.X / windowSize.Width);
            state.Add(playerPos.Y / windowSize.Height);

            // Closest 5 enemies (position + if within detection range)
            var nearestEnemies = enemies
                .OrderBy(e => Distance(playerPos, e.Position))
                .Take(5)
                .ToList();

            for (int i = 0; i < 5; i++)
            {
                if (i < nearestEnemies.Count)
                {
                    var enemy = nearestEnemies[i];
                    state.Add(enemy.Position.X / windowSize.Width);
                    state.Add(enemy.Position.Y / windowSize.Height);
                    float dist = Distance(playerPos, enemy.Position);
                    state.Add(dist < GameConstants.EnemyDetectionRange ? 1f : 0f); // Detection status
                }
                else
                {
                    state.Add(0.5f); // x
                    state.Add(0.5f); // y
                    state.Add(0f);   // not detected
                }
            }

            // Distance to walls
            state.Add(playerPos.X / windowSize.Width); // left
            state.Add((windowSize.Width - playerPos.X) / windowSize.Width); // right
            state.Add(playerPos.Y / windowSize.Height); // top
            state.Add((windowSize.Height - playerPos.Y) / windowSize.Height); // bottom

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            return action switch
            {
                0 => new PointF(0, -GameConstants.PlayerSpeed),      // Up
                1 => new PointF(GameConstants.PlayerSpeed, -GameConstants.PlayerSpeed),   // Up-Right
                2 => new PointF(GameConstants.PlayerSpeed, 0),       // Right
                3 => new PointF(GameConstants.PlayerSpeed, GameConstants.PlayerSpeed),    // Down-Right
                4 => new PointF(0, GameConstants.PlayerSpeed),       // Down
                5 => new PointF(-GameConstants.PlayerSpeed, GameConstants.PlayerSpeed),   // Down-Left
                6 => new PointF(-GameConstants.PlayerSpeed, 0),      // Left
                7 => new PointF(-GameConstants.PlayerSpeed, -GameConstants.PlayerSpeed),  // Up-Left
                _ => new PointF(0, 0)                                // Stop
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }

    /// <summary>
    /// Enemy Patrol Agent - maximize area coverage while patrolling
    /// State: enemy pos (2) + velocity (2) + walls (4) + area coverage heatmap (9 grid cells) = 17
    /// </summary>
    public class EnemyPatrolAgent
    {
        private AITrainer trainer;
        private AITrainer.ModelConfig config;
        private Random random;
        private float epsilon;

        public EnemyPatrolAgent(float epsilon = 0.3f)
        {
            this.epsilon = epsilon;
            this.random = new Random();

            config = new AITrainer.ModelConfig
            {
                StateSpaceSize = 17,  // 2 pos + 2 vel + 4 walls + 9 grid coverage
                ActionSpaceSize = 9,  // 8 directions + stop
                LearningRate = 0.001f,
                ExplorationRate = epsilon
            };

            trainer = new AITrainer(config);
            Console.WriteLine("🚶 Enemy Patrol Agent initialized");
        }

        public float[] EncodeState(PointF enemyPos, PointF velocity, float[,] areaCoverage, Size windowSize)
        {
            List<float> state = new List<float>();

            // Normalized position
            state.Add(enemyPos.X / windowSize.Width);
            state.Add(enemyPos.Y / windowSize.Height);

            // Velocity
            state.Add(velocity.X / GameConstants.StealthEnemyPatrolSpeed);
            state.Add(velocity.Y / GameConstants.StealthEnemyPatrolSpeed);

            // Distance to walls
            state.Add(enemyPos.X / windowSize.Width); // left
            state.Add((windowSize.Width - enemyPos.X) / windowSize.Width); // right
            state.Add(enemyPos.Y / windowSize.Height); // top
            state.Add((windowSize.Height - enemyPos.Y) / windowSize.Height); // bottom

            // Area coverage (3x3 grid)
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    state.Add(areaCoverage[i, j]);
                }
            }

            return state.ToArray();
        }

        public int SelectAction(float[] state)
        {
            return trainer.PredictAction(state);
        }

        public void Learn(float[] state, int action, float reward, float[] nextState, bool done)
        {
            var experience = new DataCollector.Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                IsDone = done
            };
            trainer.TrainOnBatch(new List<DataCollector.Experience> { experience });
        }

        public PointF ActionToVelocity(int action)
        {
            int speed = GameConstants.StealthEnemyPatrolSpeed;
            return action switch
            {
                0 => new PointF(0, -speed),      // Up
                1 => new PointF(speed, -speed),  // Up-Right
                2 => new PointF(speed, 0),       // Right
                3 => new PointF(speed, speed),   // Down-Right
                4 => new PointF(0, speed),       // Down
                5 => new PointF(-speed, speed),  // Down-Left
                6 => new PointF(-speed, 0),      // Left
                7 => new PointF(-speed, -speed), // Up-Left
                _ => new PointF(0, 0)            // Stop
            };
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void SaveModel(string path)
        {
            trainer.ExportModel(path);
        }

        public void LoadModel(string path)
        {
            if (System.IO.File.Exists(path))
            {
                trainer = new AITrainer(config);
            }
        }
    }
}
