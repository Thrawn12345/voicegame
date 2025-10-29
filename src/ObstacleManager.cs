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

        public void GenerateObstacles(Size screenSize, PointF playerPosition)
        {
            obstacles.Clear();

            // Generate 5-8 random rectangular obstacles
            int obstacleCount = random.Next(5, 9);

            for (int i = 0; i < obstacleCount; i++)
            {
                Obstacle obstacle;
                int attempts = 0;
                const int maxAttempts = 50;

                do
                {
                    // Random size between 30x30 and 80x80
                    float width = random.Next(30, 81);
                    float height = random.Next(30, 81);

                    // Random position with margins from edges
                    float x = random.Next(50, screenSize.Width - 50 - (int)width);
                    float y = random.Next(50, screenSize.Height - 50 - (int)height);

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