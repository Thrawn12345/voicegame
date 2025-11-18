using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace VoiceGame
{
    /// <summary>
    /// Automated headless data collector for long-duration training sessions.
    /// Runs simulated game episodes and collects data without GUI.
    /// </summary>
    public class AutomatedDataCollector
    {
        private DataCollector collector;
        private TrainingDataManager dataManager;
        private Random random;
        private int totalEpisodesCollected = 0;
        private int totalExperiencesCollected = 0;
        private DateTime sessionStart;
        private TimeSpan targetDuration;
        private List<Companion>? companions;

        // Simulation parameters
        private const int SIMULATION_WIDTH = 800;
        private const int SIMULATION_HEIGHT = 600;
        private const int MAX_STEPS_PER_EPISODE = 500;
        private const int MIN_STEPS_PER_EPISODE = 50;

        public AutomatedDataCollector(TimeSpan targetDuration = default)
        {
            collector = new DataCollector();
            dataManager = new TrainingDataManager();
            random = new Random();
            
            // Default to 11 hours if not specified
            targetDuration = targetDuration == default 
                ? TimeSpan.FromHours(11) 
                : targetDuration;
            
            this.targetDuration = targetDuration;
            this.sessionStart = DateTime.UtcNow;
        }

        /// <summary>
        /// Runs automated data collection for the specified duration.
        /// </summary>
        public void Run()
        {
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║   AUTOMATED DATA COLLECTION SESSION        ║");
            Console.WriteLine("╚════════════════════════════════════════════╝\n");

            Console.WriteLine($"🎬 Target Duration:     {targetDuration.TotalHours:F1} hours");
            Console.WriteLine($"📅 Session Start:       {sessionStart:G}");
            Console.WriteLine($"⏰ Estimated End:       {sessionStart.Add(targetDuration):G}\n");

            Console.WriteLine("Status: RUNNING");
            Console.WriteLine("Press Ctrl+C to stop early.\n");

            int episodeCounter = 0;

            try
            {
                while (DateTime.UtcNow - sessionStart < targetDuration)
                {
                    episodeCounter++;

                    // Run one simulated episode
                    int experiencesInEpisode = SimulateEpisode();
                    totalExperiencesCollected += experiencesInEpisode;

                    var (episode, episodeNum, totalReward) = collector.EndEpisode();
                    dataManager.SaveEpisode(episodeNum, episode, totalReward);
                    totalEpisodesCollected++;

                    // Print progress every 10 episodes
                    if (episodeCounter % 10 == 0)
                    {
                        PrintProgress();
                    }

                    // Small delay between episodes to prevent CPU saturation
                    Thread.Sleep(10);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n\n⏹️  Collection stopped by user.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n\n❌ Error during collection: {ex.Message}\n");
            }

            PrintSessionSummary();
        }

        /// <summary>
        /// Simulates one complete game episode and records experiences.
        /// </summary>
        private int SimulateEpisode()
        {
            int experiencesInEpisode = 0;
            int stepsInEpisode = random.Next(MIN_STEPS_PER_EPISODE, MAX_STEPS_PER_EPISODE);

            // Initialize companions for this episode (2-3 companions)
            companions = new List<Companion>();
            int companionCount = random.Next(2, 4);
            for (int i = 0; i < companionCount; i++)
            {
                var companionPos = new System.Drawing.PointF(
                    random.Next(50, SIMULATION_WIDTH - 50),
                    random.Next(50, SIMULATION_HEIGHT - 50)
                );
                var companionVel = new System.Drawing.PointF(0, 0);
                var companionRole = (CompanionRole)(i % 3); // Cycle through roles
                var formationTarget = companionPos;
                companions.Add(new Companion(companionPos, companionVel, i, companionRole, formationTarget, DateTime.UtcNow));
            }

            // Simulate random game state
            var playerPos = new System.Drawing.PointF(
                random.Next(100, SIMULATION_WIDTH - 100),
                random.Next(100, SIMULATION_HEIGHT - 100)
            );
            var playerVel = new System.Drawing.PointF(0, 0);
            int lives = 3;

            for (int step = 0; step < stepsInEpisode; step++)
            {
                // Randomly generate game objects
                var enemies = GenerateRandomEnemies(3, 5);
                var lasers = GenerateRandomLasers(1, 4);
                bool gameOver = lives <= 0;

                // Encode current state with companion information
                float[] state = collector.EncodeGameState(
                    playerPos, playerVel,
                    enemies,
                    lasers,
                    lives, gameOver,
                    SIMULATION_WIDTH, SIMULATION_HEIGHT,
                    null, // targetPos
                    companions,
                    FormationType.Line, // Default formation
                    CalculateFormationThreatLevel(enemies),
                    IsCompanionFireSupportActive()
                );

                // Choose random action (with slight bias toward movement)
                int action = random.Next(100) < 80 
                    ? random.Next(9)  // 80% chance of movement
                    : random.Next(8);  // 20% chance of stop

                // Calculate reward
                float reward = CalculateReward(action, enemies.Count, step, stepsInEpisode);

                // Update simulated state
                playerPos = UpdatePlayerPosition(playerPos, action);
                lives = random.Next(100) < 5 ? lives - 1 : lives;  // 5% chance of hit
                
                // Update companion states (simulate their behavior)
                UpdateCompanionsSimulation(playerPos, enemies);

                // Encode next state with companion information
                float[] nextState = collector.EncodeGameState(
                    playerPos, playerVel,
                    enemies,
                    lasers,
                    lives, gameOver || lives <= 0,
                    SIMULATION_WIDTH, SIMULATION_HEIGHT,
                    null, // targetPos
                    companions,
                    FormationType.Line, // Default formation
                    CalculateFormationThreatLevel(enemies),
                    IsCompanionFireSupportActive()
                );

                // Record experience with simulated confidence
                float confidence = (float)(0.7 + random.NextDouble() * 0.3);  // 0.7-1.0 confidence
                collector.RecordExperience(state, action, reward, nextState, gameOver, confidence);

                experiencesInEpisode++;

                // Stop if game over
                if (gameOver || lives <= 0) break;
            }

            return experiencesInEpisode;
        }

        private List<(System.Drawing.PointF pos, float radius)> GenerateRandomEnemies(int minCount, int maxCount)
        {
            var enemies = new List<(System.Drawing.PointF, float)>();
            int count = random.Next(minCount, maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                var pos = new System.Drawing.PointF(
                    random.Next(0, SIMULATION_WIDTH),
                    random.Next(0, SIMULATION_HEIGHT)
                );
                float radius = 12f;
                enemies.Add((pos, radius));
            }

            return enemies;
        }

        private List<(System.Drawing.PointF pos, System.Drawing.PointF vel)> GenerateRandomLasers(int minCount, int maxCount)
        {
            var lasers = new List<(System.Drawing.PointF, System.Drawing.PointF)>();
            int count = random.Next(minCount, maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                var pos = new System.Drawing.PointF(
                    random.Next(0, SIMULATION_WIDTH),
                    random.Next(0, SIMULATION_HEIGHT)
                );
                var vel = new System.Drawing.PointF(
                    (float)(random.NextDouble() - 0.5) * 8,
                    (float)(random.NextDouble() - 0.5) * 8
                );
                lasers.Add((pos, vel));
            }

            return lasers;
        }

        private System.Drawing.PointF UpdatePlayerPosition(System.Drawing.PointF pos, int action)
        {
            int speed = 5;
            var newPos = pos;

            switch (action)
            {
                case 0: newPos.Y -= speed; break;    // NORTH
                case 1: newPos.Y += speed; break;    // SOUTH
                case 2: newPos.X += speed; break;    // EAST
                case 3: newPos.X -= speed; break;    // WEST
                case 4: newPos.X += speed; newPos.Y -= speed; break;  // NORTHEAST
                case 5: newPos.X -= speed; newPos.Y -= speed; break;  // NORTHWEST
                case 6: newPos.X += speed; newPos.Y += speed; break;  // SOUTHEAST
                case 7: newPos.X -= speed; newPos.Y += speed; break;  // SOUTHWEST
                case 8: break;  // STOP - no movement
            }

            // Clamp to bounds
            if (newPos.X < 0) newPos.X = 0;
            if (newPos.X > SIMULATION_WIDTH) newPos.X = SIMULATION_WIDTH;
            if (newPos.Y < 0) newPos.Y = 0;
            if (newPos.Y > SIMULATION_HEIGHT) newPos.Y = SIMULATION_HEIGHT;

            return newPos;
        }

        private float CalculateReward(int action, int enemyCount, int step, int totalSteps)
        {
            float reward = 0f;

            // Enhanced survival reward for companion coordination
            reward += 0.15f;

            // Enemy proximity penalty with companion protection bonus
            if (enemyCount > 0)
            {
                float baseProximityPenalty = 0.05f * enemyCount;
                // Reduced penalty if we have companions (they provide protection)
                float companionProtectionFactor = Math.Max(0.3f, 1.0f - (companions?.Count ?? 0) * 0.2f);
                reward -= baseProximityPenalty * companionProtectionFactor;
            }

            // Companion coordination rewards
            if (companions?.Count > 0)
            {
                // Reward for maintaining companion formations
                reward += 0.08f * companions.Count;
                
                // Bonus for companion fire support activity
                int activelyShooting = 0;
                foreach (var companion in companions)
                {
                    var timeSinceShot = DateTime.UtcNow - companion.LastShotTime;
                    if (timeSinceShot.TotalSeconds < 3.0) activelyShooting++;
                }
                reward += 0.05f * activelyShooting;
            }

            // Bonus for reaching end of episode (higher with more companions alive)
            if (step == totalSteps - 1)
            {
                float survivalBonus = 1f + (companions?.Count ?? 0) * 0.3f;
                reward += survivalBonus;
            }

            // Strategic movement rewards - reduced penalty for STOP if near threats
            if (action == 8)
            {
                // Less penalty for stopping if many enemies nearby (defensive position)
                float stopPenalty = enemyCount > 3 ? 0.01f : 0.03f;
                reward -= stopPenalty;
            }

            // Target-based movement bonuses
            if (action >= 9 && action <= 11) // TARGET_DIRECT, TARGET_SAFE, TARGET_DODGE
            {
                reward += 0.02f; // Small bonus for using advanced movement
            }

            return reward;
        }

        private float CalculateFormationThreatLevel(List<(System.Drawing.PointF pos, float radius)> enemies)
        {
            if (enemies.Count == 0) return 0f;
            
            // Simple threat assessment based on enemy density
            float threatLevel = Math.Min(enemies.Count / 10f, 1f);
            return threatLevel;
        }

        private bool IsCompanionFireSupportActive()
        {
            if (companions?.Count == 0) return false;
            
            // Simulate companion fire support activity
            // In training, we randomly activate fire support to explore different scenarios
            return random.Next(100) < 40; // 40% chance of active fire support
        }

        private void UpdateCompanionsSimulation(System.Drawing.PointF playerPos, List<(System.Drawing.PointF pos, float radius)> enemies)
        {
            if (companions == null) return;

            // Remove companions occasionally to simulate losses
            if (companions.Count > 1 && random.Next(1000) < 2) // 0.2% chance per step
            {
                companions.RemoveAt(random.Next(companions.Count));
            }

            // Update companion positions and shooting times
            for (int i = 0; i < companions.Count; i++)
            {
                var companion = companions[i];
                
                // Move companions toward formation position near player
                var targetPos = new System.Drawing.PointF(
                    playerPos.X + random.Next(-80, 81),
                    playerPos.Y + random.Next(-80, 81)
                );
                
                // Simulate movement toward formation
                var newPosition = new System.Drawing.PointF(
                    companion.Position.X + (targetPos.X - companion.Position.X) * 0.1f,
                    companion.Position.Y + (targetPos.Y - companion.Position.Y) * 0.1f
                );

                // Create updated companion with new position and possibly new shot time
                var newLastShotTime = companion.LastShotTime;
                if (enemies.Count > 0 && random.Next(100) < 15) // 15% chance to shoot each step
                {
                    newLastShotTime = DateTime.UtcNow;
                }

                companions[i] = companion with { Position = newPosition, LastShotTime = newLastShotTime };
            }
        }

        private void PrintProgress()
        {
            TimeSpan elapsed = DateTime.UtcNow - sessionStart;
            TimeSpan remaining = targetDuration - elapsed;
            double percentComplete = elapsed.TotalSeconds / targetDuration.TotalSeconds * 100;

            int barLength = 30;
            int filledLength = (int)(barLength * percentComplete / 100);
            string progressBar = new string('█', filledLength) + new string('░', barLength - filledLength);

            Console.Write($"\r[{progressBar}] {percentComplete:F1}% | ");
            Console.Write($"Episodes: {totalEpisodesCollected} | ");
            Console.Write($"Experiences: {totalExperiencesCollected:N0} | ");
            Console.Write($"⏱️ {elapsed.Hours:D2}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s / {targetDuration.Hours:D2}h | ");
            Console.Write($"Remaining: {remaining.Hours:D2}h {remaining.Minutes:D2}m");
        }

        private void PrintSessionSummary()
        {
            TimeSpan totalElapsed = DateTime.UtcNow - sessionStart;
            DateTime sessionEnd = DateTime.UtcNow;

            Console.WriteLine("\n\n╔════════════════════════════════════════════╗");
            Console.WriteLine("║        COLLECTION SESSION COMPLETE         ║");
            Console.WriteLine("╚════════════════════════════════════════════╝\n");

            Console.WriteLine("📊 SESSION STATISTICS:");
            Console.WriteLine($"  Start Time:             {sessionStart:G}");
            Console.WriteLine($"  End Time:               {sessionEnd:G}");
            Console.WriteLine($"  Total Duration:         {totalElapsed:hh\\:mm\\:ss}");
            Console.WriteLine($"  Episodes Collected:     {totalEpisodesCollected}");
            Console.WriteLine($"  Total Experiences:      {totalExperiencesCollected:N0}");
            Console.WriteLine($"  Avg per Episode:        {(totalEpisodesCollected > 0 ? totalExperiencesCollected / totalEpisodesCollected : 0):F1}");
            Console.WriteLine($"  Experiences/Hour:       {(totalElapsed.TotalHours > 0 ? totalExperiencesCollected / totalElapsed.TotalHours : 0):F0}\n");

            // Show data statistics
            var analyzer = new DataAnalyzer();
            var stats = analyzer.AnalyzeData(dataManager);

            Console.WriteLine("📈 DATA STATISTICS:");
            Console.WriteLine($"  Avg Reward per Episode: {stats.AvgReward:F3}");
            Console.WriteLine($"  Max Reward:             {stats.MaxReward:F3}");
            Console.WriteLine($"  Min Reward:             {stats.MinReward:F3}");
            Console.WriteLine($"  Avg Confidence:         {stats.ConfidenceStats.AvgConfidence:P1}\n");

            Console.WriteLine("💾 DATA LOCATION:");
            Console.WriteLine("  ./training_data/\n");

            Console.WriteLine("🚀 NEXT STEPS:");
            Console.WriteLine("  1. Run: dotnet run -- train");
            Console.WriteLine("  2. Run: dotnet run -- analyze");
            Console.WriteLine("  3. Review results in console\n");

            Console.WriteLine("✅ Ready for model training!\n");
        }

        /// <summary>
        /// Entry point for running the automated collector.
        /// </summary>
        public static void RunCollection(TimeSpan? duration = null)
        {
            var collector = new AutomatedDataCollector(duration ?? TimeSpan.FromHours(11));
            collector.Run();
        }
    }
}
