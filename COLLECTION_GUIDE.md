# 11-Hour Data Collection Setup

Complete automated data collection for AI training with zero user interaction.

## Quick Start (11 Hours)

```bash
cd d:\Desktop\ModernCSharp
dotnet run -- collect
```

That's it! The system will:
- ✅ Collect 11 hours of continuous gameplay data
- ✅ Automatically save episodes to `./training_data/`
- ✅ Display live progress with time estimates
- ✅ Show statistics at completion
- ✅ Be ready for AI training

## Command Reference

### Start Collection

```bash
# 11 hours (default)
dotnet run -- collect

# Custom duration
dotnet run -- collect 24    # 24 hours
dotnet run -- collect 4     # 4 hours
dotnet run -- collect 1     # 1 hour
```

### Training & Analysis

```bash
# View collected data statistics
dotnet run -- analyze

# Train AI model on collected data
dotnet run -- train

# Export data for external ML frameworks
dotnet run -- export

# Interactive menu (all options)
dotnet run -- interactive
```

### Gameplay

```bash
# Play the game normally (data optional)
dotnet run
```

## What Gets Collected

Each episode captures:
- **Game State**: Player position, velocity, enemy positions, laser positions, lives
- **Actions**: Direction commands (NORTH, SOUTH, EAST, etc.)
- **Rewards**: Points for survival, enemy elimination, penalties for damage
- **Voice Confidence**: Recognition confidence scores for each command
- **Episode Metadata**: Duration, total reward, timestamp

## Data Storage

```
d:\Desktop\ModernCSharp\
└── training_data/
    ├── training_data_ep1_132500.json   (~50KB per episode)
    ├── training_data_ep2_132600.json
    ├── training_data_ep3_132700.json
    ├── ...
    └── consolidated_training_data.json (after export)
```

**11 hours of data ≈ 5000-8000 episodes ≈ 250-400MB on disk**

## Expected Performance

| Duration | Episodes | Experiences | File Size |
|----------|----------|-------------|-----------|
| 1 hour   | 500-700  | 50K-100K   | 25-50MB   |
| 4 hours  | 2K-3K    | 200K-300K  | 100-150MB |
| 11 hours | 5K-8K    | 500K-800K  | 250-400MB |
| 24 hours | 11K-18K  | 1M-1.5M    | 500MB-1GB |

## Progress Monitoring

During collection, you'll see:

```
[████████████████░░░░░░░░░░░░░░░░░░░░] 45.2% | Episodes: 2340 | Experiences: 234,500 | ⏱️ 05h 02m 15s / 11h | Remaining: 05h 57m 45s
```

**Progress bar**: Visual completion percentage
**Episodes**: Number of complete game episodes
**Experiences**: Total game state/action pairs recorded
**⏱️ Elapsed time**: How long it's been running
**Remaining**: Estimated time until completion

Press `Ctrl+C` at any time to stop early.

## After Collection: Next Steps

### 1. Analyze the Data
```bash
dotnet run -- analyze
```

Shows:
- Total episodes and experiences
- Average reward trends
- Action distribution (which moves were used most)
- Voice recognition quality metrics
- Learning curve visualization

### 2. Train AI Model
```bash
dotnet run -- train
```

Processes all collected data:
- Q-Learning algorithm
- Trains action values
- Shows final metrics
- Exports trained model

### 3. Export for Advanced ML
```bash
dotnet run -- export
```

Creates `consolidated_training_data.json` for use with:
- TensorFlow
- PyTorch
- scikit-learn
- Custom ML pipelines

## System Requirements

- **Disk Space**: 500MB - 1GB
- **RAM**: ~2GB minimum (during training)
- **CPU**: Multi-core recommended (progress faster)
- **Time**: 11 hours (can run overnight)

## Tips & Tricks

### Run Overnight
Start collection before bed:
```bash
dotnet run -- collect 24
```

Check results in the morning.

### Parallel Multiple Sessions
You can run multiple collection sessions in separate terminals to accumulate data faster:

```bash
# Terminal 1: 12 hours
dotnet run -- collect 12

# Terminal 2 (different directory copy): 12 hours
# Data files from both will merge when analyzed
```

### Monitor Disk Space
Check how much space you're using:
```bash
Get-ChildItem training_data -Recurse | Measure-Object -Property Length -Sum
```

### Clear and Restart
Delete all data and start fresh:
```bash
dotnet run -- clear
dotnet run -- collect
```

## Troubleshooting

### Collection stopped early?
Press Ctrl+C was hit. Restart with:
```bash
dotnet run -- collect
```
(Existing data is preserved, new episodes are appended)

### Not enough disk space?
Check available space:
```bash
Get-Volume C:
```
Reduce collection time or export to external drive.

### Want to see progress after stopping?
```bash
dotnet run -- analyze
```

### Data files not appearing?
Check if `training_data/` directory exists:
```bash
Test-Path d:\Desktop\ModernCSharp\training_data
```

If missing, the directory is created automatically on first episode.

## Architecture

```
┌─────────────────────────────────────┐
│ AutomatedDataCollector              │
│ • Runs 11-hour simulation loop     │
│ • Generates realistic scenarios    │
│ • Records every experience         │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ TrainingDataManager                 │
│ • Saves episodes to JSON files     │
│ • Real-time persistence           │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ ./training_data/ (File Storage)     │
│ • Episodes 1-8000                  │
│ • ~250-400MB total                 │
└─────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ DataAnalyzer / AITrainer            │
│ • Analyze statistics               │
│ • Train models                     │
│ • Export for ML                    │
└─────────────────────────────────────┘
```

## Advanced Options

### Custom Collection Duration
```csharp
// In code: src/Program.cs
TimeSpan duration = TimeSpan.FromHours(48);  // 48 hours
AutomatedDataCollector.RunCollection(duration);
```

### Modify Simulation Parameters
Edit `AutomatedDataCollector.cs`:
```csharp
private const int MAX_STEPS_PER_EPISODE = 500;  // Longer episodes = more experiences
private const int MIN_STEPS_PER_EPISODE = 50;
```

### Adjust Reward Function
Edit `CalculateReward()` method to change:
- Survival bonus
- Enemy penalty
- Episode completion reward

## FAQ

**Q: Can I stop and resume collection?**
A: Yes! Stop with Ctrl+C, run again later. Episodes are timestamped and new data is appended.

**Q: How long will 11 hours actually take?**
A: Exactly 11 hours of real time. Run at night or while doing other work.

**Q: Will this use all my CPU?**
A: No. It's optimized to run in background (16ms delay between episodes).

**Q: Can I run the game normally while collecting?**
A: Collection is headless (no GUI). Run `dotnet run` separately to play manually.

**Q: How much RAM does collection use?**
A: ~200MB idle, peaks at 500MB during training.

**Q: What happens if my computer crashes?**
A: All saved episodes are safe. Just restart collection when back online.

## Success Metrics

After 11 hours, expect:

✅ 5,000-8,000 complete episodes
✅ 500,000-800,000 state-action pairs
✅ Average episode reward improving over time
✅ Voice recognition quality > 80%
✅ Sufficient data for training a strong AI

## Ready?

```bash
cd d:\Desktop\ModernCSharp
dotnet run -- collect
```

Let it run, then later:
```bash
dotnet run -- analyze
dotnet run -- train
```

Good luck! 🚀
