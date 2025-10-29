using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceGame
{
    /// <summary>
    /// Analyzes and visualizes training data to understand AI learning progress.
    /// </summary>
    public class DataAnalyzer
    {
        public class DataStats
        {
            [JsonPropertyName("total_episodes")]
            public int TotalEpisodes { get; set; }

            [JsonPropertyName("total_experiences")]
            public int TotalExperiences { get; set; }

            [JsonPropertyName("avg_episode_length")]
            public float AvgEpisodeLength { get; set; }

            [JsonPropertyName("avg_reward")]
            public float AvgReward { get; set; }

            [JsonPropertyName("max_reward")]
            public float MaxReward { get; set; }

            [JsonPropertyName("min_reward")]
            public float MinReward { get; set; }

            [JsonPropertyName("reward_std_dev")]
            public float RewardStdDev { get; set; }

            [JsonPropertyName("action_distribution")]
            public Dictionary<string, int> ActionDistribution { get; set; }

            [JsonPropertyName("confidence_stats")]
            public ConfidenceStats ConfidenceStats { get; set; }
        }

        public class ConfidenceStats
        {
            [JsonPropertyName("avg_confidence")]
            public float AvgConfidence { get; set; }

            [JsonPropertyName("min_confidence")]
            public float MinConfidence { get; set; }

            [JsonPropertyName("max_confidence")]
            public float MaxConfidence { get; set; }

            [JsonPropertyName("high_confidence_pct")]
            public float HighConfidencePercentage { get; set; }
        }

        /// <summary>
        /// Analyzes all training data and generates statistics.
        /// </summary>
        public DataStats AnalyzeData(TrainingDataManager manager)
        {
            var episodes = manager.LoadAllEpisodes();
            
            if (episodes.Count == 0)
            {
                Console.WriteLine("⚠️  No training data found!");
                return new DataStats();
            }

            var allExperiences = new List<TrainingDataManager.SerializableExperience>();
            var allRewards = new List<float>();
            var actionCounts = new Dictionary<string, int>();

            foreach (var ep in episodes)
            {
                allExperiences.AddRange(ep.Experiences);
                allRewards.Add(ep.TotalReward);
            }

            // Calculate action distribution
            for (int i = 0; i < 9; i++)
            {
                var actionName = DataCollector.ActionToString(i);
                var count = allExperiences.Count(e => e.Action == i);
                actionCounts[actionName] = count;
            }

            // Calculate confidence statistics
            float avgConfidence = allExperiences.Average(e => e.Confidence);
            float minConfidence = allExperiences.Min(e => e.Confidence);
            float maxConfidence = allExperiences.Max(e => e.Confidence);
            float highConfidencePct = allExperiences.Count(e => e.Confidence >= 0.7f) * 100f / allExperiences.Count;

            // Calculate reward statistics
            float avgReward = allRewards.Average();
            float maxReward = allRewards.Max();
            float minReward = allRewards.Min();
            float variance = allRewards.Sum(r => (r - avgReward) * (r - avgReward)) / allRewards.Count;
            float stdDev = (float)Math.Sqrt(variance);

            var stats = new DataStats
            {
                TotalEpisodes = episodes.Count,
                TotalExperiences = allExperiences.Count,
                AvgEpisodeLength = (float)allExperiences.Count / episodes.Count,
                AvgReward = avgReward,
                MaxReward = maxReward,
                MinReward = minReward,
                RewardStdDev = stdDev,
                ActionDistribution = actionCounts,
                ConfidenceStats = new ConfidenceStats
                {
                    AvgConfidence = avgConfidence,
                    MinConfidence = minConfidence,
                    MaxConfidence = maxConfidence,
                    HighConfidencePercentage = highConfidencePct
                }
            };

            return stats;
        }

        /// <summary>
        /// Prints formatted statistics report to console.
        /// </summary>
        public void PrintReport(DataStats stats)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════╗");
            Console.WriteLine("║     TRAINING DATA ANALYSIS REPORT          ║");
            Console.WriteLine("╚════════════════════════════════════════════╝\n");

            Console.WriteLine("📈 EPISODE STATISTICS:");
            Console.WriteLine($"  Total Episodes:        {stats.TotalEpisodes}");
            Console.WriteLine($"  Total Experiences:     {stats.TotalExperiences}");
            Console.WriteLine($"  Avg Episode Length:    {stats.AvgEpisodeLength:F1} steps\n");

            Console.WriteLine("💰 REWARD STATISTICS:");
            Console.WriteLine($"  Average Reward:        {stats.AvgReward:F3}");
            Console.WriteLine($"  Max Reward:            {stats.MaxReward:F3}");
            Console.WriteLine($"  Min Reward:            {stats.MinReward:F3}");
            Console.WriteLine($"  Std Deviation:         {stats.RewardStdDev:F3}\n");

            Console.WriteLine("🎮 ACTION DISTRIBUTION:");
            foreach (var kvp in stats.ActionDistribution.OrderByDescending(x => x.Value))
            {
                int pct = stats.TotalExperiences > 0 ? kvp.Value * 100 / stats.TotalExperiences : 0;
                Console.WriteLine($"  {kvp.Key,-15} {kvp.Value,5} ({pct,3}%)");
            }

            Console.WriteLine($"\n🎤 VOICE RECOGNITION QUALITY:");
            Console.WriteLine($"  Avg Confidence:        {stats.ConfidenceStats.AvgConfidence:P1}");
            Console.WriteLine($"  Min Confidence:        {stats.ConfidenceStats.MinConfidence:P1}");
            Console.WriteLine($"  Max Confidence:        {stats.ConfidenceStats.MaxConfidence:P1}");
            Console.WriteLine($"  High Confidence (≥70%): {stats.ConfidenceStats.HighConfidencePercentage:F1}%");

            Console.WriteLine("\n" + new string('═', 44) + "\n");
        }

        /// <summary>
        /// Generates a learning curve showing reward trends over episodes.
        /// </summary>
        public List<(int episode, float avgReward)> GenerateLearningCurve(TrainingDataManager manager, int windowSize = 10)
        {
            var episodes = manager.LoadAllEpisodes().OrderBy(e => e.Episode).ToList();
            var curve = new List<(int, float)>();

            for (int i = 0; i < episodes.Count; i += windowSize)
            {
                int endIdx = Math.Min(i + windowSize, episodes.Count);
                var window = episodes.Skip(i).Take(endIdx - i).ToList();
                float avgReward = window.Average(e => e.TotalReward);
                curve.Add((episodes[i].Episode, avgReward));
            }

            return curve;
        }

        /// <summary>
        /// Exports analysis report to JSON file.
        /// </summary>
        public string ExportReport(DataStats stats, string outputPath = "data_analysis_report.json")
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(new { analysis_date = DateTime.UtcNow, stats }, options);
            File.WriteAllText(outputPath, json);
            Console.WriteLine($"✅ Analysis report exported to {outputPath}");
            return outputPath;
        }

        /// <summary>
        /// Identifies actions that receive high rewards (learning insights).
        /// </summary>
        public Dictionary<string, float> AnalyzeActionRewards(TrainingDataManager manager)
        {
            var episodes = manager.LoadAllEpisodes();
            var actionRewards = new Dictionary<string, List<float>>();

            for (int i = 0; i < 9; i++)
            {
                actionRewards[DataCollector.ActionToString(i)] = new List<float>();
            }

            foreach (var episode in episodes)
            {
                foreach (var exp in episode.Experiences)
                {
                    string actionName = DataCollector.ActionToString(exp.Action);
                    actionRewards[actionName].Add(exp.Reward);
                }
            }

            var avgRewardsByAction = actionRewards
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count > 0 ? kvp.Value.Average() : 0f
                );

            return avgRewardsByAction;
        }
    }
}
