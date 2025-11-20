using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    public class EnemyAI
    {
        private readonly Random random = new();
        private const int BehaviorChangeInterval = 3000; // Change behavior every 3 seconds

        public Enemy UpdateEnemyBehavior(Enemy enemy, Player player, List<Obstacle> obstacles, Size screenSize)
        {
            // Check if it's time to change behavior
            var timeSinceLastChange = DateTime.Now.Subtract(enemy.LastBehaviorChange).TotalMilliseconds;
            var shouldChangeBehavior = timeSinceLastChange >= BehaviorChangeInterval;

            var updatedEnemy = enemy;

            if (shouldChangeBehavior)
            {
                var newBehavior = ChooseNewBehavior(enemy, player, obstacles);
                updatedEnemy = enemy with { Behavior = newBehavior, LastBehaviorChange = DateTime.Now };
            }

            // Calculate movement based on current behavior
            var newPosition = CalculateMovement(updatedEnemy, player, obstacles, screenSize);
            return updatedEnemy with { Position = newPosition };
        }

        private EnemyBehavior ChooseNewBehavior(Enemy enemy, Player player, List<Obstacle> obstacles)
        {
            var distance = Distance(enemy.Position, player.Position);
            var hasLineOfSight = HasLineOfSight(enemy.Position, player.Position, obstacles);

            // Choose behavior based on situation
            if (distance < 100f && hasLineOfSight)
            {
                // Close range with line of sight - be cautious or aggressive
                return random.Next(2) == 0 ? EnemyBehavior.Cautious : EnemyBehavior.Aggressive;
            }
            else if (distance > 250f)
            {
                // Far away - approach or flank
                return random.Next(2) == 0 ? EnemyBehavior.Aggressive : EnemyBehavior.Flanking;
            }
            else if (!hasLineOfSight)
            {
                // No line of sight - ambush or flank
                return random.Next(2) == 0 ? EnemyBehavior.Ambush : EnemyBehavior.Flanking;
            }
            else
            {
                // Medium range with line of sight - any behavior is valid
                var behaviors = Enum.GetValues<EnemyBehavior>();
                return behaviors[random.Next(behaviors.Length)];
            }
        }

        private PointF CalculateMovement(Enemy enemy, Player player, List<Obstacle> obstacles, Size screenSize)
        {
            var currentPos = enemy.Position;
            var targetDirection = enemy.Behavior switch
            {
                EnemyBehavior.Aggressive => GetAggressiveDirection(enemy, player),
                EnemyBehavior.Flanking => GetFlankingDirection(enemy, player),
                EnemyBehavior.Cautious => GetCautiousDirection(enemy, player),
                EnemyBehavior.Ambush => GetAmbushDirection(enemy, player, obstacles),
                EnemyBehavior.BossRampage => GetAggressiveDirection(enemy, player), // Boss movement
                _ => PointF.Empty
            };

            // Apply movement with collision avoidance
            var newPos = new PointF(
                currentPos.X + targetDirection.X * enemy.Speed,
                currentPos.Y + targetDirection.Y * enemy.Speed
            );

            // Keep within screen bounds
            newPos.X = Math.Max(GameConstants.EnemyRadius, Math.Min(screenSize.Width - GameConstants.EnemyRadius, newPos.X));
            newPos.Y = Math.Max(GameConstants.EnemyRadius, Math.Min(screenSize.Height - GameConstants.EnemyRadius, newPos.Y));

            // Check obstacle collision and adjust
            return GetValidEnemyPosition(currentPos, newPos, obstacles);
        }

        private PointF GetAggressiveDirection(Enemy enemy, Player player)
        {
            // Move directly toward player
            var dx = player.Position.X - enemy.Position.X;
            var dy = player.Position.Y - enemy.Position.Y;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance == 0) return PointF.Empty;

            return new PointF(dx / distance, dy / distance);
        }

        private PointF GetFlankingDirection(Enemy enemy, Player player)
        {
            // Try to circle around the player
            var dx = player.Position.X - enemy.Position.X;
            var dy = player.Position.Y - enemy.Position.Y;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance == 0) return PointF.Empty;

            // Maintain optimal flanking distance
            var optimalDistance = GameConstants.EnemyFlankingDistance;
            var directionMultiplier = distance < optimalDistance ? -0.5f : 1.0f;

            // Add perpendicular component for circling
            var perpX = -dy / distance;
            var perpY = dx / distance;

            var moveX = (dx / distance) * directionMultiplier + perpX * 0.7f;
            var moveY = (dy / distance) * directionMultiplier + perpY * 0.7f;

            // Normalize the movement vector
            var moveDistance = (float)Math.Sqrt(moveX * moveX + moveY * moveY);
            if (moveDistance == 0) return PointF.Empty;

            return new PointF(moveX / moveDistance, moveY / moveDistance);
        }

        private PointF GetCautiousDirection(Enemy enemy, Player player)
        {
            // Maintain distance while keeping line of sight
            var dx = player.Position.X - enemy.Position.X;
            var dy = player.Position.Y - enemy.Position.Y;
            var distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance == 0) return PointF.Empty;

            var retreatDistance = GameConstants.EnemyRetreatDistance;

            if (distance < retreatDistance)
            {
                // Too close - back away
                return new PointF(-dx / distance, -dy / distance);
            }
            else if (distance > GameConstants.EnemyShootRange)
            {
                // Too far - move closer but slowly
                return new PointF((dx / distance) * 0.3f, (dy / distance) * 0.3f);
            }
            else
            {
                // Perfect distance - maintain position with slight random movement
                var randomX = (random.NextSingle() - 0.5f) * 0.2f;
                var randomY = (random.NextSingle() - 0.5f) * 0.2f;
                return new PointF(randomX, randomY);
            }
        }

        private PointF GetAmbushDirection(Enemy enemy, Player player, List<Obstacle> obstacles)
        {
            // Try to hide behind obstacles and wait for player
            var nearestObstacle = FindNearestObstacle(enemy.Position, obstacles);

            if (nearestObstacle != null)
            {
                // Move toward the obstacle
                var obstacleCenter = new PointF(
                    nearestObstacle.Position.X + nearestObstacle.Size.Width / 2,
                    nearestObstacle.Position.Y + nearestObstacle.Size.Height / 2
                );

                var dx = obstacleCenter.X - enemy.Position.X;
                var dy = obstacleCenter.Y - enemy.Position.Y;
                var distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance > 30f) // Not close enough to obstacle
                {
                    return new PointF(dx / distance, dy / distance);
                }
            }

            // If near obstacle or no obstacles, move slowly toward player
            return GetAggressiveDirection(enemy, player);
        }

        private Obstacle? FindNearestObstacle(PointF position, List<Obstacle> obstacles)
        {
            if (!obstacles.Any()) return null;

            return obstacles.OrderBy(obs =>
            {
                var center = new PointF(
                    obs.Position.X + obs.Size.Width / 2,
                    obs.Position.Y + obs.Size.Height / 2
                );
                return Distance(position, center);
            }).First();
        }

        private bool HasLineOfSight(PointF from, PointF to, List<Obstacle> obstacles)
        {
            // Simple line of sight check - could be improved with ray casting
            foreach (var obstacle in obstacles)
            {
                if (LineIntersectsRectangle(from, to, obstacle))
                    return false;
            }
            return true;
        }

        private bool LineIntersectsRectangle(PointF lineStart, PointF lineEnd, Obstacle rect)
        {
            // Simplified line-rectangle intersection
            var rectLeft = rect.Position.X;
            var rectTop = rect.Position.Y;
            var rectRight = rect.Position.X + rect.Size.Width;
            var rectBottom = rect.Position.Y + rect.Size.Height;

            // Check if either endpoint is inside the rectangle
            if ((lineStart.X >= rectLeft && lineStart.X <= rectRight && lineStart.Y >= rectTop && lineStart.Y <= rectBottom) ||
                (lineEnd.X >= rectLeft && lineEnd.X <= rectRight && lineEnd.Y >= rectTop && lineEnd.Y <= rectBottom))
            {
                return true;
            }

            // For simplicity, assume no intersection if endpoints are outside
            // A more sophisticated implementation would check line-edge intersections
            return false;
        }

        private PointF GetValidEnemyPosition(PointF currentPos, PointF newPos, List<Obstacle> obstacles)
        {
            // Check obstacle collision
            if (!CheckObstacleCollision(newPos, GameConstants.EnemyRadius, obstacles))
                return newPos;

            // Try horizontal movement only
            var horizontalOnly = new PointF(newPos.X, currentPos.Y);
            if (!CheckObstacleCollision(horizontalOnly, GameConstants.EnemyRadius, obstacles))
                return horizontalOnly;

            // Try vertical movement only
            var verticalOnly = new PointF(currentPos.X, newPos.Y);
            if (!CheckObstacleCollision(verticalOnly, GameConstants.EnemyRadius, obstacles))
                return verticalOnly;

            // No valid movement
            return currentPos;
        }

        private bool CheckObstacleCollision(PointF position, float radius, List<Obstacle> obstacles)
        {
            foreach (var obstacle in obstacles)
            {
                if (position.X + radius > obstacle.Position.X &&
                    position.X - radius < obstacle.Position.X + obstacle.Size.Width &&
                    position.Y + radius > obstacle.Position.Y &&
                    position.Y - radius < obstacle.Position.Y + obstacle.Size.Height)
                {
                    return true;
                }
            }
            return false;
        }

        private static float Distance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}