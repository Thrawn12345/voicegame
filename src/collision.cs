using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    public class CollisionDetector
    {
        public static float Distance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static bool CheckPlayerEnemyCollision(Player player, Enemy enemy)
        {
            return Distance(player.Position, enemy.Position) < GameConstants.PlayerRadius + GameConstants.EnemyRadius;
        }

        public static bool CheckLaserEnemyCollision(Laser laser, Enemy enemy)
        {
            return Distance(laser.Position, enemy.Position) < GameConstants.EnemyRadius + 3;
        }

        public static bool CheckLaserBulletCollision(Laser laser, EnemyBullet bullet)
        {
            return Distance(laser.Position, bullet.Position) < 8f;
        }

        public static bool CheckBulletPlayerCollision(EnemyBullet bullet, Player player)
        {
            return Distance(bullet.Position, player.Position) < GameConstants.PlayerRadius + 3;
        }

        public static bool CheckCompanionPlayerCollision(Companion companion, Player player)
        {
            return Distance(companion.Position, player.Position) < GameConstants.PlayerRadius + GameConstants.CompanionRadius;
        }

        public static bool CheckCompanionCompanionCollision(Companion companion1, Companion companion2)
        {
            return Distance(companion1.Position, companion2.Position) < GameConstants.CompanionRadius + GameConstants.CompanionRadius;
        }

        public static bool CheckCompanionEnemyCollision(Companion companion, Enemy enemy)
        {
            return Distance(companion.Position, enemy.Position) < GameConstants.CompanionRadius + GameConstants.EnemyRadius;
        }

        public static bool CheckBulletCompanionCollision(EnemyBullet bullet, Companion companion)
        {
            return Distance(bullet.Position, companion.Position) < GameConstants.CompanionRadius + 3;
        }

        public static bool CheckPlayerBossCollision(Player player, Boss boss)
        {
            return Distance(player.Position, boss.Position) < GameConstants.PlayerRadius + GameConstants.BossRadius;
        }

        public static bool CheckLaserBossCollision(Laser laser, Boss boss)
        {
            return Distance(laser.Position, boss.Position) < GameConstants.BossRadius + 3;
        }

        public static bool CheckCompanionBossCollision(Companion companion, Boss boss)
        {
            return Distance(companion.Position, boss.Position) < GameConstants.CompanionRadius + GameConstants.BossRadius;
        }

        public static bool CheckObstacleCollision(PointF position, float radius, List<Obstacle> obstacles)
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

        public static PointF GetValidPosition(PointF currentPos, PointF newPos, float radius, List<Obstacle> obstacles, Size screenSize)
        {
            // Clamp to screen bounds first
            var clampedPos = new PointF(
                Math.Max(radius, Math.Min(screenSize.Width - radius, newPos.X)),
                Math.Max(radius, Math.Min(screenSize.Height - radius, newPos.Y))
            );

            // Check obstacle collision
            if (!CheckObstacleCollision(clampedPos, radius, obstacles))
                return clampedPos;

            // Try horizontal movement only
            var horizontalOnly = new PointF(clampedPos.X, currentPos.Y);
            if (!CheckObstacleCollision(horizontalOnly, radius, obstacles))
                return horizontalOnly;

            // Try vertical movement only
            var verticalOnly = new PointF(currentPos.X, clampedPos.Y);
            if (!CheckObstacleCollision(verticalOnly, radius, obstacles))
                return verticalOnly;

            // No valid movement
            return currentPos;
        }

        public static bool IsPositionValid(PointF position, float radius, List<Obstacle> obstacles, Size screenSize)
        {
            // Check screen bounds
            if (position.X < radius || position.X > screenSize.Width - radius ||
                position.Y < radius || position.Y > screenSize.Height - radius)
                return false;

            // Check obstacles
            return !CheckObstacleCollision(position, radius, obstacles);
        }

        public static PointF? FindValidEnemyPosition(PointF playerPosition, List<Obstacle> obstacles, Size screenSize, Random random)
        {
            int attempts = 0;
            const int maxAttempts = 100;

            while (attempts < maxAttempts)
            {
                float x = random.Next(GameConstants.EnemyRadius, screenSize.Width - GameConstants.EnemyRadius);
                float y = random.Next(GameConstants.EnemyRadius, screenSize.Height - GameConstants.EnemyRadius);
                var position = new PointF(x, y);

                // Check if far enough from player
                if (Distance(position, playerPosition) < GameConstants.MinEnemySpawnDistance)
                {
                    attempts++;
                    continue;
                }

                // Check if not colliding with obstacles
                if (!CheckObstacleCollision(position, GameConstants.EnemyRadius, obstacles))
                {
                    return position;
                }

                attempts++;
            }

            // If no valid position found, return a fallback position
            return new PointF(screenSize.Width / 4, screenSize.Height / 4);
        }

        public static List<int> GetCollidingLaserIndices(List<Laser> lasers, List<Obstacle> obstacles)
        {
            var collidingIndices = new List<int>();

            for (int i = 0; i < lasers.Count; i++)
            {
                if (CheckObstacleCollision(lasers[i].Position, 2f, obstacles))
                {
                    collidingIndices.Add(i);
                }
            }

            return collidingIndices;
        }

        public static List<int> GetCollidingBulletIndices(List<EnemyBullet> bullets, List<Obstacle> obstacles)
        {
            var collidingIndices = new List<int>();

            for (int i = 0; i < bullets.Count; i++)
            {
                if (CheckObstacleCollision(bullets[i].Position, 3f, obstacles))
                {
                    collidingIndices.Add(i);
                }
            }

            return collidingIndices;
        }

        public static bool CheckCompanionObstacleCollision(Companion companion, List<Obstacle> obstacles)
        {
            return CheckObstacleCollision(companion.Position, GameConstants.CompanionRadius, obstacles);
        }
    }
}