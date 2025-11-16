using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VoiceGame
{
    public class ObstacleManager
    {
        private readonly List<Obstacle> obstacles = new();
        private readonly Random random = new();

        public IReadOnlyList<Obstacle> Obstacles => obstacles;

        /// <summary>
        /// Get obstacles as a list for pathfinding calculations.
        /// </summary>
        public List<Obstacle> GetObstacles() => obstacles.ToList();

        public void GenerateObstacles(Size screenSize, PointF playerPosition)
        {
            obstacles.Clear();

            // Scale obstacle count based on screen size
            int baseObstacles = 8;
            int screenArea = screenSize.Width * screenSize.Height;
            int standardArea = 800 * 600; // Reference screen size
            
            float scaleFactor = (float)screenArea / standardArea;
            int maxObstacles = (int)(baseObstacles * Math.Sqrt(scaleFactor)) + 3;
            int obstacleCount = random.Next(baseObstacles, Math.Max(baseObstacles + 1, maxObstacles + 1));
            
            Console.WriteLine($"🏗️ Generating {obstacleCount} obstacles for {screenSize.Width}x{screenSize.Height} screen");

            // Divide screen into grid for better distribution
            int gridCols = (int)Math.Ceiling(Math.Sqrt(obstacleCount * 1.2));
            int gridRows = (int)Math.Ceiling((float)obstacleCount / gridCols);
            float cellWidth = (float)screenSize.Width / gridCols;
            float cellHeight = (float)screenSize.Height / gridRows;

            for (int i = 0; i < obstacleCount; i++)
            {
                Obstacle obstacle;
                int attempts = 0;
                const int maxAttempts = 50;

                do
                {
                    // Calculate grid position for this obstacle
                    int gridX = i % gridCols;
                    int gridY = i / gridCols;
                    
                    // Random size between 25x25 and 65x65 (smaller for better distribution)
                    float width = random.Next(25, 66);
                    float height = random.Next(25, 66);

                    // Position within grid cell with some randomness
                    float cellOffsetX = random.Next(10, (int)(cellWidth * 0.7f));
                    float cellOffsetY = random.Next(10, (int)(cellHeight * 0.7f));
                    
                    float x = gridX * cellWidth + cellOffsetX;
                    float y = gridY * cellHeight + cellOffsetY;
                    
                    // Ensure stays within bounds
                    x = Math.Max(40, Math.Min(x, screenSize.Width - width - 40));
                    y = Math.Max(40, Math.Min(y, screenSize.Height - height - 40));

                    obstacle = new Obstacle(new PointF(x, y), new SizeF(width, height));
                    attempts++;

                    if (attempts >= maxAttempts)
                        break;

                } while (IsObstacleInvalid(obstacle, playerPosition, screenSize));

                obstacles.Add(obstacle);
            }

            Console.WriteLine($"🏗️ Generated {obstacles.Count} obstacles");
        }

        private bool IsObstacleInvalid(Obstacle obstacle, PointF playerPosition, Size screenSize)
        {
            // Check if too close to player spawn (150 pixel radius)
            var obstacleCenter = new PointF(
                obstacle.Position.X + obstacle.Size.Width / 2,
                obstacle.Position.Y + obstacle.Size.Height / 2
            );

            if (Distance(obstacleCenter, playerPosition) < 150)
                return true;

            // Check if overlapping with existing obstacles (with 20 pixel buffer)
            foreach (var existing in obstacles)
            {
                if (ObstaclesOverlap(obstacle, existing, 20))
                    return true;
            }

            return false;
        }

        private bool ObstaclesOverlap(Obstacle a, Obstacle b, float buffer)
        {
            return !(a.Position.X + a.Size.Width + buffer < b.Position.X ||
                     b.Position.X + b.Size.Width + buffer < a.Position.X ||
                     a.Position.Y + a.Size.Height + buffer < b.Position.Y ||
                     b.Position.Y + b.Size.Height + buffer < a.Position.Y);
        }

        public bool CheckPointCollision(PointF point, float radius = 0)
        {
            foreach (var obstacle in obstacles)
            {
                if (point.X + radius > obstacle.Position.X &&
                    point.X - radius < obstacle.Position.X + obstacle.Size.Width &&
                    point.Y + radius > obstacle.Position.Y &&
                    point.Y - radius < obstacle.Position.Y + obstacle.Size.Height)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CheckRectangleCollision(PointF position, SizeF size)
        {
            foreach (var obstacle in obstacles)
            {
                if (position.X < obstacle.Position.X + obstacle.Size.Width &&
                    position.X + size.Width > obstacle.Position.X &&
                    position.Y < obstacle.Position.Y + obstacle.Size.Height &&
                    position.Y + size.Height > obstacle.Position.Y)
                {
                    return true;
                }
            }
            return false;
        }

        public PointF? GetCollisionAdjustedPosition(PointF currentPos, PointF newPos, float radius)
        {
            // Check if new position would collide
            if (!CheckPointCollision(newPos, radius))
                return newPos;

            // Try horizontal movement only
            var horizontalOnly = new PointF(newPos.X, currentPos.Y);
            if (!CheckPointCollision(horizontalOnly, radius))
                return horizontalOnly;

            // Try vertical movement only
            var verticalOnly = new PointF(currentPos.X, newPos.Y);
            if (!CheckPointCollision(verticalOnly, radius))
                return verticalOnly;

            // No movement possible
            return currentPos;
        }

        public void DrawObstacles(Graphics g)
        {
            foreach (var obstacle in obstacles)
            {
                // Draw obstacle as gray rectangle with dark border
                using var brush = new SolidBrush(Color.Gray);
                using var pen = new Pen(Color.DarkGray, 2);

                var rect = new RectangleF(obstacle.Position, obstacle.Size);
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, Rectangle.Round(rect));
            }
        }

        private static float Distance(PointF p1, PointF p2)
        {
            float dx = p1.X - p2.X;
            float dy = p1.Y - p2.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}