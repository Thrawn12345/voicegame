using System;
using System.Collections.Generic;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Experience replay buffer for storing and sampling training experiences.
    /// Implements prioritized experience replay for better sample efficiency.
    /// </summary>
    public class ExperienceReplayBuffer
    {
        private readonly List<StoredExperience> experiences = new();
        private readonly Random random = new();
        private readonly int maxSize;
        private int currentIndex = 0;

        public ExperienceReplayBuffer(int maxSize = 100000)
        {
            this.maxSize = maxSize;
        }

        public int Size => experiences.Count;

        public class StoredExperience
        {
            public float[] State { get; set; } = Array.Empty<float>();
            public ParallelTrainingManager.PlayerAction Action { get; set; }
            public float Reward { get; set; }
            public float[] NextState { get; set; } = Array.Empty<float>();
            public bool IsTerminal { get; set; }
            public float Priority { get; set; } = 1f;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public int SampleCount { get; set; } = 0;
        }

        /// <summary>
        /// Add experiences to the buffer.
        /// </summary>
        public void AddExperience(List<ParallelTrainingManager.Experience> newExperiences)
        {
            foreach (var experience in newExperiences)
            {
                var storedExperience = new StoredExperience
                {
                    State = experience.State,
                    Action = experience.Action,
                    Reward = experience.Reward,
                    NextState = experience.NextState,
                    IsTerminal = experience.IsTerminal,
                    Priority = CalculateInitialPriority(experience),
                    Timestamp = DateTime.UtcNow
                };

                if (experiences.Count < maxSize)
                {
                    experiences.Add(storedExperience);
                }
                else
                {
                    // Replace oldest experience
                    experiences[currentIndex] = storedExperience;
                    currentIndex = (currentIndex + 1) % maxSize;
                }
            }
        }

        /// <summary>
        /// Sample a batch of experiences using prioritized sampling.
        /// </summary>
        public List<StoredExperience> SampleBatch(int batchSize)
        {
            if (experiences.Count == 0) return new List<StoredExperience>();

            var batch = new List<StoredExperience>();
            var totalPriority = experiences.Sum(e => e.Priority);

            for (int i = 0; i < Math.Min(batchSize, experiences.Count); i++)
            {
                var sample = SampleByPriority(totalPriority);
                if (sample != null)
                {
                    sample.SampleCount++;
                    batch.Add(sample);
                }
            }

            return batch;
        }

        /// <summary>
        /// Sample uniformly for comparison studies.
        /// </summary>
        public List<StoredExperience> SampleUniform(int batchSize)
        {
            if (experiences.Count == 0) return new List<StoredExperience>();

            var batch = new List<StoredExperience>();
            
            for (int i = 0; i < Math.Min(batchSize, experiences.Count); i++)
            {
                var randomIndex = random.Next(experiences.Count);
                batch.Add(experiences[randomIndex]);
            }

            return batch;
        }

        /// <summary>
        /// Update priorities of experiences after training.
        /// </summary>
        public void UpdatePriorities(List<StoredExperience> experiences, List<float> tdErrors)
        {
            for (int i = 0; i < Math.Min(experiences.Count, tdErrors.Count); i++)
            {
                var experience = experiences[i];
                var tdError = Math.Abs(tdErrors[i]);
                
                // Update priority based on TD error
                experience.Priority = Math.Max(0.01f, tdError + 0.01f); // Small epsilon to avoid zero priority
            }
        }

        /// <summary>
        /// Get statistics about the replay buffer.
        /// </summary>
        public ReplayBufferStats GetStats()
        {
            if (experiences.Count == 0)
            {
                return new ReplayBufferStats();
            }

            return new ReplayBufferStats
            {
                Size = experiences.Count,
                MaxSize = maxSize,
                AverageReward = experiences.Average(e => e.Reward),
                AveragePriority = experiences.Average(e => e.Priority),
                HighPriorityCount = experiences.Count(e => e.Priority > 1f),
                OldestExperience = experiences.Min(e => e.Timestamp),
                NewestExperience = experiences.Max(e => e.Timestamp),
                TerminalExperienceCount = experiences.Count(e => e.IsTerminal)
            };
        }

        /// <summary>
        /// Clear old experiences to maintain buffer quality.
        /// </summary>
        public void ClearOldExperiences(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            experiences.RemoveAll(e => e.Timestamp < cutoffTime);
            
            if (currentIndex >= experiences.Count)
            {
                currentIndex = 0;
            }
        }

        /// <summary>
        /// Get experiences with high temporal difference errors for analysis.
        /// </summary>
        public List<StoredExperience> GetHighErrorExperiences(int count = 100)
        {
            return experiences
                .OrderByDescending(e => e.Priority)
                .Take(count)
                .ToList();
        }

        private float CalculateInitialPriority(ParallelTrainingManager.Experience experience)
        {
            // Initial priority based on reward magnitude and terminal state
            float priority = Math.Abs(experience.Reward) + 0.1f;
            
            if (experience.IsTerminal)
            {
                priority *= 2f; // Terminal states are more important
            }
            
            if (experience.Reward > 1f)
            {
                priority *= 1.5f; // High rewards are important
            }
            
            return priority;
        }

        private StoredExperience? SampleByPriority(float totalPriority)
        {
            if (totalPriority <= 0) return null;

            var randomValue = (float)random.NextDouble() * totalPriority;
            float currentSum = 0f;

            foreach (var experience in experiences)
            {
                currentSum += experience.Priority;
                if (currentSum >= randomValue)
                {
                    return experience;
                }
            }

            // Fallback to last experience
            return experiences.LastOrDefault();
        }

        public class ReplayBufferStats
        {
            public int Size { get; set; }
            public int MaxSize { get; set; }
            public float AverageReward { get; set; }
            public float AveragePriority { get; set; }
            public int HighPriorityCount { get; set; }
            public DateTime OldestExperience { get; set; }
            public DateTime NewestExperience { get; set; }
            public int TerminalExperienceCount { get; set; }
        }
    }
}