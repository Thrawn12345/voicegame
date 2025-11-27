using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    /// <summary>
    /// Manages the stealth phase at game start
    /// During stealth: player sneaks around, enemies have limited detection range, no shooting allowed
    /// After 15s or if detected: switches to combat mode with regular AI
    /// </summary>
    public class StealthPhaseManager
    {
        private bool isStealthPhase;
        private DateTime stealthStartTime;
        private bool wasDetected;
        private PlayerStealthMovementAgent? stealthAgent;
        private EnemyPatrolAgent? patrolAgent;

        // Area coverage tracking for patrol AI
        private float[,] areaCoverage = new float[3, 3];
        private Dictionary<int, PointF> enemyVelocities = new Dictionary<int, PointF>();

        public bool IsStealthPhase => isStealthPhase;
        public bool WasDetected => wasDetected;

        public StealthPhaseManager()
        {
            isStealthPhase = false;
            wasDetected = false;
        }

        public void StartStealthPhase(PlayerStealthMovementAgent? stealthAgent = null, EnemyPatrolAgent? patrolAgent = null)
        {
            isStealthPhase = true;
            stealthStartTime = DateTime.Now;
            wasDetected = false;
            this.stealthAgent = stealthAgent;
            this.patrolAgent = patrolAgent;
            
            // Reset coverage map
            areaCoverage = new float[3, 3];
            enemyVelocities.Clear();
            
            Console.WriteLine("🥷 Stealth phase started! Sneak past enemies for 15 seconds...");
        }

        public void Update(PointF playerPos, List<Enemy> enemies, Size windowSize)
        {
            if (!isStealthPhase) return;

            // Check time limit
            var elapsed = DateTime.Now - stealthStartTime;
            if (elapsed.TotalMilliseconds >= GameConstants.StealthPhaseDurationMs)
            {
                EndStealthPhase(false);
                Console.WriteLine("✅ Stealth phase completed! Switching to combat mode.");
                return;
            }

            // Check detection
            foreach (var enemy in enemies)
            {
                float distance = Distance(playerPos, enemy.Position);
                if (distance < GameConstants.EnemyDetectionRange)
                {
                    wasDetected = true;
                    EndStealthPhase(true);
                    Console.WriteLine("🚨 DETECTED! Switching to combat mode.");
                    return;
                }
            }
        }

        public PointF GetPlayerStealthMovement(PointF playerPos, List<Enemy> enemies, Size windowSize)
        {
            if (!isStealthPhase || stealthAgent == null)
                return PointF.Empty;

            var state = stealthAgent.EncodeState(playerPos, enemies, windowSize);
            int action = stealthAgent.SelectAction(state);
            return stealthAgent.ActionToVelocity(action);
        }

        public PointF GetEnemyPatrolMovement(int enemyId, PointF enemyPos, Size windowSize, Rectangle bounds)
        {
            if (!isStealthPhase || patrolAgent == null)
                return PointF.Empty;

            // Update coverage map
            int gridCellWidth = bounds.Width / 3;
            int gridCellHeight = bounds.Height / 3;
            int gridX = Math.Clamp((int)((enemyPos.X - bounds.Left) / gridCellWidth), 0, 2);
            int gridY = Math.Clamp((int)((enemyPos.Y - bounds.Top) / gridCellHeight), 0, 2);
            
            // Decay old coverage
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    areaCoverage[i, j] *= 0.99f;
                }
            }
            
            // Mark current cell
            areaCoverage[gridX, gridY] = Math.Min(1f, areaCoverage[gridX, gridY] + 0.1f);

            // Get current velocity
            if (!enemyVelocities.ContainsKey(enemyId))
                enemyVelocities[enemyId] = PointF.Empty;

            var state = patrolAgent.EncodeState(enemyPos, enemyVelocities[enemyId], areaCoverage, windowSize);
            int action = patrolAgent.SelectAction(state);
            var velocity = patrolAgent.ActionToVelocity(action);
            
            enemyVelocities[enemyId] = velocity;
            return velocity;
        }

        public bool ShouldAllowShooting()
        {
            return !isStealthPhase;
        }

        public int GetRemainingTimeMs()
        {
            if (!isStealthPhase) return 0;
            
            var elapsed = DateTime.Now - stealthStartTime;
            int remaining = GameConstants.StealthPhaseDurationMs - (int)elapsed.TotalMilliseconds;
            return Math.Max(0, remaining);
        }

        private void EndStealthPhase(bool detected)
        {
            isStealthPhase = false;
            wasDetected = detected;
            
            // Clear velocities
            enemyVelocities.Clear();
        }

        private float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public void DrawStealthUI(Graphics g, Size windowSize)
        {
            if (!isStealthPhase) return;

            int remainingMs = GetRemainingTimeMs();
            int remainingSec = remainingMs / 1000;

            // Draw stealth timer
            using (Font font = new Font("Arial", 24, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.Yellow))
            {
                string text = $"🥷 STEALTH MODE: {remainingSec}s";
                SizeF textSize = g.MeasureString(text, font);
                g.DrawString(text, font, textBrush, 
                    (windowSize.Width - textSize.Width) / 2, 20);
            }

            // Draw warning message
            using (Font font = new Font("Arial", 14))
            using (Brush textBrush = new SolidBrush(Color.White))
            {
                string msg = "Stay " + GameConstants.EnemyDetectionRange + "px away from enemies! No shooting allowed.";
                SizeF msgSize = g.MeasureString(msg, font);
                g.DrawString(msg, font, textBrush, 
                    (windowSize.Width - msgSize.Width) / 2, 60);
            }
        }

        public void DrawEnemyDetectionRadius(Graphics g, List<Enemy> enemies)
        {
            if (!isStealthPhase) return;

            using (Pen detectionPen = new Pen(Color.FromArgb(80, 255, 0, 0), 2))
            {
                detectionPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                
                foreach (var enemy in enemies)
                {
                    float radius = GameConstants.EnemyDetectionRange;
                    g.DrawEllipse(detectionPen, 
                        enemy.Position.X - radius, 
                        enemy.Position.Y - radius, 
                        radius * 2, 
                        radius * 2);
                }
            }
        }
    }
}
