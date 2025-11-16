using System;
using System.Drawing;

namespace VoiceGame
{
    // Records for immutable data structures representing game entities
    public record Player(PointF Position, PointF Velocity, PointF? TargetPosition = null, bool IsMovingToTarget = false);

    public record Laser(PointF Position, PointF Velocity);

    public record Enemy(PointF Position, float Speed, DateTime LastShotTime, EnemyBehavior Behavior, DateTime LastBehaviorChange, int LearningId);

    public record EnemyBullet(PointF Position, PointF Velocity);
    public record Obstacle(PointF Position, SizeF Size);

    // Companion AI characters
    public record Companion(PointF Position, PointF Velocity, int Id, CompanionRole Role, PointF FormationTarget, DateTime LastShotTime);

    // Enemy behavior types
    public enum EnemyBehavior
    {
        Aggressive,  // Direct approach to player
        Flanking,    // Try to circle around player
        Cautious,    // Keep distance and shoot
        Ambush       // Hide behind obstacles and wait
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
        DateTime Timestamp
    );
}