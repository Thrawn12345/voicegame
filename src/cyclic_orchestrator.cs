using System;
using System.Drawing;
using System.Threading;

namespace VoiceGame
{
    /// <summary>
    /// Cyclic Training Orchestrator - trains all 11 AI agents one at a time in sequence
    /// </summary>
    public class CyclicTrainingOrchestrator
    {
        private PlayerMovementAgent playerMovement;
        private PlayerShootingAgent playerShooting;
        private PlayerStealthMovementAgent playerStealthMovement;
        private CompanionMovementAgent companionMovement;
        private CompanionShootingAgent companionShooting;
        private CompanionSoloMovementAgent companionSoloMovement;
        private EnemyMovementAgent enemyMovement;
        private EnemyShootingAgent enemyShooting;
        private EnemyPatrolAgent enemyPatrol;
        private BossMovementAgent bossMovement;
        private BossShootingAgent bossShooting;

        private TrainingRangeSystem rangeSystem;
        private Size windowSize;

        public CyclicTrainingOrchestrator(Size windowSize)
        {
            this.windowSize = windowSize;

            // Initialize all 11 agents
            playerMovement = new PlayerMovementAgent();
            playerShooting = new PlayerShootingAgent();
            playerStealthMovement = new PlayerStealthMovementAgent();
            companionMovement = new CompanionMovementAgent();
            companionShooting = new CompanionShootingAgent();
            companionSoloMovement = new CompanionSoloMovementAgent();
            enemyMovement = new EnemyMovementAgent();
            enemyShooting = new EnemyShootingAgent();
            enemyPatrol = new EnemyPatrolAgent();
            bossMovement = new BossMovementAgent();
            bossShooting = new BossShootingAgent();

            rangeSystem = new TrainingRangeSystem(windowSize);

            // Try to load existing models
            LoadModels();

            Console.WriteLine("🎓 Cyclic Training Orchestrator initialized with 11 AI agents");
        }

        public void RunCyclicTraining(CancellationToken cancellationToken, int episodesPerCycle = 50)
        {
            int cycleNumber = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                cycleNumber++;
                Console.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"  TRAINING CYCLE #{cycleNumber}");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                // 1. Train Player Movement (dodge bullets)
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("🎮 Training Player Movement (dodge training)...");
                    var playerMoveTraining = new TrainingRangeSystem.PlayerMovementTraining(playerMovement);
                    var range = rangeSystem.GetRange("PlayerMovement");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        playerMoveTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Player Movement training complete");
                }

                // 2. Train Player Shooting
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n🎯 Training Player Shooting...");
                    var playerShootTraining = new TrainingRangeSystem.PlayerShootingTraining(playerShooting, enemyMovement);
                    var range = rangeSystem.GetRange("PlayerShooting");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        playerShootTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Player Shooting training complete");
                }

                // 3. Train Companion Movement
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n🤖 Training Companion Movement (with simulated player)...");
                    var companionMoveTraining = new TrainingRangeSystem.CompanionMovementTraining(companionMovement);
                    var range = rangeSystem.GetRange("CompanionMovement");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        companionMoveTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Companion Movement training complete");
                }

                // 4. Train Companion Shooting
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n🎯 Training Companion Shooting...");
                    var companionShootTraining = new TrainingRangeSystem.CompanionShootingTraining(companionShooting);
                    var range = rangeSystem.GetRange("CompanionShooting");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        companionShootTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Companion Shooting training complete");
                }

                // 5. Train Companion Solo Movement (when player is dead)
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n💀 Training Companion Solo Movement (autonomous survival)...");
                    var companionSoloTraining = new TrainingRangeSystem.CompanionSoloMovementTraining(companionSoloMovement);
                    var range = rangeSystem.GetRange("CompanionSoloMovement");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        companionSoloTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Companion Solo Movement training complete");
                }

                // 6. Train Enemy Movement
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n👾 Training Enemy Movement...");
                    var enemyMoveTraining = new TrainingRangeSystem.EnemyMovementTraining(enemyMovement);
                    var range = rangeSystem.GetRange("EnemyMovement");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        enemyMoveTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Enemy Movement training complete");
                }

                // 7. Train Enemy Shooting
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n👾 Training Enemy Shooting...");
                    var enemyShootTraining = new TrainingRangeSystem.EnemyShootingTraining(enemyShooting);
                    var range = rangeSystem.GetRange("EnemyShooting");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        enemyShootTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Enemy Shooting training complete");
                }

                // 8. Train Boss Movement
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n👹 Training Boss Movement...");
                    var bossMoveTraining = new TrainingRangeSystem.BossMovementTraining(bossMovement);
                    var range = rangeSystem.GetRange("BossMovement");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        bossMoveTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Boss Movement training complete");
                }

                // 9. Train Boss Shooting
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n👹 Training Boss Shooting...");
                    var bossShootTraining = new TrainingRangeSystem.BossShootingTraining(bossShooting);
                    var range = rangeSystem.GetRange("BossShooting");
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        bossShootTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Boss Shooting training complete");
                }

                // 10. Train Player Stealth Movement
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n🥷 Training Player Stealth Movement (sneak past enemies)...");
                    var stealthTraining = new TrainingRangeSystem.PlayerStealthMovementTraining(playerStealthMovement);
                    var range = rangeSystem.GetRange("PlayerMovement"); // Reuse player movement range
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        stealthTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Player Stealth Movement training complete");
                }

                // 11. Train Enemy Patrol (area coverage)
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n🚶 Training Enemy Patrol (area coverage)...");
                    var patrolTraining = new TrainingRangeSystem.EnemyPatrolTraining(enemyPatrol);
                    var range = rangeSystem.GetRange("EnemyMovement"); // Reuse enemy movement range
                    
                    for (int i = 0; i < episodesPerCycle; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        patrolTraining.RunEpisode(range, windowSize);
                        
                        if ((i + 1) % 10 == 0)
                            Console.WriteLine($"  Episode {i + 1}/{episodesPerCycle}");
                    }
                    Console.WriteLine("✅ Enemy Patrol training complete");
                }

                // Save models after each cycle
                if (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("\n💾 Saving models...");
                    SaveModels();
                    Console.WriteLine($"✅ Cycle #{cycleNumber} complete! All models saved.\n");
                }
            }

            Console.WriteLine("\n🛑 Training stopped. Final save...");
            SaveModels();
            Console.WriteLine("✅ All models saved!");
        }

        private void LoadModels()
        {
            try
            {
                string modelsDir = "models";
                if (System.IO.Directory.Exists(modelsDir))
                {
                    TryLoadModel(playerMovement, "models/player_movement_model.json", "Player Movement");
                    TryLoadModel(playerShooting, "models/player_shooting_model.json", "Player Shooting");
                    TryLoadModel(playerStealthMovement, "models/player_stealth_movement_model.json", "Player Stealth Movement");
                    TryLoadModel(companionMovement, "models/companion_movement_model.json", "Companion Movement");
                    TryLoadModel(companionShooting, "models/companion_shooting_model.json", "Companion Shooting");
                    TryLoadModel(companionSoloMovement, "models/companion_solo_movement_model.json", "Companion Solo Movement");
                    TryLoadModel(enemyMovement, "models/enemy_movement_model.json", "Enemy Movement");
                    TryLoadModel(enemyShooting, "models/enemy_shooting_model.json", "Enemy Shooting");
                    TryLoadModel(enemyPatrol, "models/enemy_patrol_model.json", "Enemy Patrol");
                    TryLoadModel(bossMovement, "models/boss_movement_model.json", "Boss Movement");
                    TryLoadModel(bossShooting, "models/boss_shooting_model.json", "Boss Shooting");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error loading models: {ex.Message}");
            }
        }

        private void TryLoadModel<T>(T agent, string path, string name)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var method = agent?.GetType().GetMethod("LoadModel");
                    method?.Invoke(agent, new object[] { path });
                    Console.WriteLine($"✅ Loaded {name} model");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not load {name}: {ex.Message}");
            }
        }

        private void SaveModels()
        {
            try
            {
                string modelsDir = "models";
                if (!System.IO.Directory.Exists(modelsDir))
                {
                    System.IO.Directory.CreateDirectory(modelsDir);
                }

                playerMovement.SaveModel("models/player_movement_model.json");
                playerShooting.SaveModel("models/player_shooting_model.json");
                playerStealthMovement.SaveModel("models/player_stealth_movement_model.json");
                companionMovement.SaveModel("models/companion_movement_model.json");
                companionShooting.SaveModel("models/companion_shooting_model.json");
                companionSoloMovement.SaveModel("models/companion_solo_movement_model.json");
                enemyMovement.SaveModel("models/enemy_movement_model.json");
                enemyShooting.SaveModel("models/enemy_shooting_model.json");
                enemyPatrol.SaveModel("models/enemy_patrol_model.json");
                bossMovement.SaveModel("models/boss_movement_model.json");
                bossShooting.SaveModel("models/boss_shooting_model.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error saving models: {ex.Message}");
            }
        }

        // Getters for agents (to use in game mode)
        public PlayerMovementAgent GetPlayerMovement() => playerMovement;
        public PlayerShootingAgent GetPlayerShooting() => playerShooting;
        public PlayerStealthMovementAgent GetPlayerStealthMovement() => playerStealthMovement;
        public CompanionMovementAgent GetCompanionMovement() => companionMovement;
        public CompanionShootingAgent GetCompanionShooting() => companionShooting;
        public CompanionSoloMovementAgent GetCompanionSoloMovement() => companionSoloMovement;
        public EnemyMovementAgent GetEnemyMovement() => enemyMovement;
        public EnemyShootingAgent GetEnemyShooting() => enemyShooting;
        public EnemyPatrolAgent GetEnemyPatrol() => enemyPatrol;
        public BossMovementAgent GetBossMovement() => bossMovement;
        public BossShootingAgent GetBossShooting() => bossShooting;
    }
}
