using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Advanced reward system with proximity rewards, efficiency tracking, and multi-objective scoring.
    /// </summary>
    public class AdvancedRewardSystem
    {
        private readonly Dictionary<int, PointF> lastPositions = new();
        private readonly Dictionary<int, DateTime> lastShotTimes = new();
        private readonly HashSet<PointF> exploredAreas = new();
        private readonly Random random = new();
        
        private int totalShots = 0;
        private int totalHits = 0;
        private int nearMisses = 0;
        private int hazardsAvoided = 0;
        private float totalDistanceFromEdges = 0f;
        private int distanceReadings = 0;
        private DateTime episodeStartTime = DateTime.UtcNow;

        public void StartNewEpisode()
        {
            lastPositions.Clear();
            lastShotTimes.Clear();
            exploredAreas.Clear();
            totalShots = 0;
            totalHits = 0;
            nearMisses = 0;
            hazardsAvoided = 0;
            totalDistanceFromEdges = 0f;
            distanceReadings = 0;
            episodeStartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Calculate comprehensive reward for player actions.
        /// </summary>
        public float CalculatePlayerReward(Player player, List<Enemy> enemies, List<Boss> bosses, 
            List<Companion> companions, Size screenSize, bool shotFired = false, bool hitTarget = false)
        {
            float reward = 0f;
            int playerId = -1; // Player ID

            // Track position for exploration
            UpdateExploredAreas(player.Position);
            
            // Track distance from edges
            float edgeDistance = CalculateEdgeDistance(player.Position, screenSize);
            totalDistanceFromEdges += edgeDistance;
            distanceReadings++;

            // Base survival reward
            reward += 0.1f;

            // Shooting rewards
            if (shotFired)
            {
                totalShots++;
                reward += 0.05f; // Small reward for taking action
                
                if (hitTarget)
                {
                    totalHits++;
                    reward += 1.0f; // Hit reward
                }
                else
                {
                    // Proximity reward for near misses
                    float proximityReward = CalculateProximityReward(player.Position, enemies, bosses);
                    if (proximityReward > 0)
                    {
                        nearMisses++;
                        reward += proximityReward;
                    }
                }
            }

            // Movement and positioning rewards
            reward += CalculateMovementReward(playerId, player.Position);
            reward += CalculatePositioningReward(player.Position, enemies, bosses, screenSize);

            // Team coordination reward
            reward += CalculateTeamworkReward(player.Position, companions);

            // Efficiency bonus
            if (totalShots > 0)
            {
                float efficiency = (float)totalHits / totalShots;
                if (efficiency > 0.7f) // High accuracy bonus
                {
                    reward += efficiency * GameConstants.EfficiencyRewardMultiplier;
                }
            }

            return reward;
        }

        /// <summary>
        /// Calculate reward for companion actions.
        /// </summary>
        public float CalculateCompanionReward(Companion companion, Player player, List<Enemy> enemies, 
            List<Boss> bosses, List<Companion> otherCompanions, Size screenSize)
        {
            float reward = 0f;

            // Base survival reward
            reward += 0.08f;

            // Formation maintenance reward
            float formationDistance = CollisionDetector.Distance(companion.Position, player.Position);
            if (formationDistance <= 70f && formationDistance >= 20f) // Optimal formation distance
            {
                reward += 0.2f;
            }

            // Companion coordination reward
            reward += CalculateCompanionCoordination(companion, otherCompanions);

            // Combat participation reward
            var timeSinceLastShot = DateTime.UtcNow - companion.LastShotTime;
            if (timeSinceLastShot.TotalSeconds < 2.0)
            {
                reward += 0.15f; // Active combat participation
            }

            // Cover and positioning reward
            reward += CalculatePositioningReward(companion.Position, enemies, bosses, screenSize);

            return reward;
        }

        /// <summary>
        /// Calculate proximity reward for near misses.
        /// </summary>
        private float CalculateProximityReward(PointF shooterPos, List<Enemy> enemies, List<Boss> bosses)
        {
            float minDistance = float.MaxValue;
            
            foreach (var enemy in enemies)
            {
                float distance = CollisionDetector.Distance(shooterPos, enemy.Position);
                minDistance = Math.Min(minDistance, distance);
            }
            
            foreach (var boss in bosses)
            {
                float distance = CollisionDetector.Distance(shooterPos, boss.Position);
                minDistance = Math.Min(minDistance, distance);
            }

            if (minDistance <= GameConstants.ProximityRewardRange)
            {
                float proximityFactor = (GameConstants.ProximityRewardRange - minDistance) / GameConstants.ProximityRewardRange;
                return proximityFactor * GameConstants.ProximityRewardMultiplier;
            }

            return 0f;
        }

        /// <summary>
        /// Calculate movement-based rewards (exploration, avoiding stationary behavior).
        /// </summary>
        private float CalculateMovementReward(int entityId, PointF currentPos)
        {
            float reward = 0f;

            if (lastPositions.ContainsKey(entityId))
            {
                float distance = CollisionDetector.Distance(lastPositions[entityId], currentPos);
                
                // Reward for movement (avoid stationary behavior)
                if (distance > 5f)
                {
                    reward += 0.05f;
                }
                
                // Curiosity reward for exploring new areas
                if (!exploredAreas.Any(pos => CollisionDetector.Distance(pos, currentPos) < 30f))
                {
                    reward += GameConstants.CuriosityRewardMultiplier;
                }
            }

            lastPositions[entityId] = currentPos;
            return reward;
        }

        /// <summary>
        /// Calculate positioning rewards (distance from edges, tactical positioning).
        /// </summary>
        private float CalculatePositioningReward(PointF position, List<Enemy> enemies, List<Boss> bosses, Size screenSize)
        {
            float reward = 0f;

            // Reward for staying away from edges
            float edgeDistance = CalculateEdgeDistance(position, screenSize);
            if (edgeDistance > 60f)
            {
                reward += 0.1f;
            }

            // Tactical positioning relative to threats
            foreach (var enemy in enemies)
            {
                float distance = CollisionDetector.Distance(position, enemy.Position);
                if (distance > 50f && distance < 150f) // Optimal engagement range
                {
                    reward += 0.05f;
                }
            }

            return reward;
        }

        /// <summary>
        /// Calculate teamwork rewards for coordinated actions.
        /// </summary>
        private float CalculateTeamworkReward(PointF playerPos, List<Companion> companions)
        {
            if (companions.Count == 0) return 0f;

            float reward = 0f;
            int recentlyFiredCount = 0;

            // Check for coordinated firing
            foreach (var companion in companions)
            {
                var timeSinceShot = DateTime.UtcNow - companion.LastShotTime;
                if (timeSinceShot.TotalSeconds < 1.0)
                {
                    recentlyFiredCount++;
                }
            }

            // Bonus for coordinated attacks
            if (recentlyFiredCount >= 2)
            {
                reward += GameConstants.TeamworkRewardMultiplier * (recentlyFiredCount / 3f);
            }

            // Formation cohesion reward
            float avgDistanceFromPlayer = companions.Average(c => CollisionDetector.Distance(c.Position, playerPos));
            if (avgDistanceFromPlayer <= 70f) // Good formation
            {
                reward += 0.3f;
            }

            return reward;
        }

        /// <summary>
        /// Calculate companion coordination rewards.
        /// </summary>
        private float CalculateCompanionCoordination(Companion companion, List<Companion> others)
        {
            float reward = 0f;

            foreach (var other in others)
            {
                if (other.Id == companion.Id) continue;

                float distance = CollisionDetector.Distance(companion.Position, other.Position);
                
                // Reward for maintaining good spacing
                if (distance >= 30f && distance <= 60f)
                {
                    reward += 0.1f;
                }

                // Reward for synchronized actions
                var timeDiff = Math.Abs((companion.LastShotTime - other.LastShotTime).TotalSeconds);
                if (timeDiff < 0.5) // Shot within 0.5 seconds of each other
                {
                    reward += 0.15f;
                }
            }

            return reward;
        }

        /// <summary>
        /// Update explored areas for curiosity rewards.
        /// </summary>
        private void UpdateExploredAreas(PointF position)
        {
            // Grid-based exploration tracking
            int gridX = (int)(position.X / 50f) * 50;
            int gridY = (int)(position.Y / 50f) * 50;
            exploredAreas.Add(new PointF(gridX, gridY));
        }

        /// <summary>
        /// Calculate distance from screen edges.
        /// </summary>
        private float CalculateEdgeDistance(PointF position, Size screenSize)
        {
            return Math.Min(
                Math.Min(position.X, screenSize.Width - position.X),
                Math.Min(position.Y, screenSize.Height - position.Y)
            );
        }

        /// <summary>
        /// Get comprehensive training metrics.
        /// </summary>
        public GameStateSnapshot CreateEnhancedSnapshot(GameStateSnapshot baseSnapshot)
        {
            float episodeDuration = (float)(DateTime.UtcNow - episodeStartTime).TotalMinutes;
            float damagePerMinute = episodeDuration > 0 ? baseSnapshot.EnemiesDestroyed / episodeDuration : 0f;
            float bulletEfficiency = totalShots > 0 ? (float)totalHits / totalShots : 0f;
            float spatialCoverage = exploredAreas.Count / 100f; // Normalize based on expected exploration
            float avgDistanceFromEdges = distanceReadings > 0 ? totalDistanceFromEdges / distanceReadings : 0f;

            return baseSnapshot with
            {
                DamagePerMinute = damagePerMinute,
                BulletEfficiency = bulletEfficiency,
                SpatialCoverage = Math.Min(spatialCoverage, 1f),
                TeamCoordination = CalculateOverallTeamCoordination(),
                NearMisses = nearMisses,
                CuriosityScore = exploredAreas.Count * GameConstants.CuriosityRewardMultiplier,
                EnvironmentalHazardsAvoided = hazardsAvoided,
                AverageDistanceFromEdges = avgDistanceFromEdges
            };
        }

        private float CalculateOverallTeamCoordination()
        {
            // Simplified team coordination metric
            return Math.Min(1f, (nearMisses + totalHits) / Math.Max(1f, totalShots));
        }

        public void RecordHazardAvoidance()
        {
            hazardsAvoided++;
        }
    }
}