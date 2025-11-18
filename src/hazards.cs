using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Environmental hazard manager for creating dynamic obstacles and challenges.
    /// </summary>
    public class EnvironmentalHazardManager
    {
        private readonly List<EnvironmentalHazard> hazards = new();
        private readonly List<MovingObstacle> movingObstacles = new();
        private readonly Random random = new();

        public IReadOnlyList<EnvironmentalHazard> Hazards => hazards;
        public IReadOnlyList<MovingObstacle> MovingObstacles => movingObstacles;

        /// <summary>
        /// Generate environmental hazards based on difficulty level.
        /// </summary>
        public void GenerateHazards(Size screenSize, PointF playerPosition, int difficultyLevel = 1)
        {
            hazards.Clear();
            movingObstacles.Clear();

            int hazardCount = random.Next(GameConstants.MinHazards, GameConstants.MaxHazards + 1);
            hazardCount = Math.Min(hazardCount + difficultyLevel, GameConstants.MaxHazards * 2);

            Console.WriteLine($"🌩️ Generating {hazardCount} environmental hazards (Difficulty: {difficultyLevel})");

            // Generate static hazards
            for (int i = 0; i < hazardCount / 2; i++)
            {
                CreateStaticHazard(screenSize, playerPosition);
            }

            // Generate moving obstacles
            for (int i = 0; i < Math.Max(1, hazardCount / 3); i++)
            {
                CreateMovingObstacle(screenSize, playerPosition);
            }
        }

        /// <summary>
        /// Create a static environmental hazard.
        /// </summary>
        private void CreateStaticHazard(Size screenSize, PointF playerPosition)
        {
            EnvironmentalHazard hazard;
            int attempts = 0;
            const int maxAttempts = 50;

            do
            {
                var hazardType = (HazardType)random.Next(Enum.GetValues<HazardType>().Length);
                var size = GetHazardSize(hazardType);
                
                var position = new PointF(
                    random.Next((int)size.Width, screenSize.Width - (int)size.Width),
                    random.Next((int)size.Height, screenSize.Height - (int)size.Height)
                );

                hazard = new EnvironmentalHazard(position, size, hazardType, true, DateTime.UtcNow);
                attempts++;

                if (attempts >= maxAttempts) break;

            } while (IsHazardTooCloseToPlayer(hazard, playerPosition) || OverlapsWithExistingHazards(hazard));

            hazards.Add(hazard);
        }

        /// <summary>
        /// Create a moving obstacle.
        /// </summary>
        private void CreateMovingObstacle(Size screenSize, PointF playerPosition)
        {
            var size = new SizeF(40, 40);
            var startPos = new PointF(
                random.Next(50, screenSize.Width - 50),
                random.Next(50, screenSize.Height - 50)
            );

            // Create patrol points
            var patrolPoints = new PointF[]
            {
                startPos,
                new PointF(startPos.X + random.Next(-100, 100), startPos.Y + random.Next(-100, 100)),
                new PointF(startPos.X + random.Next(-150, 150), startPos.Y + random.Next(-150, 150))
            };

            // Ensure patrol points are within bounds
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                patrolPoints[i] = new PointF(
                    Math.Max(size.Width, Math.Min(screenSize.Width - size.Width, patrolPoints[i].X)),
                    Math.Max(size.Height, Math.Min(screenSize.Height - size.Height, patrolPoints[i].Y))
                );
            }

            var velocity = CalculateVelocityToTarget(startPos, patrolPoints[1]);
            var movingObstacle = new MovingObstacle(startPos, size, velocity, patrolPoints, 1);

            movingObstacles.Add(movingObstacle);
        }

        /// <summary>
        /// Update moving obstacles and hazards.
        /// </summary>
        public void Update()
        {
            // Update moving obstacles
            for (int i = 0; i < movingObstacles.Count; i++)
            {
                var obstacle = movingObstacles[i];
                var newPosition = new PointF(
                    obstacle.Position.X + obstacle.Velocity.X,
                    obstacle.Position.Y + obstacle.Velocity.Y
                );

                // Check if reached target patrol point
                var targetPoint = obstacle.PatrolPoints[obstacle.CurrentTarget];
                float distanceToTarget = CollisionDetector.Distance(newPosition, targetPoint);

                if (distanceToTarget < 10f)
                {
                    // Move to next patrol point
                    int nextTarget = (obstacle.CurrentTarget + 1) % obstacle.PatrolPoints.Length;
                    var newVelocity = CalculateVelocityToTarget(newPosition, obstacle.PatrolPoints[nextTarget]);
                    
                    movingObstacles[i] = obstacle with 
                    { 
                        Position = newPosition,
                        Velocity = newVelocity,
                        CurrentTarget = nextTarget
                    };
                }
                else
                {
                    movingObstacles[i] = obstacle with { Position = newPosition };
                }
            }

            // Update hazard activation states
            for (int i = 0; i < hazards.Count; i++)
            {
                var hazard = hazards[i];
                if (hazard.Type == HazardType.ElectricField)
                {
                    // Electric fields activate/deactivate periodically
                    var timeSinceLastActivation = DateTime.UtcNow - hazard.LastActivation;
                    if (timeSinceLastActivation.TotalSeconds > (hazard.IsActive ? 3 : 2))
                    {
                        hazards[i] = hazard with 
                        { 
                            IsActive = !hazard.IsActive,
                            LastActivation = DateTime.UtcNow
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Check if position collides with any hazard.
        /// </summary>
        public bool CheckHazardCollision(PointF position, float radius, out HazardType hazardType)
        {
            hazardType = HazardType.Spikes;

            // Check static hazards
            foreach (var hazard in hazards)
            {
                if (!hazard.IsActive) continue;

                var hazardCenter = new PointF(
                    hazard.Position.X + hazard.Size.Width / 2,
                    hazard.Position.Y + hazard.Size.Height / 2
                );

                var distance = CollisionDetector.Distance(position, hazardCenter);
                var collisionRadius = Math.Max(hazard.Size.Width, hazard.Size.Height) / 2 + radius;

                if (distance < collisionRadius)
                {
                    hazardType = hazard.Type;
                    return true;
                }
            }

            // Check moving obstacles
            foreach (var obstacle in movingObstacles)
            {
                var obstacleCenter = new PointF(
                    obstacle.Position.X + obstacle.Size.Width / 2,
                    obstacle.Position.Y + obstacle.Size.Height / 2
                );

                var distance = CollisionDetector.Distance(position, obstacleCenter);
                var collisionRadius = Math.Max(obstacle.Size.Width, obstacle.Size.Height) / 2 + radius;

                if (distance < collisionRadius)
                {
                    hazardType = HazardType.MovingCrusher;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get damage amount for hazard type.
        /// </summary>
        public float GetHazardDamage(HazardType hazardType)
        {
            return hazardType switch
            {
                HazardType.ElectricField => GameConstants.HazardDamage * 2f, // Higher damage
                HazardType.Lava => GameConstants.HazardDamage * 1.5f, // Continuous damage
                HazardType.Spikes => GameConstants.HazardDamage,
                HazardType.MovingCrusher => GameConstants.HazardDamage * 3f, // Highest damage
                _ => GameConstants.HazardDamage
            };
        }

        /// <summary>
        /// Get all collision areas for pathfinding.
        /// </summary>
        public List<Obstacle> GetHazardObstacles()
        {
            var obstacles = new List<Obstacle>();

            // Add active hazards as obstacles
            foreach (var hazard in hazards.Where(h => h.IsActive))
            {
                obstacles.Add(new Obstacle(hazard.Position, hazard.Size));
            }

            // Add moving obstacles
            foreach (var movingObstacle in movingObstacles)
            {
                obstacles.Add(new Obstacle(movingObstacle.Position, movingObstacle.Size));
            }

            return obstacles;
        }

        private SizeF GetHazardSize(HazardType hazardType)
        {
            return hazardType switch
            {
                HazardType.ElectricField => new SizeF(60, 60),
                HazardType.Lava => new SizeF(80, 80),
                HazardType.Spikes => new SizeF(30, 30),
                HazardType.MovingCrusher => new SizeF(40, 40),
                _ => new SizeF(50, 50)
            };
        }

        private bool IsHazardTooCloseToPlayer(EnvironmentalHazard hazard, PointF playerPosition)
        {
            var hazardCenter = new PointF(
                hazard.Position.X + hazard.Size.Width / 2,
                hazard.Position.Y + hazard.Size.Height / 2
            );
            return CollisionDetector.Distance(hazardCenter, playerPosition) < 100f;
        }

        private bool OverlapsWithExistingHazards(EnvironmentalHazard newHazard)
        {
            foreach (var existing in hazards)
            {
                if (ObstaclesOverlap(
                    new Obstacle(newHazard.Position, newHazard.Size),
                    new Obstacle(existing.Position, existing.Size),
                    10f))
                {
                    return true;
                }
            }
            return false;
        }

        private PointF CalculateVelocityToTarget(PointF from, PointF to)
        {
            var direction = new PointF(to.X - from.X, to.Y - from.Y);
            var distance = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            
            if (distance == 0) return PointF.Empty;
            
            var speed = GameConstants.MovingObstacleSpeed;
            return new PointF(
                (direction.X / distance) * speed,
                (direction.Y / distance) * speed
            );
        }

        private static bool ObstaclesOverlap(Obstacle a, Obstacle b, float buffer)
        {
            return a.Position.X < b.Position.X + b.Size.Width + buffer &&
                   a.Position.X + a.Size.Width + buffer > b.Position.X &&
                   a.Position.Y < b.Position.Y + b.Size.Height + buffer &&
                   a.Position.Y + a.Size.Height + buffer > b.Position.Y;
        }
    }
}