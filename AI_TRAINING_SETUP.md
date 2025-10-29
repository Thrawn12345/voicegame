# AI Data Collection & Training Setup

## Overview
This setup enables automatic collection of gameplay data during training sessions and processes it to train AI models. The system captures game states, player actions, rewards, and voice recognition confidence scores.

## Architecture

```
┌─────────────────────────────────────────────┐
│         Gameplay (GameForm)                 │
│  • DataCollector records states & actions  │
│  • Voice commands trigger training data    │
└────────────────┬────────────────────────────┘
                 │ Training Data Episodes
                 ▼
┌─────────────────────────────────────────────┐
│    TrainingDataManager (JSON persistence)   │
│  • Saves episodes to /training_data/        │
│  • Loads and consolidates data             │
└────────────────┬────────────────────────────┘
                 │ Consolidated Data
                 ▼
┌─────────────────────────────────────────────┐
│         AITrainer (Model Learning)          │
│  • Q-Learning algorithm                     │
│  • Processes experiences in batches         │
│  • Tracks training metrics                  │
└────────────────┬────────────────────────────┘
                 │ Analysis & Insights
                 ▼
┌─────────────────────────────────────────────┐
│      DataAnalyzer (Statistics)              │
│  • Generates learning curves                │
│  • Reports action distributions             │
│  • Identifies optimal behaviors             │
└─────────────────────────────────────────────┘
```

## Quick Start

### 1. Build the Project
```bash
cd d:\Desktop\ModernCSharp
dotnet build
```

### 2. Run the Training Game
```bash
dotnet run
```
The game will automatically collect data for each episode played.

### 3. Train AI Model
Run the trainer console (see DataCollectionTrainer.cs):
```bash
dotnet run -- train
```

### 4. Analyze Results
```bash
dotnet run -- analyze
```

## File Structure

```
d:\Desktop\ModernCSharp\
├── src/
│   ├── Program.cs                 # Entry point
│   ├── GameForm.cs                # Game UI (integrated with DataCollector)
│   ├── DataCollector.cs           # ⭐ NEW: Captures gameplay data
│   ├── TrainingDataManager.cs     # ⭐ NEW: Saves/loads episodes
│   ├── AITrainer.cs               # ⭐ NEW: Trains models
│   ├── DataAnalyzer.cs            # ⭐ NEW: Analysis & visualization
│   ├── VoiceController.cs         # Voice recognition
│   ├── GameLogic.cs               # Game mechanics
│   └── ... (other game files)
│
├── training_data/                 # ⭐ NEW: Auto-created directory
│   ├── training_data_ep1_*.json   # Episode data
│   ├── training_data_ep2_*.json
│   └── consolidated_training_data.json
│
├── ModernCSharp.csproj
├── voicegame.sln
└── README.md
```

## Data Collection Flow

### During Gameplay
1. **GameForm** creates a `DataCollector` instance
2. Each tick, game state is encoded: `EncodeGameState(playerPos, velocity, enemies, lasers, ...)`
3. Voice command triggers action (north, south, east, etc.)
4. `DataCollector.RecordExperience(state, action, reward, nextState, isDone, confidence)`
5. Reward calculation:
   - **+1** for each enemy destroyed
   - **-1** for each hit taken
   - **+0.1** for surviving each frame
   - **-10** for game over

### Episode End
- `DataCollector.EndEpisode()` returns completed episode
- `TrainingDataManager.SaveEpisode()` writes to `training_data/training_data_epN_HHMMSS.json`

## Training Configuration

Edit `AITrainer.ModelConfig` in code or via constructor:

```csharp
var config = new AITrainer.ModelConfig
{
    LearningRate = 0.001f,           // How quickly model learns
    DiscountFactor = 0.99f,          // Value of future rewards
    ExplorationRate = 0.1f,          // 10% random actions during training
    BatchSize = 32,                  // Experiences per training batch
    HiddenLayerSize = 128,           // Neural network size
    ActionSpaceSize = 9,             // Number of actions (NORTH, SOUTH, ...)
    StateSpaceSize = 30              // Number of state features
};

var trainer = new AITrainer(config);
```

## Analysis & Metrics

The `DataAnalyzer` provides:

1. **Episode Statistics**
   - Total episodes, total experiences
   - Average episode length
   
2. **Reward Statistics**
   - Average, max, min rewards
   - Standard deviation (consistency)
   
3. **Action Distribution**
   - Which actions were taken most
   - Helps identify learned strategies
   
4. **Voice Recognition Quality**
   - Average confidence (% of high-confidence recognitions)
   - Identifies if speech recognition is reliable
   
5. **Learning Curves**
   - Tracks reward trend over episodes
   - Shows if AI is improving

## Integration Example

In `GameForm.cs`:

```csharp
private DataCollector collector = new DataCollector();
private TrainingDataManager dataManager = new TrainingDataManager();

private void GameTimer_Tick(object sender, EventArgs e)
{
    // ... game logic ...
    
    // Collect data
    float[] state = collector.EncodeGameState(
        player.Position, player.Velocity,
        enemies, lasers,
        lives, gameOver,
        Width, Height
    );
    
    int actionIndex = DataCollector.StringToAction(lastCommand);
    float reward = CalculateReward();
    
    float[] nextState = collector.EncodeGameState(
        player.Position, player.Velocity,
        enemies, lasers,
        lives, gameOver,
        Width, Height
    );
    
    collector.RecordExperience(state, actionIndex, reward, nextState, gameOver);
    
    if (gameOver)
    {
        var (episode, episodeNum, totalReward) = collector.EndEpisode();
        dataManager.SaveEpisode(episodeNum, episode, totalReward);
    }
}
```

## Commands Reference

```bash
# Build
dotnet build

# Run game with data collection
dotnet run

# Train AI on collected data
dotnet run -- train

# Analyze training results
dotnet run -- analyze

# Export data for external ML frameworks
dotnet run -- export

# Clear old training data
dotnet run -- clear
```

## Performance Tips

1. **Collect 100+ episodes** before training for better results
2. **Play diverse games** - vary strategies to get diverse training data
3. **Check confidence** - ensure speech recognition is working well (>70%)
4. **Train regularly** - run training after every 50 episodes
5. **Monitor learning curves** - check if rewards are improving

## Advanced Usage

### Export for Python/TensorFlow
```csharp
var manager = new TrainingDataManager();
string exportPath = manager.ExportForTraining("consolidated_training_data.json");
// Load into Python for advanced ML models
```

### Custom Reward Function
Modify `CalculateReward()` in GameForm to adjust:
- Enemy destruction bonus
- Survival incentive
- Movement efficiency penalty

### Model Deployment
Once trained, `AITrainer.PredictAction(state)` can:
- Run on a separate thread for predictions
- Provide AI opponent for multiplayer
- Control autonomous player for demos

## Troubleshooting

**No training data appearing?**
- Check if `/training_data/` directory was created
- Verify game is saving episodes on game over

**Model not improving?**
- Increase exploration rate (try more random actions)
- Collect more diverse data
- Reduce learning rate if loss is oscillating

**Memory issues with large datasets?**
- Reduce state space size
- Process data in smaller batches
- Delete old episodes: `dataManager.ClearAllData()`

## Next Steps

1. ✅ Review generated training data in `/training_data/`
2. ✅ Run the trainer and check metrics
3. ✅ Integrate predictions back into GameForm
4. ✅ Create AI difficulty levels
5. ✅ Export data to advanced ML frameworks (PyTorch, TensorFlow)

---

**Status:** Full data collection pipeline implemented. Ready for gameplay training!
