using System;
using System.Drawing;

namespace VoiceGame
{
    // Records for immutable data structures representing game entities
    public record Player(PointF Position, PointF Velocity);

    public record Laser(PointF Position, PointF Velocity);

    public record Enemy(PointF Position, float Speed, DateTime LastShotTime, EnemyBehavior Behavior, DateTime LastBehaviorChange);

    public record EnemyBullet(PointF Position, PointF Velocity);
    public record Obstacle(PointF Position, SizeF Size);

    // Enemy behavior types
    public enum EnemyBehavior
    {
        Aggressive,  // Direct approach to player
        Flanking,    // Try to circle around player
        Cautious,    // Keep distance and shoot
        Ambush       // Hide behind obstacles and wait
    }

    // Game state snapshot for AI training
    public record GameStateSnapshot(
        float PlayerHealth,
        int EnemiesDestroyed,
        int BulletsDestroyed,
        DateTime Timestamp
    );
}