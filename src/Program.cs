using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VoiceGame
{
    public static class Program
    {
        // Import Windows API functions for console attachment
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        public static void Main(string[] args)
        {
            // Handle command-line arguments for automated collection
            if (args.Length > 0)
            {
                // Attach to parent console or allocate new one for console output
                if (!AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    AllocConsole();
                }

                string command = args[0].ToLower();

                switch (command)
                {
                    case "collect":
                        // Run automated data collection
                        TimeSpan duration = TimeSpan.FromHours(11);
                        if (args.Length > 1 && int.TryParse(args[1], out int hours))
                        {
                            duration = TimeSpan.FromHours(hours);
                        }
                        AutomatedDataCollector.RunCollection(duration);
                        return;

                    case "train":
                        // Run training on collected data
                        var trainer = new DataCollectionTrainer();
                        trainer.Run(new[] { "train" });
                        return;

                    case "analyze":
                        // Analyze collected data
                        var trainerAnalyze = new DataCollectionTrainer();
                        trainerAnalyze.Run(new[] { "analyze" });
                        return;

                    case "export":
                        // Export data for external use
                        var trainerExport = new DataCollectionTrainer();
                        trainerExport.Run(new[] { "export" });
                        return;

                    case "interactive":
                        // Launch interactive trainer menu
                        var interactiveTrainer = new DataCollectionTrainer();
                        interactiveTrainer.Run(new[] { "interactive" });
                        return;

                    case "auto":
                    case "autotrain":
                        // Run infinite AI vs AI training loop
                        RunAutoTrainer();
                        return;

                    case "dodge":
                        // Run specialized dodge training
                        RunDodgeTraining();
                        return;

                    case "range":
                        // Run shooting range training system
                        RunShootingRange();
                        return;

                    case "cyclic":
                        // Run background cyclic training system
                        RunBackgroundCyclicTraining();
                        return;

                    case "models":
                        // Show current AI models
                        var modelManager = new ModelManager();
                        modelManager.PrintModelSummary();
                        return;

                    case "cleanup":
                        // Clean up old training data and migrate models
                        var cleanupManager = new ModelManager();
                        cleanupManager.MigrateOldModels();
                        cleanupManager.CleanupTrainingData(50);
                        Console.WriteLine("✅ Cleanup completed!");
                        return;

                    case "help":
                        PrintCommandHelp();
                        return;
                }
            }

            // Default: Run the game with GUI
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameForm());
        }

        private static void RunAutoTrainer()
        {
            try
            {
                var autoTrainer = new AutoTrainer();
                var cts = new System.Threading.CancellationTokenSource();

                // Handle Ctrl+C gracefully
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                Console.WriteLine("🚀 Starting auto-trainer...\n");
                autoTrainer.RunInfiniteTraining(cts.Token).Wait();
            }
            catch (AggregateException aex)
            {
                Console.WriteLine($"\n❌ Error during auto-training:");
                foreach (var ex in aex.InnerExceptions)
                {
                    Console.WriteLine($"   {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"   Stack: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during auto-training: {ex.Message}");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }

        private static void RunBackgroundCyclicTraining()
        {
            Console.Clear();
            
            try
            {
                var config = new BackgroundCyclicTrainer.CyclicTrainingConfig
                {
                    TotalCycles = 25, // 25 complete cycles
                    GameAIEpisodesPerCycle = 50,
                    ShootingCyclesPerPhase = 3,
                    DodgingCyclesPerPhase = 3,
                    MaxTrainingTime = TimeSpan.FromHours(8) // 8 hours maximum
                };

                var trainer = new BackgroundCyclicTrainer(config);
                
                // Setup cancellation for Ctrl+C
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\\n🛑 Graceful shutdown initiated...");
                    cts.Cancel();
                };

                Console.WriteLine("Press Ctrl+C to stop training gracefully at any time.\\n");
                
                // Run the training
                trainer.RunCyclicTraining(cts.Token).Wait();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is OperationCanceledException)
                {
                    Console.WriteLine("\\n✅ Training stopped gracefully by user");
                }
                else
                {
                    Console.WriteLine($"\\n❌ Error during cyclic training:");
                    Console.WriteLine($"   {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\\n❌ Error during cyclic training: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private static void RunDodgeTraining()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║              DODGE TRAINING SYSTEM                   ║");
            Console.WriteLine("║   Specialized Movement & Bullet Dodging Training    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

            try
            {
                var config = new DodgeTrainingSystem.DodgeTrainingConfig
                {
                    TrainPlayerDodging = true,
                    TrainCompanionCoordination = true,
                    IncludeObstacles = true,
                    EmphasizeFormationMaintenance = true,
                    BulletDensity = 1.2f, // Slightly higher bullet density for challenge
                    TrainingDuration = TimeSpan.FromHours(3) // 3 hours of focused training
                };

                Console.WriteLine("🎯 Configuration:");
                Console.WriteLine($"   • Player Dodge Training: {config.TrainPlayerDodging}");
                Console.WriteLine($"   • Companion Coordination: {config.TrainCompanionCoordination}");
                Console.WriteLine($"   • Obstacle Avoidance: {config.IncludeObstacles}");
                Console.WriteLine($"   • Formation Maintenance: {config.EmphasizeFormationMaintenance}");
                Console.WriteLine($"   • Bullet Density: {(config.BulletDensity * 100):F0}%");
                Console.WriteLine($"   • Training Duration: {config.TrainingDuration.TotalHours:F1} hours\n");

                Console.WriteLine("Press Ctrl+C to stop training early.\n");

                var dodgeTrainer = new DodgeTrainingSystem();
                dodgeTrainer.RunDodgeTraining(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during dodge training: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private static void RunShootingRange()
        {
            Console.Clear();
            Console.WriteLine("================================================================");
            Console.WriteLine("                    SHOOTING RANGE SYSTEM                     ");
            Console.WriteLine("        Advanced Shooting & Dodging Training Cycles          ");
            Console.WriteLine("================================================================\n");

            try
            {
                var config = new ShootingRangeSystem.ShootingRangeConfig
                {
                    ShootingEpisodesPerCycle = 40,
                    DodgingEpisodesPerCycle = 40,
                    TotalTrainingCycles = 25, // 25 cycles = 2000 episodes total
                    TrainPlayerShooting = true,
                    TrainCompanionShooting = true,
                    TrainEnemyDodging = true,
                    EnemyDodgingLearningRate = 0.015f,
                    CompanionCount = 3,
                    EnemyCount = 5
                };

                Console.WriteLine("Configuration Summary:");
                Console.WriteLine($"   Total Cycles: {config.TotalTrainingCycles}");
                Console.WriteLine($"   Episodes per Cycle: {config.ShootingEpisodesPerCycle + config.DodgingEpisodesPerCycle}");
                Console.WriteLine($"   Total Episodes: {config.TotalTrainingCycles * (config.ShootingEpisodesPerCycle + config.DodgingEpisodesPerCycle)}");
                Console.WriteLine($"   Companions: {config.CompanionCount}");
                Console.WriteLine($"   Target Enemies: {config.EnemyCount}");
                Console.WriteLine($"   Enemy Learning Rate: {config.EnemyDodgingLearningRate:P1}\n");

                Console.WriteLine("Training Phases per Cycle:");
                Console.WriteLine($"   1. Shooting Training: {config.ShootingEpisodesPerCycle} episodes");
                Console.WriteLine($"   2. Dodging Training: {config.DodgingEpisodesPerCycle} episodes");
                Console.WriteLine($"   3. AI Neural Network Training");
                Console.WriteLine($"   4. Enemy Learning Update\n");

                Console.WriteLine("Press Ctrl+C to stop training early.\n");

                var shootingRange = new ShootingRangeSystem();
                var results = shootingRange.RunShootingRangeTraining(config);

                Console.WriteLine("\nTraining Results Summary:");
                Console.WriteLine($"Final Shooting Accuracy: {results.AverageShootingAccuracy:P1}");
                Console.WriteLine($"Final Dodging Success: {results.AverageDodgingSuccessRate:P1}");
                Console.WriteLine($"Completed Cycles: {results.CompletedCycles}/{config.TotalTrainingCycles}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError during shooting range training: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private static void PrintCommandHelp()
        {
            Console.WriteLine("\n╔════════════════════════════════════════════╗");
            Console.WriteLine("║     VOICE GAME - COMMAND LINE OPTIONS      ║");
            Console.WriteLine("╚════════════════════════════════════════════╝\n");

            Console.WriteLine("GAMEPLAY:");
            Console.WriteLine("  dotnet run                  Launch game (default)\n");

            Console.WriteLine("DATA COLLECTION:");
            Console.WriteLine("  dotnet run -- collect       Collect 11 hours of data");
            Console.WriteLine("  dotnet run -- collect 24    Collect 24 hours of data");
            Console.WriteLine("  dotnet run -- collect 4     Collect 4 hours of data\n");

            Console.WriteLine("AI TRAINING:");
            Console.WriteLine("  dotnet run -- train         Train AI model on collected data");
            Console.WriteLine("  dotnet run -- analyze       View training data statistics");
            Console.WriteLine("  dotnet run -- export        Export data for ML frameworks");
            Console.WriteLine("  dotnet run -- auto          Run infinite AI vs AI training");
            Console.WriteLine("  dotnet run -- dodge         Run specialized dodge & movement training");
            Console.WriteLine("  dotnet run -- range         Run shooting range with alternating training cycles");
            Console.WriteLine("");
            Console.WriteLine("📊 REPORTING SYSTEM:");
            Console.WriteLine("  All training modes now generate detailed hourly JSON reports");
            Console.WriteLine("  Reports are saved to: ./training_reports/");
            Console.WriteLine("  Each report includes: performance metrics, AI progress, recommendations");
            Console.WriteLine("  dotnet run -- interactive   Launch interactive trainer menu\n");

            Console.WriteLine("MODEL MANAGEMENT:");
            Console.WriteLine("  dotnet run -- models        Show current AI models");
            Console.WriteLine("  dotnet run -- cleanup       Clean old files & migrate models\n");

            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Play the game normally");
            Console.WriteLine("  dotnet run\n");

            Console.WriteLine("  # Collect 11 hours of automated training data");
            Console.WriteLine("  dotnet run -- collect\n");

            Console.WriteLine("  # Train AI model");
            Console.WriteLine("  dotnet run -- train\n");

            Console.WriteLine("  # Analyze collected training data");
            Console.WriteLine("  dotnet run -- analyze\n");

            Console.WriteLine("  # Run infinite AI vs AI training (Press Ctrl+C to stop)");
            Console.WriteLine("  dotnet run -- auto\n");
        }
    }
}
