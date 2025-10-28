using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Forms;

// Records for immutable data structures
public record Player(PointF Position, PointF Velocity);
public record Laser(PointF Position, PointF Velocity);
public record Enemy(PointF Position, float Speed);

public class GameForm : Form
{
    private Player player;
    private readonly List<Laser> lasers = new();
    private readonly List<Enemy> enemies = new();
    private readonly System.Windows.Forms.Timer gameTimer = new();
    private readonly System.Windows.Forms.Timer laserFireTimer = new();
    private readonly System.Windows.Forms.Timer enemySpawnTimer = new();
    private SpeechRecognitionEngine? recognizer;
    private readonly SpeechSynthesizer synthesizer = new();

    private const int PlayerSpeed = 5;
    private const int LaserSpeed = 8;
    private const int PlayerRadius = 15;
    private const int EnemyRadius = 12;
    private int lives = 3;
    private bool gameOver = false;

    public GameForm()
    {
        // Form setup
        Text = "Voice Controlled Game";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        BackColor = Color.Black;

        // Initialize game objects
        player = new Player(new PointF(Width / 2, Height / 2), PointF.Empty);

        // Game loop timer (60 FPS)
        gameTimer.Interval = 16;
        gameTimer.Tick += GameTimer_Tick;
        gameTimer.Start();

        // Laser firing timer (every second)
        laserFireTimer.Interval = 1000;
        laserFireTimer.Tick += LaserFireTimer_Tick;
        laserFireTimer.Start();

        // Enemy spawn timer (every 2 seconds)
        enemySpawnTimer.Interval = 2000;
        enemySpawnTimer.Tick += EnemySpawnTimer_Tick;
        enemySpawnTimer.Start();

        // Wire up the paint event
        Paint += GameForm_Paint;

        // Initialize voice controls
        InitializeSpeechRecognition();
    }

    private void InitializeSpeechRecognition()
    {
        try
        {
            // Create the recognizer with the default system language
            recognizer = new SpeechRecognitionEngine();

            // Create commands - case insensitive matching
            var commands = new Choices(new[] 
            { 
                "north", "south", "east", "west", 
                "north east", "north west", "south east", "south west", 
                "stop" 
            });
            var grammar = new Grammar(new GrammarBuilder(commands));
            
            // Set culture to en-US for consistency
            try
            {
                recognizer.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", 70);
            }
            catch { /* Setting may not be available */ }

            recognizer.LoadGrammar(grammar);
            recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
            recognizer.SpeechRecognitionRejected += Recognizer_SpeechRejected;
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);

            Console.WriteLine("✅ Speech recognition initialized");
            synthesizer.SpeakAsync("Voice control enabled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Speech recognition error: {ex.Message}");
            MessageBox.Show($"Could not initialize speech recognition: {ex.Message}\n\nUsing keyboard controls instead.", "Speech Error");
            recognizer?.Dispose();
            recognizer = null;
        }
    }

    private void Recognizer_SpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        Console.WriteLine($"❌ Speech rejected (confidence too low): {e.Result?.Text ?? "unknown"}");
    }

    private void Recognizer_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        // Only process if confidence is high enough (70%)
        if (e.Result.Confidence < 0.7f)
        {
            Console.WriteLine($"⚠️ Low confidence ({e.Result.Confidence:P0}): {e.Result.Text}");
            return;
        }

        var command = e.Result.Text.ToLower().Trim();
        Console.WriteLine($"🎤 Recognized ({e.Result.Confidence:P0}): {command}");
        var newVelocity = PointF.Empty;

        switch (command)
        {
            case "north":
                newVelocity = new PointF(0, -PlayerSpeed);
                Console.WriteLine("✅ Moving NORTH");
                break;
            case "south":
                newVelocity = new PointF(0, PlayerSpeed);
                Console.WriteLine("✅ Moving SOUTH");
                break;
            case "east":
                newVelocity = new PointF(PlayerSpeed, 0);
                Console.WriteLine("✅ Moving EAST");
                break;
            case "west":
                newVelocity = new PointF(-PlayerSpeed, 0);
                Console.WriteLine("✅ Moving WEST");
                break;
            case "north east":
                newVelocity = new PointF(PlayerSpeed, -PlayerSpeed);
                Console.WriteLine("✅ Moving NORTH EAST");
                break;
            case "north west":
                newVelocity = new PointF(-PlayerSpeed, -PlayerSpeed);
                Console.WriteLine("✅ Moving NORTH WEST");
                break;
            case "south east":
                newVelocity = new PointF(PlayerSpeed, PlayerSpeed);
                Console.WriteLine("✅ Moving SOUTH EAST");
                break;
            case "south west":
                newVelocity = new PointF(-PlayerSpeed, PlayerSpeed);
                Console.WriteLine("✅ Moving SOUTH WEST");
                break;
            case "stop":
                newVelocity = PointF.Empty;
                Console.WriteLine("✅ STOPPED");
                break;
            default:
                Console.WriteLine($"❌ Unknown command: {command}");
                break;
        }
        player = player with { Velocity = newVelocity };
    }

    private void GameTimer_Tick(object? sender, EventArgs e)
    {
        if (gameOver) return;

        // Update player position
        var newPlayerPos = new PointF(player.Position.X + player.Velocity.X, player.Position.Y + player.Velocity.Y);

        // Keep player within bounds
        if (newPlayerPos.X < 0) newPlayerPos.X = 0;
        if (newPlayerPos.X > ClientSize.Width) newPlayerPos.X = ClientSize.Width;
        if (newPlayerPos.Y < 0) newPlayerPos.Y = 0;
        if (newPlayerPos.Y > ClientSize.Height) newPlayerPos.Y = ClientSize.Height;
        
        player = player with { Position = newPlayerPos };

        // Update laser positions and handle bouncing
        for (int i = lasers.Count - 1; i >= 0; i--)
        {
            var laser = lasers[i];
            var newPos = new PointF(laser.Position.X + laser.Velocity.X, laser.Position.Y + laser.Velocity.Y);
            var newVel = laser.Velocity;

            if (newPos.X <= 0 || newPos.X >= ClientSize.Width)
            {
                newVel.X = -newVel.X;
            }
            if (newPos.Y <= 0 || newPos.Y >= ClientSize.Height)
            {
                newVel.Y = -newVel.Y;
            }

            lasers[i] = laser with { Position = newPos, Velocity = newVel };
        }

        // Update enemy positions and check collisions
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            
            // Move enemy toward player
            float dx = player.Position.X - enemy.Position.X;
            float dy = player.Position.Y - enemy.Position.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (distance > 0)
            {
                float moveX = (dx / distance) * enemy.Speed;
                float moveY = (dy / distance) * enemy.Speed;
                var newEnemyPos = new PointF(enemy.Position.X + moveX, enemy.Position.Y + moveY);
                enemies[i] = enemy with { Position = newEnemyPos };
            }

            // Check if laser hits enemy
            for (int j = lasers.Count - 1; j >= 0; j--)
            {
                var laser = lasers[j];
                float laserToEnemyDist = Distance(laser.Position, enemy.Position);
                
                if (laserToEnemyDist < EnemyRadius + 3)
                {
                    enemies.RemoveAt(i);
                    lasers.RemoveAt(j);
                    break;
                }
            }

            // Check if enemy hits player
            if (Distance(enemy.Position, player.Position) < PlayerRadius + EnemyRadius)
            {
                lives--;
                if (lives <= 0)
                {
                    gameOver = true;
                    synthesizer.SpeakAsync("Game Over!");
                }
                enemies.RemoveAt(i);
            }
        }

        // Redraw the form
        Invalidate();
    }

    private float Distance(PointF p1, PointF p2)
    {
        float dx = p1.X - p2.X;
        float dy = p1.Y - p2.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private void LaserFireTimer_Tick(object? sender, EventArgs e)
    {
        if (gameOver) return;

        var random = new Random();
        double angle = random.NextDouble() * 2 * Math.PI;
        var velocity = new PointF((float)(Math.Cos(angle) * LaserSpeed), (float)(Math.Sin(angle) * LaserSpeed));
        lasers.Add(new Laser(player.Position, velocity));
    }

    private void EnemySpawnTimer_Tick(object? sender, EventArgs e)
    {
        if (gameOver) return;

        var random = new Random();
        float x = random.Next(0, ClientSize.Width);
        float y = random.Next(0, ClientSize.Height);
        
        // Don't spawn too close to player
        while (Distance(new PointF(x, y), player.Position) < 150)
        {
            x = random.Next(0, ClientSize.Width);
            y = random.Next(0, ClientSize.Height);
        }
        
        enemies.Add(new Enemy(new PointF(x, y), 1.5f));
    }

    private void GameForm_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);

        // Draw player
        g.FillEllipse(Brushes.Cyan, player.Position.X - PlayerRadius, player.Position.Y - PlayerRadius, PlayerRadius * 2, PlayerRadius * 2);

        // Draw lasers
        foreach (var laser in lasers)
        {
            var endPoint = new PointF(laser.Position.X + laser.Velocity.X * 2, laser.Position.Y + laser.Velocity.Y * 2);
            g.DrawLine(Pens.Magenta, laser.Position, endPoint);
        }

        // Draw enemies (red circles)
        foreach (var enemy in enemies)
        {
            g.FillEllipse(Brushes.Red, enemy.Position.X - EnemyRadius, enemy.Position.Y - EnemyRadius, EnemyRadius * 2, EnemyRadius * 2);
        }

        // Draw UI
        var font = new Font("Arial", 14, FontStyle.Bold);
        g.DrawString($"Lives: {lives}", font, Brushes.White, 10, 10);
        
        if (gameOver)
        {
            var largeFont = new Font("Arial", 40, FontStyle.Bold);
            g.DrawString("GAME OVER", largeFont, Brushes.Red, ClientSize.Width / 2 - 150, ClientSize.Height / 2);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        gameTimer.Stop();
        laserFireTimer.Stop();
        enemySpawnTimer.Stop();
        recognizer?.Dispose();
        synthesizer.Dispose();
        base.OnFormClosing(e);
    }
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GameForm());
    }
}
