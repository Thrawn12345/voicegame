using System;
using System.Drawing;

namespace VoiceGame
{
    // Records for immutable data structures representing game entities
    public record Player(PointF Position, PointF Velocity, int Health = 3, PointF? TargetPosition = null, bool IsMovingToTarget = false);

    public record Laser(PointF Position, PointF Velocity);

    public record Enemy(PointF Position, float Speed, DateTime LastShotTime, EnemyBehavior Behavior, DateTime LastBehaviorChange, int LearningId);

    // Boss enemy with multiple lives and enhanced abilities
    public record Boss(PointF Position, float Speed, DateTime LastShotTime, EnemyBehavior Behavior, DateTime LastBehaviorChange, int Health, int MaxHealth, DateTime LastSpecialAttack)
    {        public PointF Velocity { get; init; } = PointF.Empty;
        public int LearningId { get; init; } = -1;
    }

    public record EnemyBullet(PointF Position, PointF Velocity);
    public record Obstacle(PointF Position, SizeF Size);
    
    // Environmental hazards
    public record EnvironmentalHazard(PointF Position, SizeF Size, HazardType Type, bool IsActive = true, DateTime LastActivation = default);
    public record MovingObstacle(PointF Position, SizeF Size, PointF Velocity, PointF[] PatrolPoints, int CurrentTarget = 0);

    // Companion AI characters with health system
    public record Companion(PointF Position, PointF Velocity, int Id, CompanionRole Role, PointF FormationTarget, DateTime LastShotTime, int Health = 3);

    // Enemy behavior types
    public enum EnemyBehavior
    {
        Aggressive,  // Direct approach to player
        Flanking,    // Try to circle around player
        Cautious,    // Keep distance and shoot
        Ambush,      // Hide behind obstacles and wait
        BossRampage, // Boss special aggressive mode
        BossDefensive // Boss defensive with rapid fire
    }
    
    // Environmental hazard types
    public enum HazardType
    {
        ElectricField, // Periodic electrical damage
        Spikes,        // Damage on contact
        Lava,          // Continuous damage while in area
        MovingCrusher  // Mobile damage zone
    }
    
    // Curriculum learning phases
    public enum TrainingPhase
    {
        BasicMovement,    // Static targets, no enemies
        SlowEnemies,      // Slow moving enemies
        NormalDifficulty, // Standard gameplay
        AdvancedTactics,  // Complex scenarios
        MasterLevel       // Maximum difficulty
    }

    // Companion roles for formation tactics
    public enum CompanionRole
    {
        LeftFlank,   // Covers left side of player
        RightFlank,  // Covers right side of player
        Rear         // Covers rear and provides support
    }

    // Formation types based on situation
    public enum FormationType
    {
        Line,        // Horizontal line formation
        Wedge,       // V-formation for offense
        Diamond,     // Diamond formation for defense
        Circle,      // Circular formation around player
        Adaptive     // AI chooses best formation
    }

    // Game state snapshot for AI training
    public record GameStateSnapshot(
        float PlayerHealth,
        int EnemiesDestroyed,
        int BulletsDestroyed,
        DateTime Timestamp,
        bool PerfectRun = false,
        int AliveCompanions = 0,
        bool GameContinuesAfterPlayerDeath = false,
        int BossesDefeated = 0,
        bool BossActive = false,
        // Enhanced training metrics
        float DamagePerMinute = 0f,
        float BulletEfficiency = 0f, // hits/shots ratio
        float SpatialCoverage = 0f, // % of map explored
        float TeamCoordination = 0f, // coordination effectiveness score
        int NearMisses = 0, // proximity rewards earned
        float CuriosityScore = 0f, // exploration bonus
        int EnvironmentalHazardsAvoided = 0,
        float AverageDistanceFromEdges = 0f,
        int FormationChanges = 0
    );
}