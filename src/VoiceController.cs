using System;
using System.Drawing;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows.Forms;

namespace VoiceGame
{
    public class VoiceController : IDisposable
    {
        private SpeechRecognitionEngine? recognizer;
        private readonly SpeechSynthesizer synthesizer = new();
        private readonly Action<PointF> onVelocityChange;

        public VoiceController(Action<PointF> onVelocityChange)
        {
            this.onVelocityChange = onVelocityChange;
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
                    newVelocity = new PointF(0, -GameConstants.PlayerSpeed);
                    Console.WriteLine("✅ Moving NORTH");
                    break;
                case "south":
                    newVelocity = new PointF(0, GameConstants.PlayerSpeed);
                    Console.WriteLine("✅ Moving SOUTH");
                    break;
                case "east":
                    newVelocity = new PointF(GameConstants.PlayerSpeed, 0);
                    Console.WriteLine("✅ Moving EAST");
                    break;
                case "west":
                    newVelocity = new PointF(-GameConstants.PlayerSpeed, 0);
                    Console.WriteLine("✅ Moving WEST");
                    break;
                case "north east":
                    newVelocity = new PointF(GameConstants.PlayerSpeed, -GameConstants.PlayerSpeed);
                    Console.WriteLine("✅ Moving NORTH EAST");
                    break;
                case "north west":
                    newVelocity = new PointF(-GameConstants.PlayerSpeed, -GameConstants.PlayerSpeed);
                    Console.WriteLine("✅ Moving NORTH WEST");
                    break;
                case "south east":
                    newVelocity = new PointF(GameConstants.PlayerSpeed, GameConstants.PlayerSpeed);
                    Console.WriteLine("✅ Moving SOUTH EAST");
                    break;
                case "south west":
                    newVelocity = new PointF(-GameConstants.PlayerSpeed, GameConstants.PlayerSpeed);
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

            onVelocityChange(newVelocity);
        }

        public void Speak(string text)
        {
            synthesizer.SpeakAsync(text);
        }

        /// <summary>
        /// Enable or disable voice recognition.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (recognizer != null)
            {
                try
                {
                    if (enabled)
                    {
                        recognizer.RecognizeAsync(RecognizeMode.Multiple);
                        Console.WriteLine("🎤 Voice control ENABLED");
                    }
                    else
                    {
                        recognizer.RecognizeAsyncCancel();
                        Console.WriteLine("🔇 Voice control DISABLED");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error toggling voice recognition: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            recognizer?.Dispose();
            synthesizer.Dispose();
        }
    }
}