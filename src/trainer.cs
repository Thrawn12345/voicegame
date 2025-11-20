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
            public float LearningRate { get; set; } = 0.0003f;  // Further reduced for companion system stability

            [JsonPropertyName("discount_factor")]
            public float DiscountFactor { get; set; } = 0.98f;  // Increased to value long-term companion coordination

            [JsonPropertyName("exploration_rate")]
            public float ExplorationRate { get; set; } = 0.12f;  // Balanced exploration for companion coordination

            [JsonPropertyName("batch_size")]
            public int BatchSize { get; set; } = 128;  // Larger batches for companion feature learning

            [JsonPropertyName("hidden_layer_size")]
            public int HiddenLayerSize { get; set; } = 384;  // Enhanced capacity for companion coordination

            [JsonPropertyName("action_space_size")]
            public int ActionSpaceSize { get; set; } = 12;  // 12 possible actions (9 basic + 3 target-based)

            [JsonPropertyName("state_space_size")]
            public int StateSpaceSize { get; set; } = 42;  // 42 state features (34 + 8 enhanced companion features)
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
        private float[][] qValues = Array.Empty<float[]>();
        private Random random;
        private TrainingMetrics metrics;
        
        // Adaptive training parameters for companion coordination
        private float companionPerformanceWeight = 1.2f;  // Bonus for good companion coordination
        private int adaptiveTrainingWindow = 100;  // Episodes to evaluate performance
        private float performanceThreshold = 0.7f;  // Threshold for increasing learning rate
        private Queue<float> recentPerformanceScores = new();

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
            qValues = new float[config.ActionSpaceSize][];
            for (int i = 0; i < config.ActionSpaceSize; i++)
            {
                qValues[i] = new float[config.StateSpaceSize];
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
        public string ExportModel(string modelPath = "trained_model.json")
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

            try
            {
                // Ensure directory exists
                string? directory = System.IO.Path.GetDirectoryName(modelPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // Save to JSON file
                var json = System.Text.Json.JsonSerializer.Serialize(modelData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(modelPath, json);

                Console.WriteLine($"✅ Model exported: {modelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to export model: {ex.Message}");
            }

            return modelPath;
        }

        /// <summary>
        /// Load a trained model from file.
        /// </summary>
        public static AITrainer LoadModel(string modelPath)
        {
            // For now, return a new trainer with default config
            // In production, you would deserialize the saved model
            Console.WriteLine($"📂 Loading model from {modelPath}");
            return new AITrainer();
        }

        /// <summary>
        /// Predict best action for given state.
        /// </summary>
        public int PredictAction(float[] state)
        {
            if (state.Length != config.StateSpaceSize)
            {
                throw new ArgumentException($"State size mismatch. Expected {config.StateSpaceSize}, got {state.Length}");
            }

            float[] actionValues = new float[config.ActionSpaceSize];

            // Calculate Q-values for each action
            for (int action = 0; action < config.ActionSpaceSize; action++)
            {
                actionValues[action] = CalculateQValue(state, action);
            }

            // Return action with highest Q-value
            int bestAction = 0;
            float bestValue = actionValues[0];
            for (int i = 1; i < actionValues.Length; i++)
            {
                if (actionValues[i] > bestValue)
                {
                    bestValue = actionValues[i];
                    bestAction = i;
                }
            }

            return bestAction;
        }

        /// <summary>
        /// Get Q-values for all actions given a state.
        /// </summary>
        public float[] GetQValues(float[] state)
        {
            float[] actionValues = new float[config.ActionSpaceSize];

            for (int action = 0; action < config.ActionSpaceSize; action++)
            {
                actionValues[action] = CalculateQValue(state, action);
            }

            return actionValues;
        }

        /// <summary>
        /// Calculate Q-value for a specific state-action pair.
        /// </summary>
        private float CalculateQValue(float[] state, int action)
        {
            if (action >= qValues.Length)
                return 0f;

            float qValue = 0f;
            for (int i = 0; i < Math.Min(state.Length, qValues[action].Length); i++)
            {
                qValue += state[i] * qValues[action][i];
            }

            return qValue;
        }

        /// <summary>
        /// Gets the average reward from training metrics.
        /// </summary>
        public double GetAverageReward()
        {
            return metrics.AverageEpisodeReward;
        }

        /// <summary>
        /// Gets the model data for saving to ModelManager.
        /// </summary>
        public object GetModelData()
        {
            return new
            {
                config = config,
                q_values = qValues,
                export_date = DateTime.UtcNow.ToString("O"),
                metrics = new
                {
                    episodes_processed = metrics.EpisodesProcessed,
                    experiences_processed = metrics.ExperiencesProcessed,
                    average_episode_reward = metrics.AverageEpisodeReward,
                    average_value_loss = metrics.AverageValueLoss,
                    performance_score = metrics.AverageEpisodeReward
                }
            };
        }

        /// <summary>
        /// Calculate enhanced reward that considers companion coordination, wall proximity, and movement.
        /// </summary>
        public float CalculateCompanionAwareReward(
            int enemiesDestroyed,
            int bulletsDestroyed, 
            int lives,
            bool gameOver,
            int companionsAlive,
            FormationType formationType,
            float formationEfficiency,
            bool companionFireSupport,
            PointF currentPosition,
            PointF previousPosition,
            int windowWidth,
            int windowHeight,
            float deltaTime)
        {
            float baseReward = 0f;
            
            // Base rewards
            baseReward += enemiesDestroyed * 10f;    // Enemy kills
            baseReward += bulletsDestroyed * 8f;     // Bullet destruction (increased from 2 to 8)
            baseReward += lives * 5f;                // Staying alive
            
            // Wall proximity penalty (discourage wall-hugging)
            float edgeMargin = 80f;
            bool nearWall = currentPosition.X < edgeMargin || currentPosition.X > windowWidth - edgeMargin ||
                           currentPosition.Y < edgeMargin || currentPosition.Y > windowHeight - edgeMargin;
            if (nearWall)
            {
                baseReward -= 8f; // Strong penalty for being near walls
            }
            
            // Stationary penalty (discourage camping)
            float moveDist = (float)Math.Sqrt(
                Math.Pow(currentPosition.X - previousPosition.X, 2) +
                Math.Pow(currentPosition.Y - previousPosition.Y, 2));
            
            if (moveDist < 3f && deltaTime > 1f) // Barely moved for over 1 second
            {
                baseReward -= 10f; // Heavy penalty for staying still
            }
            else if (moveDist > 5f)
            {
                baseReward += 2f; // Small reward for active movement
            }
            
            // Penalty for game over
            if (gameOver)
            {
                baseReward -= 50f;
            }
            
            // Companion coordination bonuses
            float companionBonus = 0f;
            companionBonus += companionsAlive * 3f;           // Keep companions alive
            companionBonus += formationEfficiency * 8f;       // Formation effectiveness
            companionBonus += companionFireSupport ? 5f : 0f; // Coordinated fire support
            
            // Formation type bonuses (situational effectiveness)
            float formationBonus = formationType switch
            {
                FormationType.Adaptive => 4f,  // Best formation
                FormationType.Wedge => 3f,     // Good for offense
                FormationType.Diamond => 2f,   // Good for defense
                FormationType.Line => 1f,      // Basic formation
                FormationType.Circle => 1.5f,  // Good for surrounded situations
                _ => 0f
            };
            
            // Apply companion performance weight
            float totalReward = baseReward + (companionBonus + formationBonus) * companionPerformanceWeight;
            
            // Adaptive learning rate based on recent performance
            UpdateAdaptiveLearning(totalReward);
            
            return totalReward;
        }

        /// <summary>
        /// Update adaptive learning parameters based on recent performance.
        /// </summary>
        private void UpdateAdaptiveLearning(float episodeReward)
        {
            recentPerformanceScores.Enqueue(episodeReward);
            
            // Keep only recent scores within the window
            while (recentPerformanceScores.Count > adaptiveTrainingWindow)
            {
                recentPerformanceScores.Dequeue();
            }
            
            // Calculate average recent performance
            if (recentPerformanceScores.Count >= adaptiveTrainingWindow)
            {
                float avgPerformance = recentPerformanceScores.Average();
                float normalizedPerformance = Math.Max(0f, Math.Min(1f, avgPerformance / 100f));
                
                // Adjust learning rate based on performance
                if (normalizedPerformance > performanceThreshold)
                {
                    // Good performance - reduce learning rate for stability
                    config.LearningRate = Math.Max(0.0001f, config.LearningRate * 0.95f);
                    config.ExplorationRate = Math.Max(0.05f, config.ExplorationRate * 0.98f);
                }
                else
                {
                    // Poor performance - increase learning rate for faster adaptation
                    config.LearningRate = Math.Min(0.01f, config.LearningRate * 1.02f);
                    config.ExplorationRate = Math.Min(0.3f, config.ExplorationRate * 1.01f);
                }
                
                Console.WriteLine($"🔧 Adaptive training: LR={config.LearningRate:F6}, Exploration={config.ExplorationRate:F3}, AvgPerf={avgPerformance:F2}");
            }
        }
    }
}
