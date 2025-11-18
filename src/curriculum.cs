using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Curriculum learning system that progressively increases difficulty and complexity.
    /// </summary>
    public class CurriculumLearningSystem
    {
        private TrainingPhase currentPhase = TrainingPhase.BasicMovement;
        private int episodesInCurrentPhase = 0;
        private int totalEpisodes = 0;
        private float currentPerformanceScore = 0f;
        private readonly List<float> recentPerformanceScores = new();
        private readonly Random random = new();

        // Phase transition thresholds
        private readonly Dictionary<TrainingPhase, PhaseConfig> phaseConfigs = new()
        {
            {
                TrainingPhase.BasicMovement,
                new PhaseConfig
                {
                    MinEpisodes = 100,
                    MaxEpisodes = 500,
                    PerformanceThreshold = 0.6f,
                    EnemyCount = 0,
                    EnemySpeed = 0f,
                    BossSpawnRate = 0,
                    HazardCount = 0,
                    Description = "Basic movement and obstacle avoidance"
                }
            },
            {
                TrainingPhase.SlowEnemies,
                new PhaseConfig
                {
                    MinEpisodes = 200,
                    MaxEpisodes = 800,
                    PerformanceThreshold = 0.7f,
                    EnemyCount = 2,
                    EnemySpeed = 2f,
                    BossSpawnRate = 0,
                    HazardCount = 1,
                    Description = "Slow enemies and basic combat"
                }
            },
            {
                TrainingPhase.NormalDifficulty,
                new PhaseConfig
                {
                    MinEpisodes = 500,
                    MaxEpisodes = 2000,
                    PerformanceThreshold = 0.75f,
                    EnemyCount = 4,
                    EnemySpeed = GameConstants.EnemySpeed,
                    BossSpawnRate = 20, // Boss every 20 kills instead of 15
                    HazardCount = 3,
                    Description = "Standard gameplay difficulty"
                }
            },
            {
                TrainingPhase.AdvancedTactics,
                new PhaseConfig
                {
                    MinEpisodes = 1000,
                    MaxEpisodes = 5000,
                    PerformanceThreshold = 0.8f,
                    EnemyCount = 6,
                    EnemySpeed = GameConstants.EnemySpeed * 1.2f,
                    BossSpawnRate = 15,
                    HazardCount = 4,
                    Description = "Advanced tactics and coordination"
                }
            },
            {
                TrainingPhase.MasterLevel,
                new PhaseConfig
                {
                    MinEpisodes = int.MaxValue, // Never graduate from final phase
                    MaxEpisodes = int.MaxValue,
                    PerformanceThreshold = 1.0f,
                    EnemyCount = 8,
                    EnemySpeed = GameConstants.EnemySpeed * 1.5f,
                    BossSpawnRate = 10, // Boss every 10 kills
                    HazardCount = 6,
                    Description = "Master level - maximum difficulty"
                }
            }
        };

        public class PhaseConfig
        {
            public int MinEpisodes { get; set; }
            public int MaxEpisodes { get; set; }
            public float PerformanceThreshold { get; set; }
            public int EnemyCount { get; set; }
            public float EnemySpeed { get; set; }
            public int BossSpawnRate { get; set; }
            public int HazardCount { get; set; }
            public string Description { get; set; } = "";
        }

        /// <summary>
        /// Start a new episode and configure difficulty based on current phase.
        /// </summary>
        public GameDifficultyConfig StartNewEpisode()
        {
            episodesInCurrentPhase++;
            totalEpisodes++;

            var config = phaseConfigs[currentPhase];
            
            Console.WriteLine($"📚 Episode {totalEpisodes} - Phase: {currentPhase} ({episodesInCurrentPhase}/{config.MinEpisodes}+)");
            Console.WriteLine($"   Performance: {currentPerformanceScore:P1} (Threshold: {config.PerformanceThreshold:P1})");

            return new GameDifficultyConfig
            {
                Phase = currentPhase,
                EnemyCount = config.EnemyCount,
                EnemySpeed = config.EnemySpeed,
                BossSpawnInterval = config.BossSpawnRate > 0 ? config.BossSpawnRate : int.MaxValue,
                HazardCount = config.HazardCount,
                CompanionCount = GetCompanionCountForPhase(currentPhase),
                PlayerLearningRate = GetLearningRateForPhase(currentPhase),
                Description = config.Description
            };
        }

        /// <summary>
        /// Record episode performance and check for phase progression.
        /// </summary>
        public void RecordEpisodePerformance(GameStateSnapshot snapshot)
        {
            // Calculate comprehensive performance score
            float performanceScore = CalculatePerformanceScore(snapshot);
            recentPerformanceScores.Add(performanceScore);

            // Keep only recent scores for moving average
            if (recentPerformanceScores.Count > 50)
            {
                recentPerformanceScores.RemoveAt(0);
            }

            currentPerformanceScore = recentPerformanceScores.Average();

            // Check for phase advancement
            CheckPhaseAdvancement();
        }

        /// <summary>
        /// Calculate comprehensive performance score from game snapshot.
        /// </summary>
        private float CalculatePerformanceScore(GameStateSnapshot snapshot)
        {
            float score = 0f;

            // Survival score (40% weight)
            float survivalScore = snapshot.PlayerHealth / 3f; // Normalize to 0-1
            if (snapshot.AliveCompanions > 0)
            {
                survivalScore += (snapshot.AliveCompanions / 3f) * 0.5f; // Companion survival bonus
            }
            score += survivalScore * 0.4f;

            // Combat effectiveness (30% weight)
            float combatScore = 0f;
            if (snapshot.EnemiesDestroyed > 0)
            {
                combatScore = Math.Min(1f, snapshot.EnemiesDestroyed / 10f); // Up to 10 enemies for full score
                combatScore += snapshot.BossesDefeated * 0.3f; // Boss kills are worth more
            }
            score += combatScore * 0.3f;

            // Efficiency metrics (20% weight)
            float efficiencyScore = snapshot.BulletEfficiency * 0.7f + 
                                  snapshot.SpatialCoverage * 0.3f;
            score += efficiencyScore * 0.2f;

            // Perfect run bonus (10% weight)
            if (snapshot.PerfectRun)
            {
                score += 0.1f;
            }

            return Math.Min(1f, score);
        }

        /// <summary>
        /// Check if conditions are met to advance to next phase.
        /// </summary>
        private void CheckPhaseAdvancement()
        {
            var config = phaseConfigs[currentPhase];

            bool hasMinEpisodes = episodesInCurrentPhase >= config.MinEpisodes;
            bool hasGoodPerformance = currentPerformanceScore >= config.PerformanceThreshold;
            bool hasMaxEpisodes = episodesInCurrentPhase >= config.MaxEpisodes;

            if ((hasMinEpisodes && hasGoodPerformance) || hasMaxEpisodes)
            {
                AdvanceToNextPhase();
            }
        }

        /// <summary>
        /// Advance to the next training phase.
        /// </summary>
        private void AdvanceToNextPhase()
        {
            var oldPhase = currentPhase;
            
            switch (currentPhase)
            {
                case TrainingPhase.BasicMovement:
                    currentPhase = TrainingPhase.SlowEnemies;
                    break;
                case TrainingPhase.SlowEnemies:
                    currentPhase = TrainingPhase.NormalDifficulty;
                    break;
                case TrainingPhase.NormalDifficulty:
                    currentPhase = TrainingPhase.AdvancedTactics;
                    break;
                case TrainingPhase.AdvancedTactics:
                    currentPhase = TrainingPhase.MasterLevel;
                    break;
                case TrainingPhase.MasterLevel:
                    // Stay at master level
                    return;
            }

            episodesInCurrentPhase = 0;
            recentPerformanceScores.Clear();
            
            Console.WriteLine($"🎓 PHASE ADVANCEMENT!");
            Console.WriteLine($"   From: {oldPhase} → To: {currentPhase}");
            Console.WriteLine($"   Final Performance: {currentPerformanceScore:P1}");
            Console.WriteLine($"   Description: {phaseConfigs[currentPhase].Description}");
        }

        /// <summary>
        /// Get companion count based on training phase.
        /// </summary>
        private int GetCompanionCountForPhase(TrainingPhase phase)
        {
            return phase switch
            {
                TrainingPhase.BasicMovement => 1, // Start with fewer companions
                TrainingPhase.SlowEnemies => 2,
                TrainingPhase.NormalDifficulty => 3,
                TrainingPhase.AdvancedTactics => 3,
                TrainingPhase.MasterLevel => 3,
                _ => 3
            };
        }

        /// <summary>
        /// Get learning rate based on training phase.
        /// </summary>
        private float GetLearningRateForPhase(TrainingPhase phase)
        {
            return phase switch
            {
                TrainingPhase.BasicMovement => 0.01f, // Higher learning rate for basics
                TrainingPhase.SlowEnemies => 0.008f,
                TrainingPhase.NormalDifficulty => 0.005f,
                TrainingPhase.AdvancedTactics => 0.003f,
                TrainingPhase.MasterLevel => 0.001f, // Lower rate for fine-tuning
                _ => 0.005f
            };
        }

        /// <summary>
        /// Get current training statistics.
        /// </summary>
        public CurriculumStats GetStats()
        {
            return new CurriculumStats
            {
                CurrentPhase = currentPhase,
                EpisodesInPhase = episodesInCurrentPhase,
                TotalEpisodes = totalEpisodes,
                CurrentPerformance = currentPerformanceScore,
                PhaseDescription = phaseConfigs[currentPhase].Description,
                RequiredPerformance = phaseConfigs[currentPhase].PerformanceThreshold,
                MinEpisodesForAdvancement = phaseConfigs[currentPhase].MinEpisodes
            };
        }

        public class GameDifficultyConfig
        {
            public TrainingPhase Phase { get; set; }
            public int EnemyCount { get; set; }
            public float EnemySpeed { get; set; }
            public int BossSpawnInterval { get; set; }
            public int HazardCount { get; set; }
            public int CompanionCount { get; set; }
            public float PlayerLearningRate { get; set; }
            public string Description { get; set; } = "";
        }

        public class CurriculumStats
        {
            public TrainingPhase CurrentPhase { get; set; }
            public int EpisodesInPhase { get; set; }
            public int TotalEpisodes { get; set; }
            public float CurrentPerformance { get; set; }
            public string PhaseDescription { get; set; } = "";
            public float RequiredPerformance { get; set; }
            public int MinEpisodesForAdvancement { get; set; }
        }
    }
}