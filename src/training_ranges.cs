using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Training Range System - isolated environments for training each AI type separately.
    /// Ensures no overlaps between different training scenarios.
    /// </summary>
    public class TrainingRangeSystem
    {
        private Random random = new Random();

        // Training range boundaries (non-overlapping regions)
        public class RangeDefinition
        {
            public Rectangle Bounds { get; set; }
            public string Name { get; set; } = "";
        }

        private Dictionary<string, RangeDefinition> ranges;

        public TrainingRangeSystem(Size windowSize)
        {
            // Divide screen into 9 non-overlapping regions for each AI type
            int width = windowSize.Width;
            int height = windowSize.Height;
            int w3 = width / 3;
            int h3 = height / 3;

            ranges = new Dictionary<string, RangeDefinition>
            {
                ["PlayerMovement"] = new RangeDefinition { Bounds = new Rectangle(0, 0, w3, h3), Name = "Player Movement Range" },
                ["PlayerShooting"] = new RangeDefinition { Bounds = new Rectangle(w3, 0, w3, h3), Name = "Player Shooting Range" },
                ["CompanionMovement"] = new RangeDefinition { Bounds = new Rectangle(w3 * 2, 0, w3, h3), Name = "Companion Movement Range" },
                ["CompanionShooting"] = new RangeDefinition { Bounds = new Rectangle(0, h3, w3, h3), Name = "Companion Shooting Range" },
                ["CompanionSoloMovement"] = new RangeDefinition { Bounds = new Rectangle(w3, h3, w3, h3), Name = "Companion Solo Movement Range" },
                ["EnemyMovement"] = new RangeDefinition { Bounds = new Rectangle(w3 * 2, h3, w3, h3), Name = "Enemy Movement Range" },
                ["EnemyShooting"] = new RangeDefinition { Bounds = new Rectangle(0, h3 * 2, w3, h3), Name = "Enemy Shooting Range" },
                ["BossMovement"] = new RangeDefinition { Bounds = new Rectangle(w3, h3 * 2, w3, h3), Name = "Boss Movement Range" },
                ["BossShooting"] = new RangeDefinition { Bounds = new Rectangle(w3 * 2, h3 * 2, w3, h3), Name = "Boss Shooting Range" }
            };
        }

        public Rectangle GetRange(string aiType)
        {
            return ranges[aiType].Bounds;
        }

        public PointF GetRandomPositionInRange(string aiType)
        {
            var bounds = ranges[aiType].Bounds;
            float x = bounds.Left + random.Next(100, bounds.Width - 100);
            float y = bounds.Top + random.Next(100, bounds.Height - 100);
            return new PointF(x, y);
        }

        public bool IsInRange(string aiType, PointF position)
        {
            var bounds = ranges[aiType].Bounds;
            return bounds.Contains((int)position.X, (int)position.Y);
        }

        /// <summary>
        /// Player Movement Training: Dodge bullets from enemies
        /// </summary>
        public class PlayerMovementTraining
        {
            private PlayerMovementAgent agent;
            private Random random = new Random();

            public PlayerMovementTraining(PlayerMovementAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                // Spawn player at random position
                PointF playerPos = new PointF(
                    rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                    rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                );

                List<EnemyBullet> bullets = new List<EnemyBullet>();
                float[] lastState = null!;
                int lastAction = 0;
                int stationaryFrames = 0;
                PointF lastPos = playerPos;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Spawn bullets periodically
                    if (frame % 30 == 0)
                    {
                        PointF spawnPos = new PointF(
                            rangeBounds.Left + random.Next(rangeBounds.Width),
                            rangeBounds.Top + random.Next(rangeBounds.Height)
                        );
                        PointF direction = new PointF(playerPos.X - spawnPos.X, playerPos.Y - spawnPos.Y);
                        float dist = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                        if (dist > 0)
                        {
                            float speed = 5f;
                            bullets.Add(new EnemyBullet(spawnPos, new PointF((direction.X / dist) * speed, (direction.Y / dist) * speed)));
                        }
                    }

                    // Get state
                    float[] state = agent.EncodeState(playerPos, bullets, windowSize);

                    // Select action
                    int action = agent.SelectAction(state);
                    PointF velocity = agent.ActionToVelocity(action);

                    // Update position
                    PointF newPos = new PointF(
                        Math.Clamp(playerPos.X + velocity.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                        Math.Clamp(playerPos.Y + velocity.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                    );

                    // Check if stationary
                    if (Distance(newPos, lastPos) < 1f)
                        stationaryFrames++;
                    else
                        stationaryFrames = 0;

                    // Update bullets
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        bullets[i] = bullets[i] with
                        {
                            Position = new PointF(
                                bullets[i].Position.X + bullets[i].Velocity.X,
                                bullets[i].Position.Y + bullets[i].Velocity.Y
                            )
                        };

                        // Remove out-of-bounds bullets
                        if (!rangeBounds.Contains((int)bullets[i].Position.X, (int)bullets[i].Position.Y))
                        {
                            bullets.RemoveAt(i);
                        }
                    }

                    // Calculate reward
                    float reward = 0f;

                    // Check bullet hits
                    bool hit = false;
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        if (Distance(newPos, bullets[i].Position) < 15f)
                        {
                            reward -= 50f; // Big penalty for getting hit
                            bullets.RemoveAt(i);
                            hit = true;
                        }
                    }

                    // Reward for survival and dodging bullets
                    if (!hit)
                        reward += 2f;

                    // Penalty for staying still
                    if (stationaryFrames > 10)
                        reward -= 15f;

                    // Bonus for movement (encourages active dodging)
                    if (Distance(newPos, lastPos) > 2f)
                        reward += 3f;

                    // Wall proximity penalty - scales smoothly with pixel distance
                    float distToLeft = newPos.X - rangeBounds.Left;
                    float distToRight = rangeBounds.Right - newPos.X;
                    float distToTop = newPos.Y - rangeBounds.Top;
                    float distToBottom = rangeBounds.Bottom - newPos.Y;
                    float minWallDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));

                    // Continuous scaling: closer = worse penalty
                    // At 0px: -20, at 50px: -5, at 100px: -1.25, at 150px+: 0
                    if (minWallDist < 150f)
                    {
                        float normalizedDist = minWallDist / 150f; // 0 to 1
                        float wallPenalty = -20f * (float)Math.Pow(1f - normalizedDist, 2);
                        reward += wallPenalty;
                    }

                    // Learn from experience
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, hit);
                    }

                    lastState = state;
                    lastAction = action;
                    lastPos = playerPos;
                    playerPos = newPos;

                    if (hit) break; // End episode on hit
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Player Shooting Training: Shoot at AI enemies from random positions
        /// </summary>
        public class PlayerShootingTraining
        {
            private PlayerShootingAgent agent;
            private EnemyMovementAgent enemyMovement;
            private Random random = new Random();

            public PlayerShootingTraining(PlayerShootingAgent agent, EnemyMovementAgent enemyMovement)
            {
                this.agent = agent;
                this.enemyMovement = enemyMovement;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                // Player at random position
                PointF playerPos = new PointF(
                    rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                    rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                );

                // Spawn 3-5 enemies
                List<Enemy> enemies = new List<Enemy>();
                int enemyCount = random.Next(3, 6);
                for (int i = 0; i < enemyCount; i++)
                {
                    PointF enemyPos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    enemies.Add(new Enemy(enemyPos, GameConstants.EnemySpeed, DateTime.Now, EnemyBehavior.Aggressive, DateTime.Now, -1)
                    {
                        Health = GameConstants.EnemyHealth
                    });
                }

                List<Laser> lasers = new List<Laser>();
                float[] lastState = null!;
                int lastAction = 0;
                int shotsFired = 0;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Get state
                    float[] state = agent.EncodeState(playerPos, enemies, new List<Boss>(), windowSize);

                    // Select action
                    int action = agent.SelectAction(state);

                    // Execute action
                    PointF? laserVel = agent.ActionToVelocity(action, playerPos, enemies, new List<Boss>());
                    if (laserVel.HasValue)
                    {
                        lasers.Add(new Laser(playerPos, laserVel.Value));
                        shotsFired++;
                    }

                    // Update lasers
                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        lasers[i] = lasers[i] with
                        {
                            Position = new PointF(
                                lasers[i].Position.X + lasers[i].Velocity.X,
                                lasers[i].Position.Y + lasers[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)lasers[i].Position.X, (int)lasers[i].Position.Y))
                        {
                            lasers.RemoveAt(i);
                        }
                    }

                    // Update enemies (simple movement for training)
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        PointF enemyPos = enemies[i].Position;
                        PointF newEnemyPos = new PointF(
                            enemyPos.X + (random.NextSingle() - 0.5f) * 4f,
                            enemyPos.Y + (random.NextSingle() - 0.5f) * 4f
                        );
                        newEnemyPos = new PointF(
                            Math.Clamp(newEnemyPos.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(newEnemyPos.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );
                        enemies[i] = enemies[i] with { Position = newEnemyPos };
                    }

                    // Check collisions
                    float reward = -0.1f; // Small penalty for time

                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        for (int j = enemies.Count - 1; j >= 0; j--)
                        {
                            if (Distance(lasers[i].Position, enemies[j].Position) < 20f)
                            {
                                reward += 10f; // Hit enemy
                                enemies[j] = enemies[j] with { Health = enemies[j].Health - GameConstants.LaserDamage };
                                lasers.RemoveAt(i);

                                if (enemies[j].Health <= 0)
                                {
                                    reward += 20f; // Destroyed enemy
                                    enemies.RemoveAt(j);
                                }
                                break;
                            }
                        }
                    }

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, enemies.Count == 0);
                    }

                    lastState = state;
                    lastAction = action;

                    if (enemies.Count == 0) break; // All enemies destroyed
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Companion Movement Training: Dodge bullets while staying near simulated player
        /// </summary>
        public class CompanionMovementTraining
        {
            private CompanionMovementAgent agent;
            private Random random = new Random();

            public CompanionMovementTraining(CompanionMovementAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                // Simulated player that moves in a circle
                PointF playerCenter = new PointF(
                    rangeBounds.Left + rangeBounds.Width / 2,
                    rangeBounds.Top + rangeBounds.Height / 2
                );
                float circleRadius = Math.Min(rangeBounds.Width, rangeBounds.Height) / 3f;

                PointF companionPos = new PointF(playerCenter.X + 100, playerCenter.Y);
                List<EnemyBullet> bullets = new List<EnemyBullet>();

                // Create static training obstacles
                List<Obstacle> obstacles = new List<Obstacle>
                {
                    new Obstacle(new PointF(playerCenter.X - 150, playerCenter.Y - 100), new SizeF(80, 60)),
                    new Obstacle(new PointF(playerCenter.X + 100, playerCenter.Y + 80), new SizeF(60, 80)),
                    new Obstacle(new PointF(playerCenter.X - 50, playerCenter.Y + 120), new SizeF(70, 50))
                };

                float[] lastState = null!;
                int lastAction = 0;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Update simulated player position (circular movement)
                    float angle = frame * 0.02f;
                    PointF playerPos = new PointF(
                        playerCenter.X + (float)Math.Cos(angle) * circleRadius,
                        playerCenter.Y + (float)Math.Sin(angle) * circleRadius
                    );

                    // Spawn bullets
                    if (frame % 25 == 0)
                    {
                        PointF spawnPos = new PointF(
                            rangeBounds.Left + random.Next(rangeBounds.Width),
                            rangeBounds.Top + random.Next(rangeBounds.Height)
                        );
                        PointF direction = new PointF(companionPos.X - spawnPos.X, companionPos.Y - spawnPos.Y);
                        float dist = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                        if (dist > 0)
                        {
                            float speed = 5f;
                            bullets.Add(new EnemyBullet(spawnPos, new PointF((direction.X / dist) * speed, (direction.Y / dist) * speed)));
                        }
                    }

                    // Get state
                    float[] state = agent.EncodeState(companionPos, playerPos, bullets, windowSize, obstacles);

                    // Select action
                    int action = agent.SelectAction(state);
                    PointF velocity = agent.ActionToVelocity(action);

                    // Update position
                    PointF newPos = new PointF(
                        Math.Clamp(companionPos.X + velocity.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                        Math.Clamp(companionPos.Y + velocity.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                    );

                    // Update bullets
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        bullets[i] = bullets[i] with
                        {
                            Position = new PointF(
                                bullets[i].Position.X + bullets[i].Velocity.X,
                                bullets[i].Position.Y + bullets[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)bullets[i].Position.X, (int)bullets[i].Position.Y))
                        {
                            bullets.RemoveAt(i);
                        }
                    }

                    // Calculate reward
                    float reward = 0f;
                    bool hit = false;

                    // Check bullet hits
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        if (Distance(newPos, bullets[i].Position) < 15f)
                        {
                            reward -= 50f;
                            bullets.RemoveAt(i);
                            hit = true;
                        }
                    }

                    // Reward for survival and bullet dodging
                    if (!hit)
                    {
                        reward += 2f; // Base survival

                        // Bonus for dodging close bullets (near-miss reward)
                        float closestBulletDist = float.MaxValue;
                        foreach (var bullet in bullets)
                        {
                            float dist = Distance(newPos, bullet.Position);
                            if (dist < closestBulletDist)
                                closestBulletDist = dist;
                        }
                        if (closestBulletDist < 30f && closestBulletDist >= 15f)
                            reward += 1.5f; // Near-miss bonus
                    }

                    // Distance from player - more rewarding for staying close
                    float distFromPlayer = Distance(newPos, playerPos);
                    if (distFromPlayer > 200f)
                        reward -= 8f; // Stronger penalty for being too far
                    else if (distFromPlayer < 100f)
                        reward += 5f; // Strong bonus for optimal proximity
                    else if (distFromPlayer < 150f)
                        reward += 3.5f; // Good bonus for staying close

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, hit);
                    }

                    lastState = state;
                    lastAction = action;
                    companionPos = newPos;

                    if (hit) break;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Companion Shooting Training: Similar to player shooting
        /// </summary>
        public class CompanionShootingTraining
        {
            private CompanionShootingAgent agent;
            private Random random = new Random();

            public CompanionShootingTraining(CompanionShootingAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                PointF companionPos = new PointF(
                    rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                    rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                );

                List<Enemy> enemies = new List<Enemy>();
                int enemyCount = random.Next(3, 6);
                for (int i = 0; i < enemyCount; i++)
                {
                    PointF enemyPos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    enemies.Add(new Enemy(enemyPos, GameConstants.EnemySpeed, DateTime.Now, EnemyBehavior.Aggressive, DateTime.Now, -1)
                    {
                        Health = GameConstants.EnemyHealth
                    });
                }

                List<Laser> lasers = new List<Laser>();
                float[] lastState = null!;
                int lastAction = 0;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    float[] state = agent.EncodeState(companionPos, enemies, new List<Boss>(), windowSize);
                    int action = agent.SelectAction(state);
                    PointF? laserVel = agent.ActionToVelocity(action, companionPos, enemies, new List<Boss>());

                    if (laserVel.HasValue)
                    {
                        lasers.Add(new Laser(companionPos, laserVel.Value));
                    }

                    // Update lasers
                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        lasers[i] = lasers[i] with
                        {
                            Position = new PointF(
                                lasers[i].Position.X + lasers[i].Velocity.X,
                                lasers[i].Position.Y + lasers[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)lasers[i].Position.X, (int)lasers[i].Position.Y))
                        {
                            lasers.RemoveAt(i);
                        }
                    }

                    // Update enemies
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        PointF enemyPos = enemies[i].Position;
                        PointF newEnemyPos = new PointF(
                            enemyPos.X + (random.NextSingle() - 0.5f) * 4f,
                            enemyPos.Y + (random.NextSingle() - 0.5f) * 4f
                        );
                        newEnemyPos = new PointF(
                            Math.Clamp(newEnemyPos.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(newEnemyPos.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );
                        enemies[i] = enemies[i] with { Position = newEnemyPos };
                    }

                    // Check collisions
                    float reward = -0.1f;

                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        for (int j = enemies.Count - 1; j >= 0; j--)
                        {
                            if (Distance(lasers[i].Position, enemies[j].Position) < 20f)
                            {
                                reward += 10f;
                                enemies[j] = enemies[j] with { Health = enemies[j].Health - GameConstants.LaserDamage };
                                lasers.RemoveAt(i);

                                if (enemies[j].Health <= 0)
                                {
                                    reward += 20f;
                                    enemies.RemoveAt(j);
                                }
                                break;
                            }
                        }
                    }

                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, enemies.Count == 0);
                    }

                    lastState = state;
                    lastAction = action;

                    if (enemies.Count == 0) break;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Enemy Movement Training: Move to avoid player lasers while approaching
        /// </summary>
        public class EnemyMovementTraining
        {
            private EnemyMovementAgent agent;
            private Random random = new Random();

            public EnemyMovementTraining(EnemyMovementAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                // Simulated player
                PointF playerPos = new PointF(
                    rangeBounds.Left + rangeBounds.Width / 2,
                    rangeBounds.Top + rangeBounds.Height / 2
                );

                // Enemy starts at random edge
                PointF enemyPos = new PointF(
                    rangeBounds.Left + random.Next(rangeBounds.Width),
                    rangeBounds.Top + random.Next(rangeBounds.Height)
                );

                List<Laser> lasers = new List<Laser>();
                float[] lastState = null!;
                int lastAction = 0;
                int stationaryFrames = 0;
                PointF lastPos = enemyPos;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Spawn lasers from player
                    if (frame % 20 == 0)
                    {
                        PointF direction = new PointF(enemyPos.X - playerPos.X, enemyPos.Y - playerPos.Y);
                        float dist = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                        if (dist > 0)
                        {
                            float speed = GameConstants.LaserSpeed;
                            lasers.Add(new Laser(playerPos, new PointF((direction.X / dist) * speed, (direction.Y / dist) * speed)));
                        }
                    }

                    // Get state
                    float[] state = agent.EncodeState(enemyPos, playerPos, new List<Companion>(), lasers, windowSize);

                    // Select action
                    int action = agent.SelectAction(state);
                    PointF velocity = agent.ActionToVelocity(action);

                    // Update position
                    PointF newPos = new PointF(
                        Math.Clamp(enemyPos.X + velocity.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                        Math.Clamp(enemyPos.Y + velocity.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                    );

                    // Check stationary
                    if (Distance(newPos, lastPos) < 1f)
                        stationaryFrames++;
                    else
                        stationaryFrames = 0;

                    // Update lasers
                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        lasers[i] = lasers[i] with
                        {
                            Position = new PointF(
                                lasers[i].Position.X + lasers[i].Velocity.X,
                                lasers[i].Position.Y + lasers[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)lasers[i].Position.X, (int)lasers[i].Position.Y))
                        {
                            lasers.RemoveAt(i);
                        }
                    }

                    // Calculate reward
                    float reward = 0f;
                    bool hit = false;

                    // Check laser hits
                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        if (Distance(newPos, lasers[i].Position) < 15f)
                        {
                            reward -= 30f;
                            lasers.RemoveAt(i);
                            hit = true;
                        }
                    }

                    // Reward for survival
                    if (!hit)
                        reward += 1f;

                    // Penalty for being stationary
                    if (stationaryFrames > 10)
                        reward -= 8f;

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, hit);
                    }

                    lastState = state;
                    lastAction = action;
                    lastPos = enemyPos;
                    enemyPos = newPos;

                    if (hit) break;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Enemy Shooting Training: Train to hit moving player and companions
        /// </summary>
        public class EnemyShootingTraining
        {
            private EnemyShootingAgent agent;
            private Random random = new Random();

            public EnemyShootingTraining(EnemyShootingAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                PointF enemyPos = new PointF(
                    rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                    rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                );

                // Multiple moving players and companions
                List<(PointF pos, PointF vel, bool isPlayer)> targets = new List<(PointF, PointF, bool)>();
                int targetCount = random.Next(3, 7);
                for (int i = 0; i < targetCount; i++)
                {
                    PointF pos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    PointF vel = new PointF(
                        (random.NextSingle() - 0.5f) * 4f,
                        (random.NextSingle() - 0.5f) * 4f
                    );
                    targets.Add((pos, vel, i == 0));
                }

                List<EnemyBullet> bullets = new List<EnemyBullet>();
                float[] lastState = null!;
                int lastAction = 0;
                int hits = 0;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Update targets
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var (pos, vel, isPlayer) = targets[i];
                        PointF newPos = new PointF(
                            Math.Clamp(pos.X + vel.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(pos.Y + vel.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );

                        // Bounce off walls
                        PointF newVel = vel;
                        if (newPos.X <= rangeBounds.Left + 20 || newPos.X >= rangeBounds.Right - 20)
                            newVel = new PointF(-vel.X, vel.Y);
                        if (newPos.Y <= rangeBounds.Top + 20 || newPos.Y >= rangeBounds.Bottom - 20)
                            newVel = new PointF(vel.X, -vel.Y);

                        targets[i] = (newPos, newVel, isPlayer);
                    }

                    // Get state (using first target as player)
                    var playerTarget = targets.Find(t => t.isPlayer);
                    var companions = targets.Where(t => !t.isPlayer).Select((t, idx) => new Companion(t.pos, t.vel, idx, CompanionRole.LeftFlank, t.pos, DateTime.Now)).ToList();

                    float[] state = agent.EncodeState(enemyPos, playerTarget.pos, playerTarget.vel, companions, windowSize);

                    // Select action
                    int action = agent.SelectAction(state);
                    PointF? bulletVel = agent.ActionToVelocity(action, enemyPos, playerTarget.pos, playerTarget.vel);

                    if (bulletVel.HasValue && bullets.Count < 20)
                    {
                        bullets.Add(new EnemyBullet(enemyPos, bulletVel.Value));
                    }

                    // Update bullets
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        bullets[i] = bullets[i] with
                        {
                            Position = new PointF(
                                bullets[i].Position.X + bullets[i].Velocity.X,
                                bullets[i].Position.Y + bullets[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)bullets[i].Position.X, (int)bullets[i].Position.Y))
                        {
                            bullets.RemoveAt(i);
                        }
                    }

                    // Check hits
                    float reward = -0.1f;

                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        foreach (var target in targets)
                        {
                            if (Distance(bullets[i].Position, target.pos) < 15f)
                            {
                                reward += target.isPlayer ? 25f : 15f; // More reward for hitting player
                                bullets.RemoveAt(i);
                                hits++;
                                break;
                            }
                        }
                    }

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, false);
                    }

                    lastState = state;
                    lastAction = action;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Boss Movement and Shooting Training
        /// </summary>
        public class BossMovementTraining
        {
            private BossMovementAgent agent;
            private Random random = new Random();

            public BossMovementTraining(BossMovementAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 700)
            {
                PointF bossPos = new PointF(
                    rangeBounds.Left + rangeBounds.Width / 2,
                    rangeBounds.Top + rangeBounds.Height / 2
                );

                PointF playerPos = new PointF(
                    rangeBounds.Left + random.Next(rangeBounds.Width),
                    rangeBounds.Top + random.Next(rangeBounds.Height)
                );

                List<Companion> companions = new List<Companion>();
                for (int i = 0; i < 2; i++)
                {
                    PointF companionPos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    companions.Add(new Companion(companionPos, PointF.Empty, i, CompanionRole.LeftFlank, companionPos, DateTime.Now));
                }

                // Create static training obstacles for tactical maneuvering
                PointF center = new PointF(
                    rangeBounds.Left + rangeBounds.Width / 2,
                    rangeBounds.Top + rangeBounds.Height / 2
                );
                List<Obstacle> obstacles = new List<Obstacle>
                {
                    new Obstacle(new PointF(center.X - 200, center.Y - 150), new SizeF(90, 70)),
                    new Obstacle(new PointF(center.X + 120, center.Y + 100), new SizeF(75, 85)),
                    new Obstacle(new PointF(center.X - 80, center.Y + 180), new SizeF(85, 60)),
                    new Obstacle(new PointF(center.X + 150, center.Y - 120), new SizeF(65, 75))
                };

                List<Laser> lasers = new List<Laser>();
                float[] lastState = null!;
                int lastAction = 0;
                int stationaryFrames = 0;
                PointF lastPos = bossPos;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Spawn lasers
                    if (frame % 15 == 0)
                    {
                        PointF direction = new PointF(bossPos.X - playerPos.X, bossPos.Y - playerPos.Y);
                        float dist = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                        if (dist > 0)
                        {
                            float speed = GameConstants.LaserSpeed;
                            lasers.Add(new Laser(playerPos, new PointF((direction.X / dist) * speed, (direction.Y / dist) * speed)));
                        }
                    }

                    // Get state
                    float[] state = agent.EncodeState(bossPos, playerPos, companions, lasers, windowSize, obstacles);

                    // Select action
                    int action = agent.SelectAction(state);
                    PointF velocity = agent.ActionToVelocity(action);

                    // Update position
                    PointF newPos = new PointF(
                        Math.Clamp(bossPos.X + velocity.X, rangeBounds.Left + 30, rangeBounds.Right - 30),
                        Math.Clamp(bossPos.Y + velocity.Y, rangeBounds.Top + 30, rangeBounds.Bottom - 30)
                    );

                    if (Distance(newPos, lastPos) < 1f)
                        stationaryFrames++;
                    else
                        stationaryFrames = 0;

                    // Update lasers
                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        lasers[i] = lasers[i] with
                        {
                            Position = new PointF(
                                lasers[i].Position.X + lasers[i].Velocity.X,
                                lasers[i].Position.Y + lasers[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)lasers[i].Position.X, (int)lasers[i].Position.Y))
                        {
                            lasers.RemoveAt(i);
                        }
                    }

                    // Calculate reward
                    float reward = 0f;
                    bool hit = false;

                    for (int i = lasers.Count - 1; i >= 0; i--)
                    {
                        if (Distance(newPos, lasers[i].Position) < 25f)
                        {
                            reward -= 20f;
                            lasers.RemoveAt(i);
                            hit = true;
                        }
                    }

                    if (!hit)
                        reward += 1f;

                    if (stationaryFrames > 15)
                        reward -= 10f;

                    // Walls
                    if (newPos.X < rangeBounds.Left + 80 || newPos.X > rangeBounds.Right - 80 ||
                        newPos.Y < rangeBounds.Top + 80 || newPos.Y > rangeBounds.Bottom - 80)
                        reward -= 5f;

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, hit);
                    }

                    lastState = state;
                    lastAction = action;
                    lastPos = bossPos;
                    bossPos = newPos;

                    if (hit) break;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public class BossShootingTraining
        {
            private BossShootingAgent agent;
            private Random random = new Random();

            public BossShootingTraining(BossShootingAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 700)
            {
                PointF bossPos = new PointF(
                    rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                    rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                );

                // Multiple targets
                List<(PointF pos, PointF vel, bool isPlayer)> targets = new List<(PointF, PointF, bool)>();
                int targetCount = random.Next(4, 8);
                for (int i = 0; i < targetCount; i++)
                {
                    PointF pos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    PointF vel = new PointF(
                        (random.NextSingle() - 0.5f) * 5f,
                        (random.NextSingle() - 0.5f) * 5f
                    );
                    targets.Add((pos, vel, i == 0));
                }

                List<EnemyBullet> bullets = new List<EnemyBullet>();
                float[] lastState = null!;
                int lastAction = 0;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Update targets
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var (pos, vel, isPlayer) = targets[i];
                        PointF newPos = new PointF(
                            Math.Clamp(pos.X + vel.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(pos.Y + vel.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );

                        PointF newVel = vel;
                        if (newPos.X <= rangeBounds.Left + 20 || newPos.X >= rangeBounds.Right - 20)
                            newVel = new PointF(-vel.X, vel.Y);
                        if (newPos.Y <= rangeBounds.Top + 20 || newPos.Y >= rangeBounds.Bottom - 20)
                            newVel = new PointF(vel.X, -vel.Y);

                        targets[i] = (newPos, newVel, isPlayer);
                    }

                    // Get state
                    var playerTarget = targets.Find(t => t.isPlayer);
                    var companions = targets.Where(t => !t.isPlayer).Select((t, idx) => new Companion(t.pos, t.vel, idx, CompanionRole.LeftFlank, t.pos, DateTime.Now)).ToList();

                    float[] state = agent.EncodeState(bossPos, playerTarget.pos, playerTarget.vel, companions, windowSize);

                    // Select action
                    int action = agent.SelectAction(state);
                    List<PointF> bulletVels = agent.ActionToVelocities(action, bossPos, playerTarget.pos, playerTarget.vel);

                    foreach (var vel in bulletVels)
                    {
                        if (bullets.Count < 30)
                            bullets.Add(new EnemyBullet(bossPos, vel));
                    }

                    // Update bullets
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        bullets[i] = bullets[i] with
                        {
                            Position = new PointF(
                                bullets[i].Position.X + bullets[i].Velocity.X,
                                bullets[i].Position.Y + bullets[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)bullets[i].Position.X, (int)bullets[i].Position.Y))
                        {
                            bullets.RemoveAt(i);
                        }
                    }

                    // Check hits
                    float reward = -0.1f;

                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        foreach (var target in targets)
                        {
                            if (Distance(bullets[i].Position, target.pos) < 15f)
                            {
                                reward += target.isPlayer ? 30f : 20f;
                                bullets.RemoveAt(i);
                                break;
                            }
                        }
                    }

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, false);
                    }

                    lastState = state;
                    lastAction = action;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Companion Solo Movement Training: Dodge bullets and avoid enemies when player is dead
        /// Multi-phase training: starts with 1 companion, progressively adds more to teach spacing
        /// </summary>
        public class CompanionSoloMovementTraining
        {
            private CompanionSoloMovementAgent agent;
            private Random random = new Random();
            private int totalEpisodesRun = 0; // Track episode count for multi-phase training

            public CompanionSoloMovementTraining(CompanionSoloMovementAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 500)
            {
                totalEpisodesRun++;

                // Multi-phase training: progressively increase companion count
                int companionCount = 1; // Default
                if (totalEpisodesRun <= 20)
                    companionCount = 1; // Phase 1: Learn basics alone
                else if (totalEpisodesRun <= 40)
                    companionCount = 2; // Phase 2: Learn to avoid one other companion
                else
                    companionCount = random.Next(3, 5); // Phase 3: Full complexity with 3-4 companions

                // Spawn multiple companions
                List<PointF> companionPositions = new List<PointF>();
                for (int c = 0; c < companionCount; c++)
                {
                    PointF companionPos = new PointF(
                        rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                        rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                    );
                    companionPositions.Add(companionPos);
                }

                List<EnemyBullet> bullets = new List<EnemyBullet>();
                List<Enemy> enemies = new List<Enemy>();

                // Spawn 2-4 enemies
                int enemyCount = random.Next(2, 5);
                for (int i = 0; i < enemyCount; i++)
                {
                    PointF enemyPos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    enemies.Add(new Enemy(enemyPos, GameConstants.EnemySpeed, DateTime.Now, EnemyBehavior.Aggressive, DateTime.Now, -1));
                }

                // Track state for each companion separately
                List<float[]> lastStates = new List<float[]>();
                List<int> lastActions = new List<int>();
                List<int> stationaryFrames = new List<int>();
                List<PointF> lastPositions = new List<PointF>();

                for (int c = 0; c < companionCount; c++)
                {
                    lastStates.Add(null!);
                    lastActions.Add(0);
                    stationaryFrames.Add(0);
                    lastPositions.Add(companionPositions[c]);
                }

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Enemies shoot at random companion
                    if (frame % 40 == 0)
                    {
                        foreach (var enemy in enemies)
                        {
                            if (random.NextDouble() < 0.5) // 50% chance each enemy shoots
                            {
                                // Target random companion
                                PointF targetPos = companionPositions[random.Next(companionPositions.Count)];
                                float dx = targetPos.X - enemy.Position.X;
                                float dy = targetPos.Y - enemy.Position.Y;
                                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                if (dist > 0)
                                {
                                    float speed = 5f;
                                    bullets.Add(new EnemyBullet(enemy.Position, new PointF((dx / dist) * speed, (dy / dist) * speed)));
                                }
                            }
                        }
                    }

                    // Update each companion
                    List<PointF> newCompanionPositions = new List<PointF>();
                    for (int c = 0; c < companionCount; c++)
                    {
                        PointF companionPos = companionPositions[c];

                        // Get state
                        float[] state = agent.EncodeState(companionPos, bullets, enemies, new List<Boss>(), windowSize);

                        // Select action
                        int action = agent.SelectAction(state);
                        PointF velocity = agent.ActionToVelocity(action);

                        // Update position
                        PointF newPos = new PointF(
                            Math.Clamp(companionPos.X + velocity.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(companionPos.Y + velocity.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );

                        // Check stationary
                        if (Distance(newPos, lastPositions[c]) < 1f)
                            stationaryFrames[c]++;
                        else
                            stationaryFrames[c] = 0;

                        newCompanionPositions.Add(newPos);

                        // Calculate reward
                        float reward = 0f;
                        bool hit = false;

                        // Check bullet hits
                        for (int i = bullets.Count - 1; i >= 0; i--)
                        {
                            if (Distance(newPos, bullets[i].Position) < 15f)
                            {
                                reward -= 50f;
                                bullets.RemoveAt(i);
                                hit = true;
                                break;
                            }
                        }

                        // Check enemy collision
                        foreach (var enemy in enemies)
                        {
                            if (Distance(newPos, enemy.Position) < 25f)
                            {
                                reward -= 30f;
                                hit = true;
                            }
                        }

                        // Survival and bullet dodging reward
                        if (!hit)
                        {
                            reward += 2f; // Base survival (increased from 1f)

                            // Near-miss bullet dodging bonus
                            if (bullets.Count > 0)
                            {
                                float closestBulletDist = bullets.Min(b => Distance(newPos, b.Position));
                                if (closestBulletDist < 35f && closestBulletDist >= 15f)
                                    reward += 1.2f; // Near-miss dodge bonus
                                else if (closestBulletDist < 50f && closestBulletDist >= 35f)
                                    reward += 0.5f; // Close proximity awareness
                            }
                        }

                        // Penalty for staying still
                        if (stationaryFrames[c] > 10)
                            reward -= 12f;

                        // Bonus for movement (active survival)
                        if (Distance(newPos, lastPositions[c]) > 2f)
                            reward += 3f;

                        // Bonus for maintaining distance from enemies
                        float avgEnemyDist = enemies.Count > 0
                            ? enemies.Average(e => Distance(newPos, e.Position))
                            : 500f;
                        if (avgEnemyDist > 150f && avgEnemyDist < 300f)
                            reward += 3f; // Good distance for survival

                        // CLUSTERING PENALTY: Companions should spread out when player is dead
                        if (companionCount > 1)
                        {
                            // Find distance to nearest companion
                            float minCompanionDist = float.MaxValue;
                            for (int other = 0; other < companionCount; other++)
                            {
                                if (other != c)
                                {
                                    float dist = Distance(newPos, companionPositions[other]);
                                    if (dist < minCompanionDist)
                                        minCompanionDist = dist;
                                }
                            }

                            // Severe penalty for clustering too close
                            if (minCompanionDist < 40f)
                                reward -= 10f;
                            else if (minCompanionDist < 80f)
                                reward -= 5f;
                            else if (minCompanionDist >= 120f && minCompanionDist <= 250f)
                                reward += 2f; // Bonus for good spacing
                        }

                        // Learn
                        if (lastStates[c] != null)
                        {
                            agent.Learn(lastStates[c], lastActions[c], reward, state, hit);
                        }

                        lastStates[c] = state;
                        lastActions[c] = action;
                        lastPositions[c] = newPos;
                    }

                    companionPositions = newCompanionPositions;

                    // Update enemies (simple movement toward nearest companion)
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        PointF enemyPos = enemies[i].Position;

                        // Target nearest companion
                        PointF targetPos = companionPositions[0];
                        float minDist = Distance(enemyPos, targetPos);
                        for (int c = 1; c < companionPositions.Count; c++)
                        {
                            float dist = Distance(enemyPos, companionPositions[c]);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                targetPos = companionPositions[c];
                            }
                        }

                        float dx = targetPos.X - enemyPos.X;
                        float dy = targetPos.Y - enemyPos.Y;
                        float dist2 = (float)Math.Sqrt(dx * dx + dy * dy);

                        PointF enemyVel = PointF.Empty;
                        if (dist2 > 0)
                        {
                            enemyVel = new PointF((dx / dist2) * 2f, (dy / dist2) * 2f);
                        }

                        PointF newEnemyPos = new PointF(
                            Math.Clamp(enemyPos.X + enemyVel.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(enemyPos.Y + enemyVel.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );
                        enemies[i] = enemies[i] with { Position = newEnemyPos };
                    }

                    // Update bullets
                    for (int i = bullets.Count - 1; i >= 0; i--)
                    {
                        bullets[i] = bullets[i] with
                        {
                            Position = new PointF(
                                bullets[i].Position.X + bullets[i].Velocity.X,
                                bullets[i].Position.Y + bullets[i].Velocity.Y
                            )
                        };

                        if (!rangeBounds.Contains((int)bullets[i].Position.X, (int)bullets[i].Position.Y))
                        {
                            bullets.RemoveAt(i);
                        }
                    }
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Player Stealth Movement Training: Sneak past enemies without being detected
        /// </summary>
        public class PlayerStealthMovementTraining
        {
            private PlayerStealthMovementAgent agent;
            private Random random = new Random();

            public PlayerStealthMovementTraining(PlayerStealthMovementAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 1000)
            {
                // Player starts at random position
                PointF playerPos = new PointF(
                    rangeBounds.Left + random.Next(100, rangeBounds.Width - 100),
                    rangeBounds.Top + random.Next(100, rangeBounds.Height - 100)
                );

                // Spawn 3-6 patrolling enemies
                List<Enemy> enemies = new List<Enemy>();
                int enemyCount = random.Next(GameConstants.StealthMinEnemies, GameConstants.StealthMaxEnemies + 1);

                for (int i = 0; i < enemyCount; i++)
                {
                    PointF enemyPos = new PointF(
                        rangeBounds.Left + random.Next(rangeBounds.Width),
                        rangeBounds.Top + random.Next(rangeBounds.Height)
                    );
                    enemies.Add(new Enemy(enemyPos, GameConstants.StealthEnemyPatrolSpeed, DateTime.Now, EnemyBehavior.Patrol, DateTime.Now, -1));
                }

                float[] lastState = null!;
                int lastAction = 0;
                bool detected = false;
                int stationaryFrames = 0;
                PointF lastPos = playerPos;
                float distanceTraveled = 0f;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Get state
                    float[] state = agent.EncodeState(playerPos, enemies, windowSize);

                    // Select action
                    int action = agent.SelectAction(state);
                    PointF velocity = agent.ActionToVelocity(action);

                    // Update position
                    PointF newPos = new PointF(
                        Math.Clamp(playerPos.X + velocity.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                        Math.Clamp(playerPos.Y + velocity.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                    );

                    // Check stationary
                    float moveDist = Distance(newPos, lastPos);
                    if (moveDist < 1f)
                        stationaryFrames++;
                    else
                    {
                        stationaryFrames = 0;
                        distanceTraveled += moveDist;
                    }

                    // Move enemies in patrol patterns
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        PointF enemyPos = enemies[i].Position;

                        // Simple patrol: move in current direction, bounce off walls
                        float angle = (float)(random.NextDouble() * Math.PI * 2);
                        if (frame % 60 == 0) // Change direction every 60 frames
                        {
                            angle = (float)(random.NextDouble() * Math.PI * 2);
                        }

                        PointF enemyVel = new PointF(
                            (float)Math.Cos(angle) * GameConstants.StealthEnemyPatrolSpeed,
                            (float)Math.Sin(angle) * GameConstants.StealthEnemyPatrolSpeed
                        );

                        PointF newEnemyPos = new PointF(
                            Math.Clamp(enemyPos.X + enemyVel.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                            Math.Clamp(enemyPos.Y + enemyVel.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                        );

                        enemies[i] = enemies[i] with { Position = newEnemyPos };
                    }

                    // Calculate reward
                    float reward = 0f;

                    // Check if detected by any enemy
                    detected = false;
                    float minEnemyDist = float.MaxValue;
                    foreach (var enemy in enemies)
                    {
                        float dist = Distance(newPos, enemy.Position);
                        if (dist < minEnemyDist)
                            minEnemyDist = dist;

                        if (dist < GameConstants.EnemyDetectionRange)
                        {
                            detected = true;
                            reward -= 100f; // Huge penalty for being detected
                            break;
                        }
                    }

                    // Reward for staying undetected
                    if (!detected)
                        reward += 2f;

                    // Reward for movement (exploration and evasion)
                    if (moveDist > 2f)
                        reward += 2f;

                    // Penalty for staying still (sitting duck)
                    if (stationaryFrames > 10)
                        reward -= 8f;

                    // Reward for maintaining safe distance from enemies (sweet spot: 50-100px)
                    if (!detected && minEnemyDist >= 50f && minEnemyDist <= 100f)
                        reward += 3f; // Close but safe
                    else if (!detected && minEnemyDist > 100f && minEnemyDist <= 150f)
                        reward += 1f; // Safe distance

                    // Wall proximity penalty
                    float distToLeft = newPos.X - rangeBounds.Left;
                    float distToRight = rangeBounds.Right - newPos.X;
                    float distToTop = newPos.Y - rangeBounds.Top;
                    float distToBottom = rangeBounds.Bottom - newPos.Y;
                    float minWallDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));

                    if (minWallDist < 150f)
                    {
                        float normalizedDist = minWallDist / 150f;
                        float wallPenalty = -10f * (float)Math.Pow(1f - normalizedDist, 2);
                        reward += wallPenalty;
                    }

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, detected);
                    }

                    lastState = state;
                    lastAction = action;
                    lastPos = playerPos;
                    playerPos = newPos;

                    if (detected) break; // Episode ends if detected
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Enemy Patrol Training: Maximize area coverage while patrolling
        /// </summary>
        public class EnemyPatrolTraining
        {
            private EnemyPatrolAgent agent;
            private Random random = new Random();

            public EnemyPatrolTraining(EnemyPatrolAgent agent)
            {
                this.agent = agent;
            }

            public void RunEpisode(Rectangle rangeBounds, Size windowSize, int episodeLength = 800)
            {
                // Enemy starts at random position
                PointF enemyPos = new PointF(
                    rangeBounds.Left + random.Next(rangeBounds.Width),
                    rangeBounds.Top + random.Next(rangeBounds.Height)
                );

                PointF velocity = new PointF(0, 0);

                // Track area coverage with 3x3 grid
                float[,] areaCoverage = new float[3, 3];
                int gridCellWidth = rangeBounds.Width / 3;
                int gridCellHeight = rangeBounds.Height / 3;

                float[] lastState = null!;
                int lastAction = 0;
                float totalCoverage = 0f;

                for (int frame = 0; frame < episodeLength; frame++)
                {
                    // Update coverage map
                    int gridX = Math.Clamp((int)((enemyPos.X - rangeBounds.Left) / gridCellWidth), 0, 2);
                    int gridY = Math.Clamp((int)((enemyPos.Y - rangeBounds.Top) / gridCellHeight), 0, 2);

                    // Decay old coverage
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            areaCoverage[i, j] *= 0.99f;
                        }
                    }

                    // Mark current cell as visited
                    float oldCoverage = areaCoverage[gridX, gridY];
                    areaCoverage[gridX, gridY] = Math.Min(1f, areaCoverage[gridX, gridY] + 0.1f);

                    // Get state
                    float[] state = agent.EncodeState(enemyPos, velocity, areaCoverage, windowSize);

                    // Select action
                    int action = agent.SelectAction(state);
                    velocity = agent.ActionToVelocity(action);

                    // Update position
                    PointF newPos = new PointF(
                        Math.Clamp(enemyPos.X + velocity.X, rangeBounds.Left + 20, rangeBounds.Right - 20),
                        Math.Clamp(enemyPos.Y + velocity.Y, rangeBounds.Top + 20, rangeBounds.Bottom - 20)
                    );

                    // Calculate reward
                    float reward = 0f;

                    // Reward for visiting new/less-visited areas
                    if (oldCoverage < 0.5f)
                        reward += 5f; // Big reward for new areas
                    else if (oldCoverage < 0.8f)
                        reward += 2f; // Medium reward for less-visited
                    else
                        reward -= 3f; // Penalty for over-visiting same area

                    // Calculate coverage diversity (how evenly distributed)
                    float avgCoverage = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            avgCoverage += areaCoverage[i, j];
                        }
                    }
                    avgCoverage /= 9f;
                    totalCoverage = avgCoverage;

                    // Reward for balanced coverage
                    float coverageVariance = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            float diff = areaCoverage[i, j] - avgCoverage;
                            coverageVariance += diff * diff;
                        }
                    }
                    coverageVariance /= 9f;

                    if (coverageVariance < 0.1f && avgCoverage > 0.3f)
                        reward += 3f; // Bonus for even coverage

                    // Small penalty for stopping
                    if (velocity.X == 0 && velocity.Y == 0)
                        reward -= 1f;

                    // Wall proximity penalty (gentle)
                    float distToLeft = newPos.X - rangeBounds.Left;
                    float distToRight = rangeBounds.Right - newPos.X;
                    float distToTop = newPos.Y - rangeBounds.Top;
                    float distToBottom = rangeBounds.Bottom - newPos.Y;
                    float minWallDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));

                    if (minWallDist < 50f)
                        reward -= 2f;

                    // Learn
                    if (lastState != null)
                    {
                        agent.Learn(lastState, lastAction, reward, state, false);
                    }

                    lastState = state;
                    lastAction = action;
                    enemyPos = newPos;
                }
            }

            private float Distance(PointF a, PointF b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }
        }
    }
}
