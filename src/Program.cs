using System;
using System.Windows.Forms;

namespace VoiceGame
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Handle command-line arguments for automated collection
            if (args.Length > 0)
            {
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
            Console.WriteLine("  dotnet run -- interactive   Launch interactive trainer menu\n");

            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Play the game normally");
            Console.WriteLine("  dotnet run\n");

            Console.WriteLine("  # Collect 11 hours of automated training data");
            Console.WriteLine("  dotnet run -- collect\n");

            Console.WriteLine("  # Train AI model");
            Console.WriteLine("  dotnet run -- train\n");

            Console.WriteLine("  # Analyze collected training data");
            Console.WriteLine("  dotnet run -- analyze\n");
        }
    }
}
