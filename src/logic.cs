using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    public class GameLogic
    {
        private readonly ObstacleManager obstacleManager;
        private readonly EnemyAI enemyAI;
        private readonly Random random = new();

        public GameLogic(ObstacleManager obstacleManager)
        {
            this.obstacleManager = obstacleManager;
            this.enemyAI = new EnemyAI();
        }

        public Player UpdatePlayerPosition(Player player, Size screenSize)
        {
            var newPos = new PointF(
                player.Position.X + player.Velocity.X,
                player.Position.Y + player.Velocity.Y
            );

            var validPos = CollisionDetector.GetValidPosition(
                player.Position,
                newPos,
                GameConstants.PlayerRadius,
                obstacleManager.Obstacles.ToList(),
                screenSize
            );

            return player with { Position = validPos };
        }

        public List<Laser> UpdateLasers(List<Laser> lasers, Size screenSize)
        {
            var updatedLasers = new List<Laser>();

            foreach (var laser in lasers)
            {
                var newPos = new PointF(
                    laser.Position.X + laser.Velocity.X,
                    laser.Position.Y + laser.Velocity.Y
                );

                // Remove laser if it hits walls or obstacles
                if (newPos.X <= 0 || newPos.X >= screenSize.Width ||
                    newPos.Y <= 0 || newPos.Y >= screenSize.Height ||
                    CollisionDetector.CheckObstacleCollision(newPos, 2f, obstacleManager.Obstacles.ToList()))
                {
                    continue; // Skip this laser (remove it)
                }

                updatedLasers.Add(laser with { Position = newPos });
            }

            return updatedLasers;
        }

        public List<EnemyBullet> UpdateEnemyBullets(List<EnemyBullet> bullets, Size screenSize)
        {
            var updatedBullets = new List<EnemyBullet>();

            foreach (var bullet in bullets)
            {
                var newPos = new PointF(
                    bullet.Position.X + bullet.Velocity.X,
                    bullet.Position.Y + bullet.Velocity.Y
                );

                // Remove bullet if it goes off screen or hits obstacles
                if (newPos.X < 0 || newPos.X > screenSize.Width ||
                    newPos.Y < 0 || newPos.Y > screenSize.Height ||
                    CollisionDetector.CheckObstacleCollision(newPos, 3f, obstacleManager.Obstacles.ToList()))
                {
                    continue; // Skip this bullet (remove it)
                }

                updatedBullets.Add(bullet with { Position = newPos });
            }

            return updatedBullets;
        }

        public List<Enemy> UpdateEnemies(List<Enemy> enemies, Player player, Size screenSize)
        {
            var updatedEnemies = new List<Enemy>();

            foreach (var enemy in enemies)
            {
                // Use the AI system to update enemy behavior and position
                var updatedEnemy = enemyAI.UpdateEnemyBehavior(
                    enemy,
                    player,
                    obstacleManager.Obstacles.ToList(),
                    screenSize
                );

                updatedEnemies.Add(updatedEnemy);
            }

            return updatedEnemies;
        }

        public EnemyBullet? CreateEnemyBullet(Enemy enemy, Player player)
        {
            float dx = player.Position.X - enemy.Position.X;
            float dy = player.Position.Y - enemy.Position.Y;
            float distance = CollisionDetector.Distance(player.Position, enemy.Position);

            if (distance <= GameConstants.EnemyShootRange &&
                DateTime.Now.Subtract(enemy.LastShotTime).TotalMilliseconds >= GameConstants.EnemyShootCooldownMs)
            {
                float bulletDx = dx / distance;
                float bulletDy = dy / distance;
                var bulletVelocity = new PointF(
                    bulletDx * GameConstants.EnemyBulletSpeed,
                    bulletDy * GameConstants.EnemyBulletSpeed
                );

                return new EnemyBullet(enemy.Position, bulletVelocity);
            }

            return null;
        }

        public Enemy? SpawnEnemy(Player player, Size screenSize, EnemyLearningAgent? learningAgent = null)
        {
            var position = CollisionDetector.FindValidEnemyPosition(
                player.Position,
                obstacleManager.Obstacles.ToList(),
                screenSize,
                random
            );

            if (position.HasValue)
            {
                // Randomly assign a behavior to new enemies
                var behaviors = Enum.GetValues<EnemyBehavior>();
                var randomBehavior = behaviors[random.Next(behaviors.Length)];

                // Register enemy with learning agent
                int learningId = learningAgent?.RegisterEnemy() ?? -1;

                return new Enemy(
                    position.Value,
                    GameConstants.EnemySpeed,
                    DateTime.MinValue,
                    randomBehavior,
                    DateTime.Now,
                    learningId
                );
            }
            return null;
        }

        public (List<Laser> lasers, List<Enemy> enemies, int enemiesDestroyed) ProcessLaserEnemyCollisions(
            List<Laser> lasers, List<Enemy> enemies, int currentEnemiesDestroyed)
        {
            var updatedLasers = new List<Laser>(lasers);
            var updatedEnemies = new List<Enemy>(enemies);
            int newEnemiesDestroyed = currentEnemiesDestroyed;

            for (int i = updatedEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = updatedEnemies[i];

                for (int j = updatedLasers.Count - 1; j >= 0; j--)
                {
                    var laser = updatedLasers[j];

                    if (CollisionDetector.CheckLaserEnemyCollision(laser, enemy))
                    {
                        updatedEnemies.RemoveAt(i);
                        updatedLasers.RemoveAt(j);
                        newEnemiesDestroyed++;
                        Console.WriteLine($"🎯 Enemy destroyed! Total: {newEnemiesDestroyed}");
                        break;
                    }
                }
            }

            return (updatedLasers, updatedEnemies, newEnemiesDestroyed);
        }

        public (List<Laser> companionBullets, List<Enemy> enemies, int enemiesDestroyed) ProcessCompanionBulletEnemyCollisions(
            List<Laser> companionBullets, List<Enemy> enemies, int currentEnemiesDestroyed)
        {
            var updatedBullets = new List<Laser>(companionBullets);
            var updatedEnemies = new List<Enemy>(enemies);
            int newEnemiesDestroyed = currentEnemiesDestroyed;

            for (int i = updatedEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = updatedEnemies[i];

                for (int j = updatedBullets.Count - 1; j >= 0; j--)
                {
                    var bullet = updatedBullets[j];

                    if (CollisionDetector.CheckLaserEnemyCollision(bullet, enemy))
                    {
                        updatedEnemies.RemoveAt(i);
                        updatedBullets.RemoveAt(j);
                        newEnemiesDestroyed++;
                        Console.WriteLine($"🤖🎯 Companion destroyed enemy! Total: {newEnemiesDestroyed}");
                        break;
                    }
                }
            }

            return (updatedBullets, updatedEnemies, newEnemiesDestroyed);
        }

        public (List<Laser> lasers, List<EnemyBullet> bullets, int bulletsDestroyed) ProcessLaserBulletCollisions(
            List<Laser> lasers, List<EnemyBullet> bullets, int currentBulletsDestroyed)
        {
            var updatedLasers = new List<Laser>(lasers);
            var updatedBullets = new List<EnemyBullet>(bullets);
            int newBulletsDestroyed = currentBulletsDestroyed;

            for (int i = updatedLasers.Count - 1; i >= 0; i--)
            {
                var laser = updatedLasers[i];

                for (int j = updatedBullets.Count - 1; j >= 0; j--)
                {
                    var bullet = updatedBullets[j];

                    if (CollisionDetector.CheckLaserBulletCollision(laser, bullet))
                    {
                        updatedLasers.RemoveAt(i);
                        updatedBullets.RemoveAt(j);
                        newBulletsDestroyed++;
                        Console.WriteLine($"🛡️ Bullet destroyed defensively! Total: {newBulletsDestroyed}");
                        break;
                    }
                }
            }

            return (updatedLasers, updatedBullets, newBulletsDestroyed);
        }

        public PointF UpdateCompanionPosition(Companion companion, PointF velocity, Size screenSize)
        {
            var newPos = new PointF(
                companion.Position.X + velocity.X,
                companion.Position.Y + velocity.Y
            );

            var validPos = CollisionDetector.GetValidPosition(
                companion.Position,
                newPos,
                GameConstants.CompanionRadius,
                obstacleManager.Obstacles.ToList(),
                screenSize
            );

            return validPos;
        }
    }
}