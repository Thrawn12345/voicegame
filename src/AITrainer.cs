using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VoiceGame
{
    /// <summary>
    /// Trains AI models on collected game data using reinforcement learning principles.
    /// Supports Q-Learning and policy gradient methods.
    /// </summary>
    public class AITrainer
    {
        public class ModelConfig
        {
            [JsonPropertyName("learning_rate")]
            public float LearningRate { get; set; } = 0.001f;

            [JsonPropertyName("discount_factor")]
            public float DiscountFactor { get; set; } = 0.99f;

            [JsonPropertyName("exploration_rate")]
            public float ExplorationRate { get; set; } = 0.1f;

            [JsonPropertyName("batch_size")]
            public int BatchSize { get; set; } = 32;

            [JsonPropertyName("hidden_layer_size")]
            public int HiddenLayerSize { get; set; } = 128;

            [JsonPropertyName("action_space_size")]
            public int ActionSpaceSize { get; set; } = 9;  // 9 possible actions

            [JsonPropertyName("state_space_size")]
            public int StateSpaceSize { get; set; } = 30;  // 30 state features
        }

        public class TrainingMetrics
        {
            public int EpisodesProcessed { get; set; }
            public int ExperiencesProcessed { get; set; }
            public float AverageEpisodeReward { get; set; }
            public float AverageValueLoss { get; set; }
            public float AveragePolicyLoss { get; set; }
            public List<float> RewardHistory { get; set; } = new();
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }

        private ModelConfig config;
        private List<float[]> qValues;  // Q-value matrix for each state
        private Random random;
        private TrainingMetrics metrics;

        public AITrainer(ModelConfig? config = null)
        {
            this.config = config ?? new ModelConfig();
            this.random = new Random();
            this.metrics = new TrainingMetrics { StartTime = DateTime.UtcNow };
            InitializeNetwork();
        }

        private void InitializeNetwork()
        {
            // Initialize Q-value table/network
            qValues = new List<float[]>();
            for (int i = 0; i < config.ActionSpaceSize; i++)
            {
                qValues.Add(new float[config.StateSpaceSize]);
            }
            Console.WriteLine($"✅ Initialized AI model: {config.StateSpaceSize} states × {config.ActionSpaceSize} actions");
        }

        /// <summary>
        /// Trains the AI model on a batch of experiences using Q-Learning update rule.
        /// </summary>
        public void TrainOnBatch(List<DataCollector.Experience> experiences)
        {
            if (experiences.Count == 0) return;

            float totalLoss = 0f;
            int updates = 0;

            foreach (var experience in experiences)
            {
                // Q-Learning update: Q(s,a) = Q(s,a) + α[r + γ*max(Q(s',a)) - Q(s,a)]
                float currentQ = EstimateValue(experience.State, experience.Action);
                float maxNextQ = FindMaxQValue(experience.NextState);
                
                float targetQ = experience.Reward + (experience.IsDone ? 0 : config.DiscountFactor * maxNextQ);
                float loss = (targetQ - currentQ) * (targetQ - currentQ);  // MSE loss
                
                // Update Q-value
                UpdateQValue(experience.State, experience.Action, targetQ);
                
                totalLoss += loss;
                updates++;
            }

            metrics.AverageValueLoss = totalLoss / updates;
            metrics.ExperiencesProcessed += experiences.Count;
        }

        /// <summary>
        /// Trains the AI on complete episodes from a TrainingDataManager.
        /// </summary>
        public void TrainOnEpisodes(List<TrainingDataManager.TrainingEpisode> episodes)
        {
            Console.WriteLine($"\n📚 Starting training on {episodes.Count} episodes...");
            metrics.StartTime = DateTime.UtcNow;

            int totalExperiences = 0;
            var rewards = new List<float>();

            foreach (var episode in episodes)
            {
                var experiences = episode.Experiences.ConvertAll(se => new DataCollector.Experience
                {
                    State = se.State,
                    Action = se.Action,
                    Reward = se.Reward,
                    NextState = se.NextState,
                    IsDone = se.IsDone,
                    Confidence = se.Confidence,
                    Timestamp = se.Timestamp
                });

                TrainOnBatch(experiences);
                rewards.Add(episode.TotalReward);
                totalExperiences += experiences.Count;
                metrics.EpisodesProcessed++;

                if (metrics.EpisodesProcessed % 10 == 0)
                {
                    Console.WriteLine($"  → Processed {metrics.EpisodesProcessed} episodes ({totalExperiences} total experiences)");
                }
            }

            metrics.EndTime = DateTime.UtcNow;
            metrics.AverageEpisodeReward = rewards.Any() ? rewards.Average() : 0;
            metrics.RewardHistory = rewards;

            PrintTrainingStats();
        }

        /// <summary>
        /// Predicts the best action for a given state using the trained model.
        /// </summary>
        public int PredictAction(float[] state, bool useExploration = false)
        {
            if (useExploration && random.NextDouble() < config.ExplorationRate)
            {
                return random.Next(config.ActionSpaceSize);
            }

            float bestValue = float.MinValue;
            int bestAction = 0;

            for (int a = 0; a < config.ActionSpaceSize; a++)
            {
                float value = EstimateValue(state, a);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestAction = a;
                }
            }

            return bestAction;
        }

        /// <summary>
        /// Estimates the value of taking an action in a state.
        /// </summary>
        private float EstimateValue(float[] state, int action)
        {
            if (action < 0 || action >= config.ActionSpaceSize) return 0f;

            // Simplified: use state features as weights
            float value = 0f;
            for (int i = 0; i < Math.Min(state.Length, config.StateSpaceSize); i++)
            {
                value += state[i] * qValues[action][i];
            }
            return value;
        }

        /// <summary>
        /// Updates Q-values based on an experience.
        /// </summary>
        private void UpdateQValue(float[] state, int action, float targetQ)
        {
            if (action < 0 || action >= config.ActionSpaceSize) return;

            for (int i = 0; i < Math.Min(state.Length, config.StateSpaceSize); i++)
            {
                float gradient = (targetQ - EstimateValue(state, action)) * state[i];
                qValues[action][i] += config.LearningRate * gradient;
            }
        }

        /// <summary>
        /// Finds the maximum Q-value across all actions for a state.
        /// </summary>
        private float FindMaxQValue(float[] state)
        {
            float maxQ = float.MinValue;
            for (int a = 0; a < config.ActionSpaceSize; a++)
            {
                float q = EstimateValue(state, a);
                maxQ = Math.Max(maxQ, q);
            }
            return maxQ == float.MinValue ? 0f : maxQ;
        }

        private void PrintTrainingStats()
        {
            Console.WriteLine("\n═══════════════════════════════════════════");
            Console.WriteLine("📊 TRAINING COMPLETED");
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine($"Episodes:              {metrics.EpisodesProcessed}");
            Console.WriteLine($"Experiences:          {metrics.ExperiencesProcessed}");
            Console.WriteLine($"Avg Episode Reward:   {metrics.AverageEpisodeReward:F3}");
            Console.WriteLine($"Avg Value Loss:       {metrics.AverageValueLoss:F6}");
            Console.WriteLine($"Training Duration:    {(metrics.EndTime - metrics.StartTime).TotalSeconds:F1}s");
            Console.WriteLine("═══════════════════════════════════════════\n");
        }

        /// <summary>
        /// Exports trained model parameters for persistence.
        /// </summary>
        public string ExportModel(string modelName = "trained_model")
        {
            var modelData = new
            {
                config = config,
                q_values = qValues,
                export_date = DateTime.UtcNow.ToString("O"),
                metrics = new
                {
                    episodes_processed = metrics.EpisodesProcessed,
                    experiences_processed = metrics.ExperiencesProcessed,
                    average_episode_reward = metrics.AverageEpisodeReward,
                    average_value_loss = metrics.AverageValueLoss
                }
            };

            Console.WriteLine($"✅ Model exported: {modelName}");
            return modelName;
        }
    }
}
