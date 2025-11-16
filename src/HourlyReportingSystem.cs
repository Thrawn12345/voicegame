using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceGame
{
    /// <summary>
    /// Automated reporting system that generates hourly JSON reports of training progress,
    /// AI performance metrics, and system statistics.
    /// </summary>
    public class HourlyReportingSystem
    {
        private System.Threading.Timer? reportTimer;
        private DateTime sessionStartTime;
        private string reportsDirectory;
        private TrainingMetrics currentMetrics;
        private readonly object metricsLock = new object();

        public class HourlyReport
        {
            [JsonPropertyName("report_timestamp")]
            public DateTime ReportTimestamp { get; set; }

            [JsonPropertyName("session_start_time")]
            public DateTime SessionStartTime { get; set; }

            [JsonPropertyName("elapsed_hours")]
            public double ElapsedHours { get; set; }

            [JsonPropertyName("training_type")]
            public string TrainingType { get; set; } = "";

            [JsonPropertyName("episodes_completed")]
            public int EpisodesCompleted { get; set; }

            [JsonPropertyName("total_experiences")]
            public int TotalExperiences { get; set; }

            [JsonPropertyName("shooting_performance")]
            public ShootingMetrics? ShootingPerformance { get; set; }

            [JsonPropertyName("dodging_performance")]
            public DodgingMetrics? DodgingPerformance { get; set; }

            [JsonPropertyName("ai_learning_progress")]
            public AILearningMetrics AILearningProgress { get; set; } = new();

            [JsonPropertyName("companion_statistics")]
            public CompanionStatistics CompanionStats { get; set; } = new();

            [JsonPropertyName("system_performance")]
            public SystemPerformance SystemPerf { get; set; } = new();

            [JsonPropertyName("recent_achievements")]
            public List<string> RecentAchievements { get; set; } = new();

            [JsonPropertyName("training_recommendations")]
            public List<string> TrainingRecommendations { get; set; } = new();
        }

        public class ShootingMetrics
        {
            [JsonPropertyName("current_accuracy")]
            public float CurrentAccuracy { get; set; }

            [JsonPropertyName("average_accuracy")]
            public float AverageAccuracy { get; set; }

            [JsonPropertyName("best_accuracy")]
            public float BestAccuracy { get; set; }

            [JsonPropertyName("total_shots_fired")]
            public int TotalShotsFired { get; set; }

            [JsonPropertyName("total_hits")]
            public int TotalHits { get; set; }

            [JsonPropertyName("accuracy_trend")]
            public string AccuracyTrend { get; set; } = "";
        }

        public class DodgingMetrics
        {
            [JsonPropertyName("current_dodge_rate")]
            public float CurrentDodgeRate { get; set; }

            [JsonPropertyName("average_dodge_rate")]
            public float AverageDodgeRate { get; set; }

            [JsonPropertyName("best_dodge_rate")]
            public float BestDodgeRate { get; set; }

            [JsonPropertyName("total_dodge_attempts")]
            public int TotalDodgeAttempts { get; set; }

            [JsonPropertyName("successful_dodges")]
            public int SuccessfulDodges { get; set; }

            [JsonPropertyName("dodge_trend")]
            public string DodgeTrend { get; set; } = "";
        }

        public class AILearningMetrics
        {
            [JsonPropertyName("neural_network_loss")]
            public float NeuralNetworkLoss { get; set; }

            [JsonPropertyName("exploration_rate")]
            public float ExplorationRate { get; set; }

            [JsonPropertyName("learning_rate")]
            public float LearningRate { get; set; }

            [JsonPropertyName("training_cycles_completed")]
            public int TrainingCyclesCompleted { get; set; }

            [JsonPropertyName("enemy_ai_improvement")]
            public float EnemyAIImprovement { get; set; }

            [JsonPropertyName("convergence_status")]
            public string ConvergenceStatus { get; set; } = "";
        }

        public class CompanionStatistics
        {
            [JsonPropertyName("active_companions")]
            public int ActiveCompanions { get; set; }

            [JsonPropertyName("companion_survival_rate")]
            public float CompanionSurvivalRate { get; set; }

            [JsonPropertyName("formation_effectiveness")]
            public float FormationEffectiveness { get; set; }

            [JsonPropertyName("coordination_score")]
            public float CoordinationScore { get; set; }

            [JsonPropertyName("fire_support_frequency")]
            public float FireSupportFrequency { get; set; }
        }

        public class SystemPerformance
        {
            [JsonPropertyName("episodes_per_hour")]
            public float EpisodesPerHour { get; set; }

            [JsonPropertyName("experiences_per_hour")]
            public float ExperiencesPerHour { get; set; }

            [JsonPropertyName("memory_usage_mb")]
            public long MemoryUsageMB { get; set; }

            [JsonPropertyName("training_efficiency")]
            public float TrainingEfficiency { get; set; }

            [JsonPropertyName("estimated_completion_hours")]
            public double EstimatedCompletionHours { get; set; }
        }

        public class TrainingMetrics
        {
            public string TrainingType { get; set; } = "";
            public int EpisodesCompleted { get; set; }
            public int TotalExperiences { get; set; }
            public List<float> ShootingAccuracyHistory { get; set; } = new();
            public List<float> DodgingSuccessHistory { get; set; } = new();
            public int TotalShotsFired { get; set; }
            public int TotalHits { get; set; }
            public int TotalDodgeAttempts { get; set; }
            public int SuccessfulDodges { get; set; }
            public int TrainingCyclesCompleted { get; set; }
            public float CurrentNeuralNetworkLoss { get; set; }
            public float EnemyAIImprovement { get; set; }
            public List<string> RecentAchievements { get; set; } = new();
        }

        public HourlyReportingSystem(string? customReportsDirectory = null)
        {
            sessionStartTime = DateTime.UtcNow;
            reportsDirectory = customReportsDirectory ?? Path.Combine(Environment.CurrentDirectory, "training_reports");
            currentMetrics = new TrainingMetrics();

            // Create reports directory if it doesn't exist
            Directory.CreateDirectory(reportsDirectory);

            Console.WriteLine($"📊 Hourly reporting system initialized");
            Console.WriteLine($"📁 Reports will be saved to: {reportsDirectory}");
        }

        /// <summary>
        /// Starts the hourly reporting timer.
        /// </summary>
        public void StartReporting()
        {
            // Calculate time until next hour
            var now = DateTime.UtcNow;
            var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
            var timeUntilNextHour = nextHour - now;

            Console.WriteLine($"⏰ First report scheduled for: {nextHour:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"⌛ Time until first report: {timeUntilNextHour.TotalMinutes:F0} minutes");

            // Start timer for hourly reports
            reportTimer = new System.Threading.Timer(GenerateHourlyReport, null, timeUntilNextHour, TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Stops the hourly reporting system.
        /// </summary>
        public void StopReporting()
        {
            reportTimer?.Dispose();
            reportTimer = null;
            
            // Generate final report
            GenerateHourlyReport(null);
            Console.WriteLine("📊 Hourly reporting system stopped. Final report generated.");
        }

        /// <summary>
        /// Updates training metrics from external systems.
        /// </summary>
        public void UpdateMetrics(
            string trainingType,
            int episodesCompleted,
            int totalExperiences,
            float? shootingAccuracy = null,
            float? dodgingSuccess = null,
            int? shotsFired = null,
            int? hits = null,
            int? dodgeAttempts = null,
            int? successfulDodges = null,
            int? trainingCycles = null,
            float? neuralNetworkLoss = null,
            float? enemyAIImprovement = null,
            string? achievement = null)
        {
            lock (metricsLock)
            {
                currentMetrics.TrainingType = trainingType;
                currentMetrics.EpisodesCompleted = episodesCompleted;
                currentMetrics.TotalExperiences = totalExperiences;

                if (shootingAccuracy.HasValue)
                    currentMetrics.ShootingAccuracyHistory.Add(shootingAccuracy.Value);

                if (dodgingSuccess.HasValue)
                    currentMetrics.DodgingSuccessHistory.Add(dodgingSuccess.Value);

                if (shotsFired.HasValue)
                    currentMetrics.TotalShotsFired += shotsFired.Value;

                if (hits.HasValue)
                    currentMetrics.TotalHits += hits.Value;

                if (dodgeAttempts.HasValue)
                    currentMetrics.TotalDodgeAttempts += dodgeAttempts.Value;

                if (successfulDodges.HasValue)
                    currentMetrics.SuccessfulDodges += successfulDodges.Value;

                if (trainingCycles.HasValue)
                    currentMetrics.TrainingCyclesCompleted = trainingCycles.Value;

                if (neuralNetworkLoss.HasValue)
                    currentMetrics.CurrentNeuralNetworkLoss = neuralNetworkLoss.Value;

                if (enemyAIImprovement.HasValue)
                    currentMetrics.EnemyAIImprovement = enemyAIImprovement.Value;

                if (!string.IsNullOrEmpty(achievement))
                {
                    currentMetrics.RecentAchievements.Add($"{DateTime.UtcNow:HH:mm:ss}: {achievement}");
                    // Keep only last 10 achievements
                    if (currentMetrics.RecentAchievements.Count > 10)
                        currentMetrics.RecentAchievements.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Generates and saves an hourly report.
        /// </summary>
        private void GenerateHourlyReport(object? state)
        {
            try
            {
                var report = CreateReport();
                var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                var fileName = $"training_report_{report.ReportTimestamp:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(reportsDirectory, fileName);

                File.WriteAllText(filePath, reportJson);

                Console.WriteLine($"\n📊 HOURLY REPORT GENERATED 📊");
                Console.WriteLine($"📁 Saved to: {fileName}");
                Console.WriteLine($"⏱️  Training Time: {report.ElapsedHours:F2} hours");
                Console.WriteLine($"🎯 Episodes: {report.EpisodesCompleted:N0}");
                Console.WriteLine($"📈 Experiences: {report.TotalExperiences:N0}");

                if (report.ShootingPerformance != null)
                {
                    Console.WriteLine($"🎯 Shooting Accuracy: {report.ShootingPerformance.CurrentAccuracy:P1} ({report.ShootingPerformance.AccuracyTrend})");
                }

                if (report.DodgingPerformance != null)
                {
                    Console.WriteLine($"🏃 Dodge Success: {report.DodgingPerformance.CurrentDodgeRate:P1} ({report.DodgingPerformance.DodgeTrend})");
                }

                Console.WriteLine($"🤖 AI Cycles: {report.AILearningProgress.TrainingCyclesCompleted}");
                Console.WriteLine($"💻 Performance: {report.SystemPerf.EpisodesPerHour:F0} episodes/hour\n");

                // Print recent achievements
                if (report.RecentAchievements.Count > 0)
                {
                    Console.WriteLine("🏆 Recent Achievements:");
                    foreach (var achievement in report.RecentAchievements.TakeLast(3))
                    {
                        Console.WriteLine($"   • {achievement}");
                    }
                    Console.WriteLine();
                }

                // Print training recommendations
                if (report.TrainingRecommendations.Count > 0)
                {
                    Console.WriteLine("💡 Training Recommendations:");
                    foreach (var recommendation in report.TrainingRecommendations)
                    {
                        Console.WriteLine($"   • {recommendation}");
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating hourly report: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a comprehensive training report.
        /// </summary>
        private HourlyReport CreateReport()
        {
            var now = DateTime.UtcNow;
            var elapsedTime = now - sessionStartTime;

            HourlyReport report;
            lock (metricsLock)
            {
                report = new HourlyReport
                {
                    ReportTimestamp = now,
                    SessionStartTime = sessionStartTime,
                    ElapsedHours = elapsedTime.TotalHours,
                    TrainingType = currentMetrics.TrainingType,
                    EpisodesCompleted = currentMetrics.EpisodesCompleted,
                    TotalExperiences = currentMetrics.TotalExperiences,
                    RecentAchievements = new List<string>(currentMetrics.RecentAchievements)
                };

                // Shooting performance metrics
                if (currentMetrics.ShootingAccuracyHistory.Count > 0)
                {
                    var currentAccuracy = currentMetrics.ShootingAccuracyHistory.LastOrDefault();
                    var avgAccuracy = currentMetrics.ShootingAccuracyHistory.Average();
                    var bestAccuracy = currentMetrics.ShootingAccuracyHistory.Max();
                    var trend = CalculateTrend(currentMetrics.ShootingAccuracyHistory);

                    report.ShootingPerformance = new ShootingMetrics
                    {
                        CurrentAccuracy = currentAccuracy,
                        AverageAccuracy = avgAccuracy,
                        BestAccuracy = bestAccuracy,
                        TotalShotsFired = currentMetrics.TotalShotsFired,
                        TotalHits = currentMetrics.TotalHits,
                        AccuracyTrend = trend
                    };
                }

                // Dodging performance metrics
                if (currentMetrics.DodgingSuccessHistory.Count > 0)
                {
                    var currentDodgeRate = currentMetrics.DodgingSuccessHistory.LastOrDefault();
                    var avgDodgeRate = currentMetrics.DodgingSuccessHistory.Average();
                    var bestDodgeRate = currentMetrics.DodgingSuccessHistory.Max();
                    var trend = CalculateTrend(currentMetrics.DodgingSuccessHistory);

                    report.DodgingPerformance = new DodgingMetrics
                    {
                        CurrentDodgeRate = currentDodgeRate,
                        AverageDodgeRate = avgDodgeRate,
                        BestDodgeRate = bestDodgeRate,
                        TotalDodgeAttempts = currentMetrics.TotalDodgeAttempts,
                        SuccessfulDodges = currentMetrics.SuccessfulDodges,
                        DodgeTrend = trend
                    };
                }

                // AI Learning metrics
                report.AILearningProgress = new AILearningMetrics
                {
                    NeuralNetworkLoss = currentMetrics.CurrentNeuralNetworkLoss,
                    ExplorationRate = 0.12f, // From updated config
                    LearningRate = 0.0003f, // From updated config
                    TrainingCyclesCompleted = currentMetrics.TrainingCyclesCompleted,
                    EnemyAIImprovement = currentMetrics.EnemyAIImprovement,
                    ConvergenceStatus = DetermineConvergenceStatus()
                };
            }

            // System performance metrics
            var episodesPerHour = elapsedTime.TotalHours > 0 ? report.EpisodesCompleted / (float)elapsedTime.TotalHours : 0;
            var experiencesPerHour = elapsedTime.TotalHours > 0 ? report.TotalExperiences / (float)elapsedTime.TotalHours : 0;

            report.SystemPerf = new SystemPerformance
            {
                EpisodesPerHour = episodesPerHour,
                ExperiencesPerHour = experiencesPerHour,
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024),
                TrainingEfficiency = CalculateTrainingEfficiency(episodesPerHour),
                EstimatedCompletionHours = EstimateCompletionTime(episodesPerHour)
            };

            // Companion statistics (estimated/simulated)
            report.CompanionStats = new CompanionStatistics
            {
                ActiveCompanions = 3,
                CompanionSurvivalRate = 0.85f + (report.TrainingRecommendations.Count * 0.02f),
                FormationEffectiveness = Math.Min(0.95f, 0.60f + (report.AILearningProgress.TrainingCyclesCompleted * 0.01f)),
                CoordinationScore = Math.Min(0.90f, 0.50f + (report.EpisodesCompleted / 1000f)),
                FireSupportFrequency = 0.40f + (report.AILearningProgress.TrainingCyclesCompleted * 0.01f)
            };

            // Generate training recommendations
            report.TrainingRecommendations = GenerateTrainingRecommendations(report);

            return report;
        }

        private string CalculateTrend(List<float> values)
        {
            if (values.Count < 2) return "Stable";

            var recentValues = values.TakeLast(Math.Min(5, values.Count)).ToList();
            var slope = (recentValues.Last() - recentValues.First()) / (recentValues.Count - 1);

            return slope switch
            {
                > 0.02f => "Improving",
                < -0.02f => "Declining",
                _ => "Stable"
            };
        }

        private string DetermineConvergenceStatus()
        {
            lock (metricsLock)
            {
                if (currentMetrics.TrainingCyclesCompleted < 5) return "Early Training";
                if (currentMetrics.TrainingCyclesCompleted < 15) return "Learning";
                if (currentMetrics.CurrentNeuralNetworkLoss < 0.1f) return "Converging";
                return "Training";
            }
        }

        private float CalculateTrainingEfficiency(float episodesPerHour)
        {
            // Efficiency based on target of 100 episodes/hour
            return Math.Min(1.0f, episodesPerHour / 100f);
        }

        private double EstimateCompletionTime(float episodesPerHour)
        {
            if (episodesPerHour <= 0) return double.PositiveInfinity;
            
            // Estimate based on typical training goals (2000 episodes)
            var remainingEpisodes = Math.Max(0, 2000 - currentMetrics.EpisodesCompleted);
            return remainingEpisodes / episodesPerHour;
        }

        private List<string> GenerateTrainingRecommendations(HourlyReport report)
        {
            var recommendations = new List<string>();

            // Performance-based recommendations
            if (report.SystemPerf.EpisodesPerHour < 50)
            {
                recommendations.Add("Consider reducing episode length or complexity to improve throughput");
            }

            if (report.ShootingPerformance?.CurrentAccuracy < 0.4f)
            {
                recommendations.Add("Focus on shooting range training to improve accuracy");
            }

            if (report.DodgingPerformance?.CurrentDodgeRate < 0.5f)
            {
                recommendations.Add("Increase dodge training cycles to improve survival rates");
            }

            if (report.AILearningProgress.TrainingCyclesCompleted > 0 && 
                report.AILearningProgress.NeuralNetworkLoss > 0.5f)
            {
                recommendations.Add("Consider adjusting learning rate or model architecture");
            }

            if (report.SystemPerf.MemoryUsageMB > 1000)
            {
                recommendations.Add("Monitor memory usage - consider periodic garbage collection");
            }

            // Progress-based recommendations
            if (report.ElapsedHours > 2 && report.EpisodesCompleted < 100)
            {
                recommendations.Add("Training progress is slow - check for bottlenecks");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Training is progressing well - continue current approach");
            }

            return recommendations;
        }

        /// <summary>
        /// Generates an immediate report (useful for debugging or manual triggers).
        /// </summary>
        public void GenerateImmediateReport()
        {
            GenerateHourlyReport(null);
        }

        /// <summary>
        /// Gets the current reports directory path.
        /// </summary>
        public string GetReportsDirectory()
        {
            return reportsDirectory;
        }
    }
}