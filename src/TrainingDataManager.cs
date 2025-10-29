using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceGame
{
    /// <summary>
    /// Manages saving and loading training data episodes in JSON format.
    /// </summary>
    public class TrainingDataManager
    {
        private string dataDirectory;
        private string currentSessionId;

        public class SerializableExperience
        {
            [JsonPropertyName("state")]
            public float[] State { get; set; }

            [JsonPropertyName("action")]
            public int Action { get; set; }

            [JsonPropertyName("reward")]
            public float Reward { get; set; }

            [JsonPropertyName("next_state")]
            public float[] NextState { get; set; }

            [JsonPropertyName("is_done")]
            public bool IsDone { get; set; }

            [JsonPropertyName("confidence")]
            public float Confidence { get; set; }

            [JsonPropertyName("timestamp")]
            public long Timestamp { get; set; }
        }

        public class TrainingEpisode
        {
            [JsonPropertyName("episode")]
            public int Episode { get; set; }

            [JsonPropertyName("session_id")]
            public string SessionId { get; set; }

            [JsonPropertyName("experiences_count")]
            public int ExperiencesCount { get; set; }

            [JsonPropertyName("total_reward")]
            public float TotalReward { get; set; }

            [JsonPropertyName("timestamp")]
            public long Timestamp { get; set; }

            [JsonPropertyName("experiences")]
            public List<SerializableExperience> Experiences { get; set; }
        }

        public TrainingDataManager(string dataDir = "training_data")
        {
            dataDirectory = dataDir;
            currentSessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }
        }

        /// <summary>
        /// Saves an episode to a JSON file in the training data directory.
        /// </summary>
        public string SaveEpisode(int episodeNum, List<DataCollector.Experience> experiences, float totalReward)
        {
            var serializableExperiences = new List<SerializableExperience>();
            foreach (var exp in experiences)
            {
                serializableExperiences.Add(new SerializableExperience
                {
                    State = exp.State,
                    Action = exp.Action,
                    Reward = exp.Reward,
                    NextState = exp.NextState,
                    IsDone = exp.IsDone,
                    Confidence = exp.Confidence,
                    Timestamp = exp.Timestamp
                });
            }

            var episode = new TrainingEpisode
            {
                Episode = episodeNum,
                SessionId = currentSessionId,
                ExperiencesCount = experiences.Count,
                TotalReward = totalReward,
                Timestamp = DateTime.UtcNow.Ticks,
                Experiences = serializableExperiences
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string filename = $"training_data_ep{episodeNum}_{DateTime.Now:HHmmss}.json";
            string filepath = Path.Combine(dataDirectory, filename);

            string json = JsonSerializer.Serialize(episode, options);
            File.WriteAllText(filepath, json);

            Console.WriteLine($"✅ Saved episode {episodeNum} with {experiences.Count} experiences to {filename}");
            return filepath;
        }

        /// <summary>
        /// Loads a training episode from JSON file.
        /// </summary>
        public TrainingEpisode LoadEpisode(string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException($"Training data file not found: {filepath}");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            string json = File.ReadAllText(filepath);
            return JsonSerializer.Deserialize<TrainingEpisode>(json, options);
        }

        /// <summary>
        /// Loads all training episodes from the data directory.
        /// </summary>
        public List<TrainingEpisode> LoadAllEpisodes()
        {
            var episodes = new List<TrainingEpisode>();
            var files = Directory.GetFiles(dataDirectory, "training_data_ep*.json");

            foreach (var file in files)
            {
                try
                {
                    episodes.Add(LoadEpisode(file));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Failed to load {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            return episodes;
        }

        /// <summary>
        /// Gets total experiences across all episodes.
        /// </summary>
        public (int episodeCount, int totalExperiences) GetDataStats()
        {
            var episodes = LoadAllEpisodes();
            int totalExp = 0;
            foreach (var ep in episodes)
            {
                totalExp += ep.ExperiencesCount;
            }
            return (episodes.Count, totalExp);
        }

        /// <summary>
        /// Exports collected episodes to a single consolidated JSON for model training.
        /// </summary>
        public string ExportForTraining(string outputName = "consolidated_training_data.json")
        {
            var episodes = LoadAllEpisodes();
            var consolidated = new
            {
                export_date = DateTime.UtcNow.ToString("O"),
                total_episodes = episodes.Count,
                total_experiences = episodes.Sum(e => e.ExperiencesCount),
                episodes = episodes
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string filepath = Path.Combine(dataDirectory, outputName);
            string json = JsonSerializer.Serialize(consolidated, options);
            File.WriteAllText(filepath, json);

            Console.WriteLine($"✅ Exported {episodes.Count} episodes to {outputName}");
            return filepath;
        }

        /// <summary>
        /// Clears all training data (use with caution).
        /// </summary>
        public void ClearAllData()
        {
            var files = Directory.GetFiles(dataDirectory, "training_data_ep*.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }
            Console.WriteLine($"✅ Cleared {files.Length} training data files");
        }
    }
}
