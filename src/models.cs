using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VoiceGame
{
    /// <summary>
    /// Manages AI model storage, keeping only the best/newest models to prevent folder clutter.
    /// Automatically cleans up old models and maintains a clean model directory.
    /// </summary>
    public class ModelManager
    {
        private const string ModelsDirectory = "models";
        private const string TrainingDataDirectory = "training_data";

        public ModelManager()
        {
            // Ensure models directory exists
            if (!Directory.Exists(ModelsDirectory))
            {
                Directory.CreateDirectory(ModelsDirectory);
            }
        }

        /// <summary>
        /// Saves an AI model, replacing any existing model of the same type with a better one.
        /// </summary>
        public string SaveBestModel(string modelType, object modelData, double performance = 0.0)
        {
            string modelFileName = $"{modelType}_model.json";
            string modelPath = Path.Combine(ModelsDirectory, modelFileName);
            string backupPath = Path.Combine(ModelsDirectory, $"{modelType}_model_backup.json");

            try
            {
                // If model exists, check if new one is better
                if (File.Exists(modelPath))
                {
                    var existingPerformance = GetModelPerformance(modelPath);
                    var modelAge = DateTime.Now - File.GetLastWriteTime(modelPath);
                    
                    // If both performances are 0, only replace if model is old (> 5 minutes)
                    if (performance == 0.0 && existingPerformance == 0.0)
                    {
                        if (modelAge.TotalMinutes < 5)
                        {
                            return modelPath; // Keep recent model
                        }
                        // Otherwise allow replacement with fresh 0-performance model
                    }
                    
                    // If new model isn't significantly better, don't replace
                    else if (performance > 0 && existingPerformance > 0 && 
                             performance <= existingPerformance * 1.05) // Need at least 5% improvement
                    {
                        Console.WriteLine($"⚠️ Keeping existing {modelType} model (performance: {existingPerformance:F2} vs {performance:F2})");
                        return modelPath;
                    }

                    // Backup existing model before replacing
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(modelPath, backupPath);
                    Console.WriteLine($"📁 Backed up existing {modelType} model");
                }

                // Save the new model
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(modelData, jsonOptions);
                File.WriteAllText(modelPath, json);

                Console.WriteLine($"✅ Saved best {modelType} model (performance: {performance:F2})");
                return modelPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving {modelType} model: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the performance metric from an existing model file.
        /// </summary>
        private double GetModelPerformance(string modelPath)
        {
            try
            {
                string json = File.ReadAllText(modelPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("metrics", out JsonElement metrics))
                {
                    if (metrics.TryGetProperty("average_episode_reward", out JsonElement reward))
                    {
                        return reward.GetDouble();
                    }
                    if (metrics.TryGetProperty("performance_score", out JsonElement score))
                    {
                        return score.GetDouble();
                    }
                }
                return 0.0;
            }
            catch
            {
                return 0.0; // If we can't parse, assume worst performance
            }
        }

        /// <summary>
        /// Loads the best model of a specific type.
        /// </summary>
        public string GetBestModelPath(string modelType)
        {
            string modelPath = Path.Combine(ModelsDirectory, $"{modelType}_model.json");
            return File.Exists(modelPath) ? modelPath : string.Empty;
        }

        /// <summary>
        /// Cleans up old training data files, keeping only recent ones.
        /// </summary>
        public void CleanupTrainingData(int keepRecentCount = 50)
        {
            try
            {
                if (!Directory.Exists(TrainingDataDirectory))
                    return;

                var trainingFiles = Directory.GetFiles(TrainingDataDirectory, "training_data_ep*.json")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToArray();

                if (trainingFiles.Length <= keepRecentCount)
                    return;

                int deletedCount = 0;
                for (int i = keepRecentCount; i < trainingFiles.Length; i++)
                {
                    try
                    {
                        File.Delete(trainingFiles[i]);
                        deletedCount++;
                    }
                    catch { /* Ignore individual file deletion errors */ }
                }

                if (deletedCount > 0)
                {
                    Console.WriteLine($"🧹 Cleaned up {deletedCount} old training data files (kept {keepRecentCount} recent)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during training data cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up old model files from training_data directory that should now be in models directory.
        /// </summary>
        public void MigrateOldModels()
        {
            try
            {
                if (!Directory.Exists(TrainingDataDirectory))
                    return;

                // Find model files in training_data directory
                var oldModelFiles = Directory.GetFiles(TrainingDataDirectory, "*_model_*.json");
                
                foreach (string oldModelFile in oldModelFiles)
                {
                    try
                    {
                        string fileName = Path.GetFileName(oldModelFile);
                        
                        // Determine model type from filename
                        string modelType = "unknown";
                        if (fileName.Contains("enemy_movement"))
                            modelType = "enemy_movement";
                        else if (fileName.Contains("enemy_shooting"))
                            modelType = "enemy_shooting";
                        else if (fileName.Contains("player_shooting"))
                            modelType = "player_shooting";
                        else if (fileName.Contains("ai_model"))
                            modelType = "player_ai";

                        if (modelType != "unknown")
                        {
                            // Load the old model and save it as the new best model
                            string json = File.ReadAllText(oldModelFile);
                            var modelData = JsonSerializer.Deserialize<object>(json);
                            if (modelData != null)
                            {
                                SaveBestModel(modelType, modelData);
                            }
                        }

                        // Delete the old model file
                        File.Delete(oldModelFile);
                    }
                    catch { /* Ignore individual file errors */ }
                }

                if (oldModelFiles.Length > 0)
                {
                    Console.WriteLine($"🔄 Migrated {oldModelFiles.Length} old model files to models directory");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error during model migration: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a summary of all current models.
        /// </summary>
        public void PrintModelSummary()
        {
            try
            {
                var modelFiles = Directory.GetFiles(ModelsDirectory, "*_model.json");
                
                Console.WriteLine("\n╔═══════════════════════════════════════════════╗");
                Console.WriteLine("║              CURRENT AI MODELS                ║");
                Console.WriteLine("╚═══════════════════════════════════════════════╝");

                if (modelFiles.Length == 0)
                {
                    Console.WriteLine("  No models found in models directory.");
                    return;
                }

                foreach (string modelFile in modelFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(modelFile);
                    string modelType = fileName.Replace("_model", "");
                    var fileInfo = new FileInfo(modelFile);
                    double performance = GetModelPerformance(modelFile);

                    Console.WriteLine($"  🤖 {modelType,-20} Performance: {performance,6:F2}  Size: {fileInfo.Length / 1024,4:F0}KB");
                    Console.WriteLine($"     Last Updated: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting model summary: {ex.Message}");
            }
        }
    }
}