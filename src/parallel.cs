using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace VoiceGame
{
    /// <summary>
    /// Parallel training environment manager for running multiple game instances simultaneously.
    /// </summary>
    public class ParallelTrainingManager
    {
        private readonly List<TrainingEnvironment> environments = new();
        private readonly ExperienceReplayBuffer replayBuffer = new();
        private readonly AdvancedRewardSystem rewardSystem = new();
        private readonly Random random = new();
        private readonly object lockObject = new();

        public class TrainingEnvironment
        {
            public int Id { get; set; }
            public GameState GameState { get; set; } = new();
            public AdvancedRewardSystem RewardSystem { get; set; } = new();
            public CurriculumLearningSystem CurriculumSystem { get; set; } = new();
            public bool IsActive { get; set; } = true;
            public float AverageReward { get; set; } = 0f;
            public int EpisodeCount { get; set; } = 0;
        }

        public class GameState
        {
            public Player Player { get; set; } = new(PointF.Empty, PointF.Empty);
            public List<Enemy> Enemies { get; set; } = new();
            public List<Boss> Bosses { get; set; } = new();
            public List<Companion> Companions { get; set; } = new();
            public List<Laser> Lasers { get; set; } = new();
            public List<EnemyBullet> Bullets { get; set; } = new();
            public List<Obstacle> Obstacles { get; set; } = new();
            public List<EnvironmentalHazard> Hazards { get; set; } = new();
            public Size ScreenSize { get; set; } = new(800, 600);
            public int EnemiesDestroyed { get; set; } = 0;
            public int BossesDefeated { get; set; } = 0;
            public bool GameOver { get; set; } = false;
        }

        /// <summary>
        /// Initialize parallel training environments.
        /// </summary>
        public void InitializeEnvironments(int environmentCount = 4)
        {
            environments.Clear();
            
            for (int i = 0; i < environmentCount; i++)
            {
                var environment = new TrainingEnvironment
                {
                    Id = i,
                    GameState = CreateInitialGameState(),
                    RewardSystem = new AdvancedRewardSystem(),
                    CurriculumSystem = new CurriculumLearningSystem()
                };
                
                environments.Add(environment);
            }
            
            Console.WriteLine($"🏭 Initialized {environmentCount} parallel training environments");
        }

        /// <summary>
        /// Run parallel training across all environments.
        /// </summary>
        public async Task<ParallelTrainingResults> RunParallelTraining(int totalEpisodes, int maxConcurrency = 4)
        {
            var results = new ParallelTrainingResults();
            var tasks = new List<Task>();
            var semaphore = new System.Threading.SemaphoreSlim(maxConcurrency);

            Console.WriteLine($"🚀 Starting parallel training: {totalEpisodes} episodes across {environments.Count} environments");

            foreach (var environment in environments)
            {
                tasks.Add(RunEnvironmentTraining(environment, totalEpisodes / environments.Count, semaphore, results));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine($"✅ Parallel training complete!");
            Console.WriteLine($"   Total Episodes: {results.TotalEpisodes}");
            Console.WriteLine($"   Average Reward: {results.AverageReward:F3}");
            Console.WriteLine($"   Best Performance: {results.BestPerformance:F3}");
            Console.WriteLine($"   Experience Samples: {replayBuffer.Size}");

            return results;
        }

        /// <summary>
        /// Run training in a single environment.
        /// </summary>
        private async Task RunEnvironmentTraining(TrainingEnvironment environment, int episodeCount, 
            System.Threading.SemaphoreSlim semaphore, ParallelTrainingResults results)
        {
            await semaphore.WaitAsync();
            
            try
            {
                for (int episode = 0; episode < episodeCount; episode++)
                {
                    if (!environment.IsActive) break;

                    var episodeResult = await RunSingleEpisode(environment);
                    
                    // Update results thread-safely
                    lock (lockObject)
                    {
                        results.TotalEpisodes++;
                        results.TotalReward += episodeResult.TotalReward;
                        results.AverageReward = results.TotalReward / results.TotalEpisodes;
                        results.BestPerformance = Math.Max(results.BestPerformance, episodeResult.PerformanceScore);
                        
                        if (episodeResult.PerfectRun)
                        {
                            results.PerfectRuns++;
                        }
                    }

                    // Store experience in replay buffer
                    replayBuffer.AddExperience(episodeResult.Experiences);

                    // Progress reporting
                    if (episode % 100 == 0)
                    {
                        Console.WriteLine($"Environment {environment.Id}: Episode {episode}/{episodeCount} - Reward: {episodeResult.TotalReward:F2}");
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Run a single training episode in an environment.
        /// </summary>
        private async Task<EpisodeResult> RunSingleEpisode(TrainingEnvironment environment)
        {
            environment.RewardSystem.StartNewEpisode();
            var difficultyConfig = environment.CurriculumSystem.StartNewEpisode();
            
            // Reset game state
            ResetGameState(environment.GameState, difficultyConfig);
            
            var experiences = new List<Experience>();
            float totalReward = 0f;
            int stepCount = 0;
            const int maxSteps = 1000;

            while (!environment.GameState.GameOver && stepCount < maxSteps)
            {
                // Simulate game step
                var stepResult = await SimulateGameStep(environment, difficultyConfig);
                
                totalReward += stepResult.Reward;
                experiences.AddRange(stepResult.Experiences);
                stepCount++;

                // Check termination conditions
                if (environment.GameState.Player.Health <= 0 && environment.GameState.Companions.Count == 0)
                {
                    environment.GameState.GameOver = true;
                }
            }

            // Create episode snapshot
            var snapshot = new GameStateSnapshot(
                environment.GameState.Player.Health,
                environment.GameState.EnemiesDestroyed,
                0, // Bullets destroyed - would need tracking
                DateTime.UtcNow,
                environment.GameState.Player.Health == 3 && environment.GameState.Companions.All(c => c.Health == 3),
                environment.GameState.Companions.Count,
                environment.GameState.Companions.Count > 0 && environment.GameState.Player.Health <= 0,
                environment.GameState.BossesDefeated,
                environment.GameState.Bosses.Count > 0
            );

            // Record performance for curriculum learning
            environment.CurriculumSystem.RecordEpisodePerformance(snapshot);

            return new EpisodeResult
            {
                TotalReward = totalReward,
                PerformanceScore = CalculatePerformanceScore(snapshot),
                PerfectRun = snapshot.PerfectRun,
                Experiences = experiences,
                StepCount = stepCount,
                Snapshot = snapshot
            };
        }

        /// <summary>
        /// Simulate a single game step.
        /// </summary>
        private Task<StepResult> SimulateGameStep(TrainingEnvironment environment, 
            CurriculumLearningSystem.GameDifficultyConfig difficultyConfig)
        {
            var gameState = environment.GameState;
            var experiences = new List<Experience>();
            float stepReward = 0f;

            // Update player position (simplified AI decision)
            var playerAction = DecidePlayerAction(gameState);
            var newPlayerPos = ApplyPlayerAction(gameState.Player, playerAction, gameState.ScreenSize);
            gameState.Player = gameState.Player with { Position = newPlayerPos };

            // Calculate player reward
            float playerReward = environment.RewardSystem.CalculatePlayerReward(
                gameState.Player, gameState.Enemies, gameState.Bosses, gameState.Companions, gameState.ScreenSize);
            stepReward += playerReward;

            // Update companions
            for (int i = 0; i < gameState.Companions.Count; i++)
            {
                var companion = gameState.Companions[i];
                var companionAction = DecideCompanionAction(companion, gameState);
                var newCompanionPos = ApplyCompanionAction(companion, companionAction, gameState.ScreenSize);
                gameState.Companions[i] = companion with { Position = newCompanionPos };

                float companionReward = environment.RewardSystem.CalculateCompanionReward(
                    gameState.Companions[i], gameState.Player, gameState.Enemies, gameState.Bosses, 
                    gameState.Companions, gameState.ScreenSize);
                stepReward += companionReward * 0.5f; // Companion rewards worth less
            }

            // Update enemies (simplified)
            UpdateEnemies(gameState, difficultyConfig);

            // Update bosses
            UpdateBosses(gameState);

            // Check collisions and update game state
            ProcessCollisions(gameState);

            // Create experience for replay buffer
            var experience = new Experience
            {
                State = CaptureGameState(gameState),
                Action = playerAction,
                Reward = stepReward,
                NextState = CaptureGameState(gameState), // Would be different after action
                IsTerminal = gameState.GameOver
            };
            experiences.Add(experience);

            return Task.FromResult(new StepResult
            {
                Reward = stepReward,
                Experiences = experiences
            });
        }

        // Simplified helper methods for simulation
        private PlayerAction DecidePlayerAction(GameState gameState)
        {
            // Simplified AI decision - move towards nearest enemy or away from danger
            var nearestEnemy = gameState.Enemies.OrderBy(e => 
                CollisionDetector.Distance(e.Position, gameState.Player.Position)).FirstOrDefault();
            
            if (nearestEnemy != null)
            {
                var distance = CollisionDetector.Distance(nearestEnemy.Position, gameState.Player.Position);
                if (distance < 100f) // Too close, move away
                {
                    return PlayerAction.MoveAway;
                }
                else if (distance > 200f) // Too far, move closer
                {
                    return PlayerAction.MoveToward;
                }
                else // Good distance, shoot
                {
                    return PlayerAction.Shoot;
                }
            }
            
            return PlayerAction.Stay;
        }

        private CompanionAction DecideCompanionAction(Companion companion, GameState gameState)
        {
            // Simplified companion AI - maintain formation and shoot at enemies
            var distanceFromPlayer = CollisionDetector.Distance(companion.Position, gameState.Player.Position);
            
            if (distanceFromPlayer > 80f)
            {
                return CompanionAction.MoveToPlayer;
            }
            else if (gameState.Enemies.Count > 0)
            {
                return CompanionAction.Shoot;
            }
            
            return CompanionAction.Stay;
        }

        private PointF ApplyPlayerAction(Player player, PlayerAction action, Size screenSize)
        {
            const float moveSpeed = 5f;
            var newPos = player.Position;
            
            switch (action)
            {
                case PlayerAction.MoveToward:
                    newPos = new PointF(newPos.X + random.Next(-1, 2) * moveSpeed, 
                                       newPos.Y + random.Next(-1, 2) * moveSpeed);
                    break;
                case PlayerAction.MoveAway:
                    newPos = new PointF(newPos.X + random.Next(-1, 2) * moveSpeed, 
                                       newPos.Y + random.Next(-1, 2) * moveSpeed);
                    break;
            }
            
            // Keep within bounds
            newPos.X = Math.Max(15, Math.Min(screenSize.Width - 15, newPos.X));
            newPos.Y = Math.Max(15, Math.Min(screenSize.Height - 15, newPos.Y));
            
            return newPos;
        }

        private PointF ApplyCompanionAction(Companion companion, CompanionAction action, Size screenSize)
        {
            const float moveSpeed = 4f;
            var newPos = companion.Position;
            
            switch (action)
            {
                case CompanionAction.MoveToPlayer:
                    // Simplified movement towards formation
                    newPos = new PointF(newPos.X + random.Next(-1, 2) * moveSpeed, 
                                       newPos.Y + random.Next(-1, 2) * moveSpeed);
                    break;
            }
            
            // Keep within bounds
            newPos.X = Math.Max(15, Math.Min(screenSize.Width - 15, newPos.X));
            newPos.Y = Math.Max(15, Math.Min(screenSize.Height - 15, newPos.Y));
            
            return newPos;
        }

        private void UpdateEnemies(GameState gameState, CurriculumLearningSystem.GameDifficultyConfig config)
        {
            // Spawn enemies based on difficulty
            if (gameState.Enemies.Count < config.EnemyCount)
            {
                var enemyPos = new PointF(random.Next(50, gameState.ScreenSize.Width - 50),
                                         random.Next(50, gameState.ScreenSize.Height - 50));
                var enemy = new Enemy(enemyPos, config.EnemySpeed, DateTime.MinValue, 
                                    EnemyBehavior.Aggressive, DateTime.UtcNow, gameState.Enemies.Count);
                gameState.Enemies.Add(enemy);
            }
        }

        private void UpdateBosses(GameState gameState)
        {
            // Simple boss spawning logic
            if (gameState.EnemiesDestroyed > 0 && gameState.EnemiesDestroyed % 15 == 0 && gameState.Bosses.Count == 0)
            {
                var bossPos = new PointF(random.Next(50, gameState.ScreenSize.Width - 50),
                                        random.Next(50, gameState.ScreenSize.Height - 50));
                var boss = new Boss(bossPos, 3f, DateTime.MinValue, EnemyBehavior.BossRampage, 
                                   DateTime.UtcNow, 5, 5, DateTime.MinValue);
                gameState.Bosses.Add(boss);
            }
        }

        private void ProcessCollisions(GameState gameState)
        {
            // Simplified collision processing
            // This would include all the collision detection logic from the main game
        }

        private GameState CreateInitialGameState()
        {
            var screenSize = new Size(800, 600);
            var playerPos = new PointF(screenSize.Width / 2, screenSize.Height / 2);
            
            return new GameState
            {
                Player = new Player(playerPos, PointF.Empty, 3),
                ScreenSize = screenSize,
                Companions = new List<Companion>
                {
                    new(new PointF(playerPos.X - 50, playerPos.Y), PointF.Empty, 1, 
                        CompanionRole.LeftFlank, PointF.Empty, DateTime.MinValue, 3),
                    new(new PointF(playerPos.X + 50, playerPos.Y), PointF.Empty, 2, 
                        CompanionRole.RightFlank, PointF.Empty, DateTime.MinValue, 3),
                    new(new PointF(playerPos.X, playerPos.Y - 60), PointF.Empty, 3, 
                        CompanionRole.Rear, PointF.Empty, DateTime.MinValue, 3)
                }
            };
        }

        private void ResetGameState(GameState gameState, CurriculumLearningSystem.GameDifficultyConfig config)
        {
            var playerPos = new PointF(gameState.ScreenSize.Width / 2, gameState.ScreenSize.Height / 2);
            gameState.Player = new Player(playerPos, PointF.Empty, 3);
            gameState.Enemies.Clear();
            gameState.Bosses.Clear();
            gameState.Lasers.Clear();
            gameState.Bullets.Clear();
            gameState.EnemiesDestroyed = 0;
            gameState.BossesDefeated = 0;
            gameState.GameOver = false;
            
            // Reset companions with full health
            for (int i = 0; i < gameState.Companions.Count; i++)
            {
                gameState.Companions[i] = gameState.Companions[i] with { Health = 3 };
            }
        }

        private float CalculatePerformanceScore(GameStateSnapshot snapshot)
        {
            return (snapshot.PlayerHealth / 3f + snapshot.EnemiesDestroyed / 10f + 
                   snapshot.AliveCompanions / 3f) / 3f;
        }

        private float[] CaptureGameState(GameState gameState)
        {
            // Convert game state to neural network input format
            var state = new List<float>
            {
                gameState.Player.Position.X / gameState.ScreenSize.Width,
                gameState.Player.Position.Y / gameState.ScreenSize.Height,
                gameState.Player.Health / 3f,
                gameState.Enemies.Count / 10f,
                gameState.Bosses.Count,
                gameState.Companions.Count / 3f
            };
            
            return state.ToArray();
        }

        // Data structures for actions and results
        public enum PlayerAction { Stay, MoveToward, MoveAway, Shoot }
        public enum CompanionAction { Stay, MoveToPlayer, Shoot }

        public class StepResult
        {
            public float Reward { get; set; }
            public List<Experience> Experiences { get; set; } = new();
        }

        public class EpisodeResult
        {
            public float TotalReward { get; set; }
            public float PerformanceScore { get; set; }
            public bool PerfectRun { get; set; }
            public List<Experience> Experiences { get; set; } = new();
            public int StepCount { get; set; }
            public GameStateSnapshot Snapshot { get; set; } = null!;
        }

        public class ParallelTrainingResults
        {
            public int TotalEpisodes { get; set; }
            public float TotalReward { get; set; }
            public float AverageReward { get; set; }
            public float BestPerformance { get; set; }
            public int PerfectRuns { get; set; }
        }

        public class Experience
        {
            public float[] State { get; set; } = Array.Empty<float>();
            public PlayerAction Action { get; set; }
            public float Reward { get; set; }
            public float[] NextState { get; set; } = Array.Empty<float>();
            public bool IsTerminal { get; set; }
        }
    }
}