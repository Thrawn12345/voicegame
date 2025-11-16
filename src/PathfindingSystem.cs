using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Intelligent pathfinding system that considers obstacles, bullets, and enemies
    /// for safe navigation to target positions.
    /// </summary>
    public class PathfindingSystem
    {
        private readonly Random random = new();

        /// <summary>
        /// Calculate optimal velocity towards target, considering obstacles and threats.
        /// </summary>
        public PointF GetOptimalVelocity(
            PointF currentPos,
            PointF targetPos,
            List<Enemy> enemies,
            List<EnemyBullet> enemyBullets,
            List<Obstacle> obstacles,
            int windowWidth,
            int windowHeight)
        {
            // Direct path velocity
            float dx = targetPos.X - currentPos.X;
            float dy = targetPos.Y - currentPos.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            // If very close to target, stop moving
            if (distance < 10f)
            {
                return PointF.Empty;
            }

            // Normalize direction
            float dirX = dx / distance;
            float dirY = dy / distance;

            // Calculate threat vectors
            var threatVector = CalculateThreatVector(currentPos, enemies, enemyBullets);
            var obstacleVector = CalculateObstacleAvoidance(currentPos, obstacles, dirX, dirY);

            // Combine vectors with weights
            float avoidanceWeight = 0.7f;
            float directWeight = 0.5f;
            float obstacleWeight = 0.8f;

            float finalX = (dirX * directWeight) + (threatVector.X * avoidanceWeight) + (obstacleVector.X * obstacleWeight);
            float finalY = (dirY * directWeight) + (threatVector.Y * avoidanceWeight) + (obstacleVector.Y * obstacleWeight);

            // Normalize and apply speed
            float finalMagnitude = (float)Math.Sqrt(finalX * finalX + finalY * finalY);
            if (finalMagnitude > 0)
            {
                finalX = (finalX / finalMagnitude) * GameConstants.PlayerSpeed;
                finalY = (finalY / finalMagnitude) * GameConstants.PlayerSpeed;
            }

            // Add some randomness for unpredictability
            float randomFactor = 0.1f;
            finalX += (float)(random.NextDouble() - 0.5) * randomFactor * GameConstants.PlayerSpeed;
            finalY += (float)(random.NextDouble() - 0.5) * randomFactor * GameConstants.PlayerSpeed;

            // Ensure we stay within bounds
            return ClampToBounds(new PointF(finalX, finalY), currentPos, windowWidth, windowHeight);
        }

        /// <summary>
        /// Calculate avoidance vector for bullets and enemies.
        /// </summary>
        private PointF CalculateThreatVector(PointF currentPos, List<Enemy> enemies, List<EnemyBullet> enemyBullets)
        {
            float avoidX = 0f;
            float avoidY = 0f;

            // Avoid enemy bullets
            foreach (var bullet in enemyBullets)
            {
                float dx = currentPos.X - bullet.Position.X;
                float dy = currentPos.Y - bullet.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance < 80f && distance > 0) // Threat range
                {
                    // Predict bullet path
                    float bulletSpeed = (float)Math.Sqrt(bullet.Velocity.X * bullet.Velocity.X + bullet.Velocity.Y * bullet.Velocity.Y);
                    if (bulletSpeed > 0)
                    {
                        float timeToReach = distance / bulletSpeed;
                        PointF predictedBulletPos = new PointF(
                            bullet.Position.X + bullet.Velocity.X * timeToReach,
                            bullet.Position.Y + bullet.Velocity.Y * timeToReach
                        );

                        float predictedDx = currentPos.X - predictedBulletPos.X;
                        float predictedDy = currentPos.Y - predictedBulletPos.Y;
                        float predictedDistance = (float)Math.Sqrt(predictedDx * predictedDx + predictedDy * predictedDy);

                        if (predictedDistance < 30f) // Will be close
                        {
                            float avoidanceStrength = 1f / Math.Max(distance, 1f);
                            avoidX += (dx / distance) * avoidanceStrength;
                            avoidY += (dy / distance) * avoidanceStrength;
                        }
                    }
                }
            }

            // Avoid enemies
            foreach (var enemy in enemies)
            {
                float dx = currentPos.X - enemy.Position.X;
                float dy = currentPos.Y - enemy.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance < 60f && distance > 0) // Enemy avoidance range
                {
                    float avoidanceStrength = 0.5f / Math.Max(distance, 1f);
                    avoidX += (dx / distance) * avoidanceStrength;
                    avoidY += (dy / distance) * avoidanceStrength;
                }
            }

            return new PointF(avoidX, avoidY);
        }

        /// <summary>
        /// Calculate obstacle avoidance vector with improved pathfinding.
        /// </summary>
        private PointF CalculateObstacleAvoidance(PointF currentPos, List<Obstacle> obstacles, float dirX, float dirY)
        {
            float avoidX = 0f;
            float avoidY = 0f;

            foreach (var obstacle in obstacles)
            {
                // Check if obstacle is in our path
                float obstacleX = obstacle.Position.X + obstacle.Size.Width / 2;
                float obstacleY = obstacle.Position.Y + obstacle.Size.Height / 2;

                float dx = currentPos.X - obstacleX;
                float dy = currentPos.Y - obstacleY;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                float avoidanceRange = Math.Max(obstacle.Size.Width, obstacle.Size.Height) + 40f;

                if (distance < avoidanceRange && distance > 0)
                {
                    // Calculate if obstacle is in our intended direction
                    float dotProduct = (dx * dirX + dy * dirY) / distance;

                    if (dotProduct < 0) // Obstacle is ahead of us
                    {
                        // Enhanced avoidance - find the best direction around obstacle
                        var bestAvoidDir = FindBestAvoidanceDirection(currentPos, obstacle, dirX, dirY, obstacles);
                        
                        float avoidanceStrength = 1.5f / Math.Max(distance, 1f);
                        avoidX += bestAvoidDir.X * avoidanceStrength;
                        avoidY += bestAvoidDir.Y * avoidanceStrength;
                    }
                }
            }

            return new PointF(avoidX, avoidY);
        }

        /// <summary>
        /// Find the best direction to go around an obstacle.
        /// </summary>
        private PointF FindBestAvoidanceDirection(PointF currentPos, Obstacle obstacle, float dirX, float dirY, List<Obstacle> allObstacles)
        {
            // Calculate obstacle bounds
            float obstacleLeft = obstacle.Position.X;
            float obstacleRight = obstacle.Position.X + obstacle.Size.Width;
            float obstacleTop = obstacle.Position.Y;
            float obstacleBottom = obstacle.Position.Y + obstacle.Size.Height;

            // Try going around left or right
            PointF leftDirection = new PointF(-dirY, dirX);   // Perpendicular left
            PointF rightDirection = new PointF(dirY, -dirX);  // Perpendicular right

            // Check which direction has fewer obstacles
            float leftClearance = CheckDirectionClearance(currentPos, leftDirection, allObstacles, 60f);
            float rightClearance = CheckDirectionClearance(currentPos, rightDirection, allObstacles, 60f);

            // Choose the direction with more clearance
            if (leftClearance > rightClearance)
            {
                return leftDirection;
            }
            else if (rightClearance > leftClearance)
            {
                return rightDirection;
            }
            else
            {
                // If equal clearance, choose based on which side of obstacle we're closer to
                float obstacleX = obstacle.Position.X + obstacle.Size.Width / 2;
                float obstacleY = obstacle.Position.Y + obstacle.Size.Height / 2;
                
                if (currentPos.X < obstacleX)
                {
                    return leftDirection; // Go left if we're on the left side
                }
                else
                {
                    return rightDirection; // Go right if we're on the right side
                }
            }
        }

        /// <summary>
        /// Check how clear a direction is of obstacles.
        /// </summary>
        private float CheckDirectionClearance(PointF startPos, PointF direction, List<Obstacle> obstacles, float checkDistance)
        {
            float clearance = checkDistance;
            
            // Normalize direction
            float dirMagnitude = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            if (dirMagnitude == 0) return 0f;
            
            float normalizedDirX = direction.X / dirMagnitude;
            float normalizedDirY = direction.Y / dirMagnitude;

            // Check multiple points along the direction
            for (float dist = 10f; dist <= checkDistance; dist += 10f)
            {
                PointF checkPoint = new PointF(
                    startPos.X + normalizedDirX * dist,
                    startPos.Y + normalizedDirY * dist
                );

                // Check if this point intersects any obstacle
                foreach (var obstacle in obstacles)
                {
                    if (checkPoint.X >= obstacle.Position.X && 
                        checkPoint.X <= obstacle.Position.X + obstacle.Size.Width &&
                        checkPoint.Y >= obstacle.Position.Y && 
                        checkPoint.Y <= obstacle.Position.Y + obstacle.Size.Height)
                    {
                        clearance = Math.Min(clearance, dist - 10f);
                        break;
                    }
                }
            }

            return clearance;
        }

        /// <summary>
        /// Ensure velocity doesn't move player out of bounds.
        /// </summary>
        private PointF ClampToBounds(PointF velocity, PointF currentPos, int windowWidth, int windowHeight)
        {
            float newX = currentPos.X + velocity.X;
            float newY = currentPos.Y + velocity.Y;

            float clampedVelX = velocity.X;
            float clampedVelY = velocity.Y;

            if (newX < GameConstants.PlayerRadius)
                clampedVelX = Math.Max(0, clampedVelX);
            else if (newX > windowWidth - GameConstants.PlayerRadius)
                clampedVelX = Math.Min(0, clampedVelX);

            if (newY < GameConstants.PlayerRadius)
                clampedVelY = Math.Max(0, clampedVelY);
            else if (newY > windowHeight - GameConstants.PlayerRadius)
                clampedVelY = Math.Min(0, clampedVelY);

            return new PointF(clampedVelX, clampedVelY);
        }

        /// <summary>
        /// Get state features for AI learning related to pathfinding.
        /// </summary>
        public float[] GetPathfindingFeatures(
            PointF currentPos,
            PointF? targetPos,
            List<Enemy> enemies,
            List<EnemyBullet> enemyBullets,
            List<Obstacle> obstacles,
            int windowWidth,
            int windowHeight)
        {
            var features = new List<float>();

            if (targetPos.HasValue)
            {
                // Target relative position (2 features)
                float dx = targetPos.Value.X - currentPos.X;
                float dy = targetPos.Value.Y - currentPos.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                features.Add(dx / windowWidth);  // Normalized target X offset
                features.Add(dy / windowHeight); // Normalized target Y offset
                features.Add(Math.Min(distance / 200f, 1f)); // Normalized distance to target

                // Path threat level (1 feature)
                float pathThreat = CalculatePathThreatLevel(currentPos, targetPos.Value, enemies, enemyBullets);
                features.Add(pathThreat);
            }
            else
            {
                // No target set
                features.Add(0f); // No target X
                features.Add(0f); // No target Y  
                features.Add(0f); // No distance
                features.Add(0f); // No path threat
            }

            return features.ToArray();
        }

        /// <summary>
        /// Calculate how dangerous the direct path to target is.
        /// </summary>
        private float CalculatePathThreatLevel(PointF start, PointF target, List<Enemy> enemies, List<EnemyBullet> bullets)
        {
            float threatLevel = 0f;
            float pathLength = (float)Math.Sqrt(Math.Pow(target.X - start.X, 2) + Math.Pow(target.Y - start.Y, 2));

            if (pathLength == 0) return 0f;

            // Check bullets intersecting path
            foreach (var bullet in bullets)
            {
                float distanceToPath = DistancePointToLineSegment(bullet.Position, start, target);
                if (distanceToPath < 25f) // Bullet close to path
                {
                    threatLevel += 0.3f;
                }
            }

            // Check enemies near path
            foreach (var enemy in enemies)
            {
                float distanceToPath = DistancePointToLineSegment(enemy.Position, start, target);
                if (distanceToPath < 40f) // Enemy close to path
                {
                    threatLevel += 0.2f;
                }
            }

            return Math.Min(threatLevel, 1f);
        }

        /// <summary>
        /// Calculate distance from point to line segment.
        /// </summary>
        private float DistancePointToLineSegment(PointF point, PointF lineStart, PointF lineEnd)
        {
            float A = point.X - lineStart.X;
            float B = point.Y - lineStart.Y;
            float C = lineEnd.X - lineStart.X;
            float D = lineEnd.Y - lineStart.Y;

            float dot = A * C + B * D;
            float lenSq = C * C + D * D;
            float param = (lenSq != 0) ? dot / lenSq : -1;

            float xx, yy;

            if (param < 0)
            {
                xx = lineStart.X;
                yy = lineStart.Y;
            }
            else if (param > 1)
            {
                xx = lineEnd.X;
                yy = lineEnd.Y;
            }
            else
            {
                xx = lineStart.X + param * C;
                yy = lineStart.Y + param * D;
            }

            float dx = point.X - xx;
            float dy = point.Y - yy;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}