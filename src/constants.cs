namespace VoiceGame
{
    public static class GameConstants
    {
        // Player settings
        public const int PlayerSpeed = 5;
        public const int PlayerRadius = 15;

        // Laser settings
        public const int LaserSpeed = 8;

        // Enemy settings
        public const int EnemyRadius = 12;
        public const float EnemySpeed = PlayerSpeed; // Same speed as player
        public const int EnemyBulletSpeed = LaserSpeed + 4; // Faster than player bullets (12 vs 8)
        public const int EnemyShootCooldownMs = 2000; // 2 seconds between shots
        public const int EnemyShootRange = 200; // pixels
        public const int EnemyFlankingDistance = 100; // Distance for flanking behavior
        public const int EnemyRetreatDistance = 50; // Distance to maintain when retreating

        // Timer intervals
        public const int GameTimerInterval = 16; // 60 FPS
        public const int AIShootingInterval = 300; // AI decisions every 300ms
        public const int EnemySpawnInterval = 2000; // Spawn enemy every 2 seconds

        // Game settings
        public const int InitialLives = 3;
        public const float MinEnemySpawnDistance = 150f;

        // Random movement settings
        public const float RandomMovementChance = 0.05f; // 5% chance per frame
        public const int RandomMovementInterval = 1500; // Random movement every 1.5 seconds

        // AI training settings
        public const int ExperienceSaveInterval = 500;
        
        // Companion settings
        public const int CompanionShootCooldownMs = AIShootingInterval; // Same as player AI shooting rate
        public const int CompanionRadius = 12; // Same as enemy radius
        public const int CompanionHealth = 3; // 3 lives like player
        
        // Reward system
        public const float PerfectRunBonusMultiplier = 2.5f; // Extra reward for no hits
        public const float EdgePenaltyMultiplier = 0.7f; // Penalty for staying near edges
        public const float StationaryPenaltyMultiplier = 0.8f; // Penalty for not moving
        
        // Obstacle generation
        public const int MinObstacles = 8; // Minimum obstacles on map
        public const int MaxObstacles = 15; // Maximum obstacles on map
        public const int ObstacleMinSize = 30;
        public const int ObstacleMaxSize = 80;
        
        // Boss system
        public const int BossSpawnInterval = 15; // Spawn boss every 15 enemy kills
        public const int BossHealth = 5; // Boss has 5 lives
        public const int BossRadius = 25; // Larger than regular enemies
        public const float BossSpeed = PlayerSpeed * 0.8f; // Slightly slower than player
        public const int BossShootCooldownMs = 1000; // Faster shooting than regular enemies
        public const int BossSpecialAttackCooldownMs = 3000; // Special attack every 3 seconds
        
        // Enhanced reward system
        public const float ProximityRewardRange = 50f; // Range for near-miss rewards
        public const float ProximityRewardMultiplier = 0.1f; // Reward for getting close to targets
        public const float EfficiencyRewardMultiplier = 1.5f; // Reward for bullet efficiency
        public const float TeamworkRewardMultiplier = 2.0f; // Reward for coordinated actions
        public const float CuriosityRewardMultiplier = 0.2f; // Reward for exploring new areas
        
        // Progressive difficulty
        public const float DifficultyScaleRate = 0.05f; // How much difficulty increases per level
        public const int DifficultyCheckInterval = 100; // Episodes between difficulty adjustments
        
        // Environmental hazards
        public const int MinHazards = 2;
        public const int MaxHazards = 6;
        public const float HazardDamage = 0.5f; // Half damage from environmental hazards
        public const int MovingObstacleSpeed = 2;
    }
}