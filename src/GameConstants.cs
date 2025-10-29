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
        public const int EnemyBulletSpeed = LaserSpeed; // Same bullet speed as player
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
    }
}