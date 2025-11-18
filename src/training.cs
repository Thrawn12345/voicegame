using System;
using System.Collections.Generic;

namespace VoiceGame
{
    /// <summary>
    /// Standalone console application for training AI on collected gameplay data.
    /// Commands: train, analyze, export, clear, interactive
    /// </summary>
    public class DataCollectionTrainer
    {
        private TrainingDataManager dataManager;
        private DataAnalyzer analyzer;

        public DataCollectionTrainer()
        {
            dataManager = new TrainingDataManager();
            analyzer = new DataAnalyzer();
        }

        public void Run(string[] args)
        {
            if (args.Length == 0)
            {
                ShowMenu();
                return;
            }

            string command = args[0].ToLower();

            switch (command)
            {
                case "train":
                    TrainModel();
                    break;
                case "analyze":
                    AnalyzeData();
                    break;
                case "export":
                    ExportData();
                    break;
                case "clear":
                    ClearData();
                    break;
                case "interactive":
                    InteractiveMode();
                    break;
                case "help":
                    PrintHelp();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                    break;
            }
        }

        private void ShowMenu()
        {
            Console.Clear();
            Console.WriteLine("╔═════════════════════════════════════════════╗");
            Console.WriteLine("║   Voice Game AI Training Tool               ║");
            Console.WriteLine("╚═════════════════════════════════════════════╝\n");

            Console.WriteLine("Commands:");
            Console.WriteLine("  1. train      - Train AI model on collected data");
            Console.WriteLine("  2. analyze    - Analyze training data statistics");
            Console.WriteLine("  3. export     - Export consolidated training data");
            Console.WriteLine("  4. clear      - Delete all training data");
            Console.WriteLine("  5. interactive- Interactive mode");
            Console.WriteLine("  6. help       - Show help\n");

            Console.Write("Enter command or number (1-6): ");
            string input = Console.ReadLine()?.ToLower() ?? "";

            Console.WriteLine();

            switch (input)
            {
                case "1":
                case "train":
                    TrainModel();
                    break;
                case "2":
                case "analyze":
                    AnalyzeData();
                    break;
                case "3":
                case "export":
                    ExportData();
                    break;
                case "4":
                case "clear":
                    ClearData();
                    break;
                case "5":
                case "interactive":
                    InteractiveMode();
                    break;
                case "6":
                case "help":
                    PrintHelp();
                    break;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }

        private void TrainModel()
        {
            Console.WriteLine("DEBUG: TrainModel() method started.");
            Console.WriteLine("🤖 TRAINING AI MODEL");
            Console.WriteLine(new string('═', 45) + "\n");

            try
            {
                Console.WriteLine("DEBUG: Loading episodes...");
                var episodes = dataManager.LoadAllEpisodes();
                Console.WriteLine($"DEBUG: Found {episodes.Count} episodes.");

                if (episodes.Count == 0)
                {
                    Console.WriteLine("❌ No training data found!");
                    Console.WriteLine("   Play the game to collect data first.\n");
                    return;
                }

                Console.WriteLine($"📚 Loaded {episodes.Count} episodes\n");
                Console.WriteLine("DEBUG: Creating AITrainer instance...");

                // Create trainer with default config
                var trainer = new AITrainer();
                Console.WriteLine("DEBUG: AITrainer instance created. Starting training...");

                // Train on all episodes
                trainer.TrainOnEpisodes(episodes);
                Console.WriteLine("DEBUG: Training finished.");

                // Show learning insights
                Console.WriteLine("DEBUG: Analyzing action rewards...");
                var actionRewards = analyzer.AnalyzeActionRewards(dataManager);
                Console.WriteLine("💡 LEARNING INSIGHTS:");
                Console.WriteLine("   Best performing actions:");
                foreach (var kvp in actionRewards.OrderByDescending(x => x.Value).Take(3))
                {
                    Console.WriteLine($"   • {kvp.Key,-15} avg reward: {kvp.Value:F3}");
                }

                // Export trained model using ModelManager (only save if it's the best)
                var modelManager = new ModelManager();
                var modelData = trainer.GetModelData();
                double performance = trainer.GetAverageReward();
                
                string modelPath = modelManager.SaveBestModel("player_ai", modelData, performance);

                Console.WriteLine("\n✅ Training completed!");
                Console.WriteLine($"   Best model saved to: {Path.GetFileName(modelPath)}");
                Console.WriteLine("   Model ready for deployment.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: An exception occurred in TrainModel: {ex.ToString()}");
                Console.WriteLine($"❌ Error during training: {ex.Message}\n");
            }
        }

        private void AnalyzeData()
        {
            Console.WriteLine("📊 ANALYZING TRAINING DATA");
            Console.WriteLine(new string('═', 45) + "\n");

            try
            {
                var stats = analyzer.AnalyzeData(dataManager);

                if (stats.TotalEpisodes == 0)
                {
                    Console.WriteLine("❌ No training data found!\n");
                    return;
                }

                // Print formatted report
                analyzer.PrintReport(stats);

                // Generate and display learning curve
                var curve = analyzer.GenerateLearningCurve(dataManager);
                Console.WriteLine("📈 LEARNING CURVE (10-episode windows):");
                foreach (var (episode, avgReward) in curve)
                {
                    int barLength = (int)((avgReward + 10) * 2);  // Scale for display
                    string bar = new string('█', Math.Max(0, barLength));
                    Console.WriteLine($"   Ep {episode,3}: {bar} {avgReward:F2}");
                }

                // Export detailed report
                analyzer.ExportReport(stats);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during analysis: {ex.Message}\n");
            }
        }

        private void ExportData()
        {
            Console.WriteLine("📤 EXPORTING TRAINING DATA");
            Console.WriteLine(new string('═', 45) + "\n");

            try
            {
                string exportPath = dataManager.ExportForTraining();
                Console.WriteLine($"✅ Data exported to: {exportPath}");
                Console.WriteLine("   Ready to use with TensorFlow, PyTorch, etc.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during export: {ex.Message}\n");
            }
        }

        private void ClearData()
        {
            Console.WriteLine("⚠️  DELETE ALL TRAINING DATA");
            Console.WriteLine(new string('═', 45));
            Console.Write("\nThis will permanently delete all training data files.\n");
            Console.Write("Continue? (yes/no): ");

            string response = Console.ReadLine()?.ToLower() ?? "";

            if (response == "yes" || response == "y")
            {
                try
                {
                    dataManager.ClearAllData();
                    Console.WriteLine("✅ All training data cleared.\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}\n");
                }
            }
            else
            {
                Console.WriteLine("Cancelled.\n");
            }
        }

        private void InteractiveMode()
        {
            Console.WriteLine("🎮 INTERACTIVE TRAINING MODE");
            Console.WriteLine(new string('═', 45) + "\n");

            bool running = true;
            while (running)
            {
                Console.WriteLine("\nOptions:");
                Console.WriteLine("  [A]nalyze data");
                Console.WriteLine("  [T]rain model");
                Console.WriteLine("  [E]xport data");
                Console.WriteLine("  [S]tatistics");
                Console.WriteLine("  [Q]uit\n");

                Console.Write("Choose (A/T/E/S/Q): ");
                string choice = Console.ReadLine()?.ToUpper() ?? "";

                switch (choice)
                {
                    case "A":
                        Console.Clear();
                        AnalyzeData();
                        break;
                    case "T":
                        Console.Clear();
                        TrainModel();
                        break;
                    case "E":
                        Console.Clear();
                        ExportData();
                        break;
                    case "S":
                        Console.Clear();
                        ShowQuickStats();
                        break;
                    case "Q":
                        running = false;
                        Console.WriteLine("\nGoodbye!\n");
                        break;
                    default:
                        Console.WriteLine("Invalid choice.");
                        break;
                }
            }
        }

        private void ShowQuickStats()
        {
            Console.WriteLine("📊 QUICK STATISTICS");
            Console.WriteLine(new string('═', 45) + "\n");

            try
            {
                var (episodeCount, totalExp) = dataManager.GetDataStats();
                Console.WriteLine($"Episodes collected:    {episodeCount}");
                Console.WriteLine($"Total experiences:     {totalExp}");
                Console.WriteLine($"Avg per episode:       {(episodeCount > 0 ? (float)totalExp / episodeCount : 0):F1}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}\n");
            }
        }

        private void PrintHelp()
        {
            Console.WriteLine("📖 HELP - Voice Game AI Training Tool");
            Console.WriteLine(new string('═', 45) + "\n");

            Console.WriteLine("COMMANDS:");
            Console.WriteLine("  dotnet run -- train     Train AI model");
            Console.WriteLine("  dotnet run -- analyze   Analyze data");
            Console.WriteLine("  dotnet run -- export    Export for ML");
            Console.WriteLine("  dotnet run -- clear     Delete data");
            Console.WriteLine("  dotnet run -- interactive  Interactive menu\n");

            Console.WriteLine("WORKFLOW:");
            Console.WriteLine("  1. Play the game (data auto-collected)");
            Console.WriteLine("  2. Run: dotnet run -- analyze");
            Console.WriteLine("  3. Run: dotnet run -- train");
            Console.WriteLine("  4. Check results\n");

            Console.WriteLine("DATA LOCATION:");
            Console.WriteLine("  ./training_data/\n");

            Console.WriteLine("See AI_TRAINING_SETUP.md for detailed docs.\n");
        }
    }
}
