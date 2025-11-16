using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// AI system for managing companion formations and coordination.
    /// </summary>
    public class FormationAI
    {
        private readonly Random random = new();
        private FormationType currentFormation = FormationType.Line;
        private DateTime lastFormationChange = DateTime.UtcNow;
        private readonly PathfindingSystem pathfinding = new();

        /// <summary>
        /// Update companion positions based on formation and current situation.
        /// </summary>
        public List<Companion> UpdateCompanions(
            List<Companion> companions,
            Player player,
            List<Enemy> enemies,
            List<EnemyBullet> enemyBullets,
            List<Obstacle> obstacles,
            int windowWidth,
            int windowHeight)
        {
            // Analyze situation and adapt formation
            var newFormation = AnalyzeSituation(player.Position, enemies, enemyBullets, obstacles);
            if (newFormation != currentFormation && DateTime.UtcNow - lastFormationChange > TimeSpan.FromSeconds(2))
            {
                currentFormation = newFormation;
                lastFormationChange = DateTime.UtcNow;
                Console.WriteLine($"🔄 Formation changed to: {currentFormation}");
            }

            var updatedCompanions = new List<Companion>();

            foreach (var companion in companions)
            {
                // Calculate formation target position with survival AI
                var baseFormationTarget = CalculateFormationPosition(companion, player.Position, currentFormation);
                var survivalTarget = ApplySurvivalAI(companion, baseFormationTarget, enemies, enemyBullets, obstacles);
                
                // Use pathfinding to navigate to survival-adjusted position
                var optimalVelocity = pathfinding.GetOptimalVelocity(
                    companion.Position,
                    survivalTarget,
                    enemies,
                    enemyBullets,
                    obstacles,
                    windowWidth,
                    windowHeight
                );

                // Update companion with new position and target
                var newPosition = new PointF(
                    Math.Max(15, Math.Min(windowWidth - 15, companion.Position.X + optimalVelocity.X)),
                    Math.Max(15, Math.Min(windowHeight - 15, companion.Position.Y + optimalVelocity.Y))
                );

                updatedCompanions.Add(companion with 
                { 
                    Position = newPosition,
                    Velocity = optimalVelocity,
                    FormationTarget = survivalTarget
                });
            }

            return updatedCompanions;
        }

        /// <summary>
        /// Analyzes current battle situation to determine optimal formation.
        /// </summary>
        private FormationType AnalyzeSituation(PointF playerPosition, List<Enemy> enemies, List<EnemyBullet> enemyBullets, List<Obstacle> obstacles)
        {
            int nearbyEnemies = enemies.Count(e => Distance(e.Position, playerPosition) < 150);
            int nearbyBullets = enemyBullets.Count(b => Distance(b.Position, playerPosition) < 100);
            bool inCover = obstacles.Any(o => Distance(playerPosition, new PointF(o.Position.X + o.Size.Width/2, o.Position.Y + o.Size.Height/2)) < 80);

            // Decision logic for formation
            if (nearbyBullets > 3)
                return FormationType.Circle; // Defensive formation under heavy fire
            else if (nearbyEnemies > 4)
                return FormationType.Diamond; // Diamond for heavy combat
            else if (inCover)
                return FormationType.Wedge; // Wedge for advancing from cover
            else if (enemies.Count > 0)
                return FormationType.Line; // Line formation for general combat
            else
                return FormationType.Adaptive; // Let AI decide

            static float Distance(PointF p1, PointF p2)
            {
                return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
            }
        }

        /// <summary>
        /// Calculate the ideal position for a companion in the current formation.
        /// </summary>
        private PointF CalculateFormationPosition(Companion companion, PointF playerPosition, FormationType formation)
        {
            float spacing = 100f; // Increased spacing for looser formation
            float offset = 80f;   // Increased distance from player
            float variation = random.Next(-20, 21); // Add random variation for organic feel

            return formation switch
            {
                FormationType.Line => companion.Role switch
                {
                    CompanionRole.LeftFlank => new PointF(playerPosition.X - spacing + variation, playerPosition.Y + variation * 0.5f),
                    CompanionRole.RightFlank => new PointF(playerPosition.X + spacing + variation, playerPosition.Y + variation * 0.5f),
                    CompanionRole.Rear => new PointF(playerPosition.X + variation * 0.7f, playerPosition.Y + offset + variation),
                    _ => playerPosition
                },

                FormationType.Wedge => companion.Role switch
                {
                    CompanionRole.LeftFlank => new PointF(playerPosition.X - spacing/2 + variation, playerPosition.Y + offset + variation * 0.3f),
                    CompanionRole.RightFlank => new PointF(playerPosition.X + spacing/2 + variation, playerPosition.Y + offset + variation * 0.3f),
                    CompanionRole.Rear => new PointF(playerPosition.X + variation * 0.5f, playerPosition.Y + offset * 1.5f + variation),
                    _ => playerPosition
                },

                FormationType.Diamond => companion.Role switch
                {
                    CompanionRole.LeftFlank => new PointF(playerPosition.X - offset + variation, playerPosition.Y + variation * 0.4f),
                    CompanionRole.RightFlank => new PointF(playerPosition.X + offset + variation, playerPosition.Y + variation * 0.4f),
                    CompanionRole.Rear => new PointF(playerPosition.X + variation * 0.5f, playerPosition.Y + offset + variation),
                    _ => playerPosition
                },

                FormationType.Circle => companion.Role switch
                {
                    CompanionRole.LeftFlank => new PointF(
                        playerPosition.X + (float)(offset * Math.Cos(2 * Math.PI / 3 * companion.Id)),
                        playerPosition.Y + (float)(offset * Math.Sin(2 * Math.PI / 3 * companion.Id))
                    ),
                    CompanionRole.RightFlank => new PointF(
                        playerPosition.X + (float)(offset * Math.Cos(2 * Math.PI / 3 * (companion.Id + 1))),
                        playerPosition.Y + (float)(offset * Math.Sin(2 * Math.PI / 3 * (companion.Id + 1)))
                    ),
                    CompanionRole.Rear => new PointF(
                        playerPosition.X + (float)(offset * Math.Cos(2 * Math.PI / 3 * (companion.Id + 2))),
                        playerPosition.Y + (float)(offset * Math.Sin(2 * Math.PI / 3 * (companion.Id + 2)))
                    ),
                    _ => playerPosition
                },

                FormationType.Adaptive => CalculateAdaptivePosition(companion, playerPosition),
                _ => playerPosition
            };
        }

        /// <summary>
        /// Calculate adaptive formation position based on AI analysis.
        /// </summary>
        private PointF CalculateAdaptivePosition(Companion companion, PointF playerPosition)
        {
            // AI decides best position based on companion role and current situation
            float spacing = 50f;
            float dynamicOffset = 30f + (companion.Id * 15f); // Varying distances

            return companion.Role switch
            {
                CompanionRole.LeftFlank => new PointF(
                    playerPosition.X - spacing - random.Next(-10, 10),
                    playerPosition.Y + random.Next(-20, 20)
                ),
                CompanionRole.RightFlank => new PointF(
                    playerPosition.X + spacing + random.Next(-10, 10),
                    playerPosition.Y + random.Next(-20, 20)
                ),
                CompanionRole.Rear => new PointF(
                    playerPosition.X + random.Next(-15, 15),
                    playerPosition.Y + dynamicOffset + random.Next(-10, 10)
                ),
                _ => playerPosition
            };
        }

        /// <summary>
        /// Determine if companions should shoot based on tactical situation.
        /// </summary>
        public bool ShouldCompanionShoot(Companion companion, List<Enemy> enemies, Player player)
        {
            if (DateTime.UtcNow - companion.LastShotTime < TimeSpan.FromMilliseconds(800))
                return false; // Rate limiting

            // Find nearest enemy
            var nearestEnemy = enemies.OrderBy(e => Distance(e.Position, companion.Position)).FirstOrDefault();
            
            if (nearestEnemy != null)
            {
                float distance = Distance(nearestEnemy.Position, companion.Position);
                float playerDistance = Distance(nearestEnemy.Position, player.Position);

                // Shoot if enemy is close or if companion is closer than player
                return distance < 120f || distance < playerDistance * 1.2f;
            }

            return false;
        }

        /// <summary>
        /// Get current formation type for display.
        /// </summary>
        public FormationType GetCurrentFormation() => currentFormation;

        /// <summary>
        /// Apply survival AI to adjust target position for threat avoidance.
        /// </summary>
        private PointF ApplySurvivalAI(Companion companion, PointF targetPosition, List<Enemy> enemies, List<EnemyBullet> bullets, List<Obstacle> obstacles)
        {
            var adjustedTarget = targetPosition;
            float survivalRadius = 60f; // Radius for threat avoidance
            
            // Avoid nearby enemies
            foreach (var enemy in enemies)
            {
                float distanceToEnemy = Distance(targetPosition, enemy.Position);
                if (distanceToEnemy < survivalRadius)
                {
                    // Push target away from enemy
                    float pushX = (targetPosition.X - enemy.Position.X) / distanceToEnemy * 30f;
                    float pushY = (targetPosition.Y - enemy.Position.Y) / distanceToEnemy * 30f;
                    adjustedTarget = new PointF(adjustedTarget.X + pushX, adjustedTarget.Y + pushY);
                }
            }
            
            // Avoid incoming bullets
            foreach (var bullet in bullets)
            {
                float distanceToBullet = Distance(targetPosition, bullet.Position);
                if (distanceToBullet < 40f)
                {
                    // Push target perpendicular to bullet trajectory
                    float perpX = -bullet.Velocity.Y; // Perpendicular to bullet direction
                    float perpY = bullet.Velocity.X;
                    float perpMagnitude = (float)Math.Sqrt(perpX * perpX + perpY * perpY);
                    if (perpMagnitude > 0)
                    {
                        adjustedTarget = new PointF(
                            adjustedTarget.X + (perpX / perpMagnitude) * 25f,
                            adjustedTarget.Y + (perpY / perpMagnitude) * 25f
                        );
                    }
                }
            }
            
            // Avoid obstacles - prevent companions from phasing through
            foreach (var obstacle in obstacles)
            {
                float distanceToObstacle = Distance(targetPosition, obstacle.Position);
                float avoidanceRadius = Math.Max(obstacle.Size.Width, obstacle.Size.Height) / 2f + 15f; // Use larger dimension as radius plus buffer
                
                if (distanceToObstacle < avoidanceRadius)
                {
                    // Push target away from obstacle center
                    float pushX = (targetPosition.X - obstacle.Position.X) / distanceToObstacle * 35f;
                    float pushY = (targetPosition.Y - obstacle.Position.Y) / distanceToObstacle * 35f;
                    adjustedTarget = new PointF(adjustedTarget.X + pushX, adjustedTarget.Y + pushY);
                }
            }
            
            // Stay within reasonable radius of original target (coordinate-based positioning)
            float maxDeviation = 80f; // Maximum distance from formation target
            float deviationDistance = Distance(adjustedTarget, targetPosition);
            if (deviationDistance > maxDeviation)
            {
                float ratio = maxDeviation / deviationDistance;
                adjustedTarget = new PointF(
                    targetPosition.X + (adjustedTarget.X - targetPosition.X) * ratio,
                    targetPosition.Y + (adjustedTarget.Y - targetPosition.Y) * ratio
                );
            }
            
            return adjustedTarget;
        }

        private static float Distance(PointF p1, PointF p2)
        {
            return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }
}