# AI Player Integration Guide

## Overview

The trained AI model has been successfully integrated into the game! You can now watch the AI play autonomously or toggle between voice control and AI control.

## Features

### AI Player (`AIPlayer.cs`)

- Uses the trained Q-learning model to make movement decisions
- Can run in fully autonomous mode
- Supports exploration rate adjustment (balance between learned behavior and random exploration)
- Provides action confidence scores for debugging

### Game Integration

The AI player has been integrated into `GameForm.cs` with the following features:

- **Toggle AI Mode**: Press `A` during gameplay to enable/disable AI control
- **Visual Feedback**: Title bar changes to show current mode
- **Seamless Switching**: Switch between voice and AI control at any time

## How to Use

### 1. Run the Game

```bash
dotnet run
```

### 2. Toggle AI Mode

- **Press `A`** to enable AI mode
- The title bar will change to "Voice Game - AI MODE"
- The AI will take control of player movement
- **Press `A` again** to return to voice control

### 3. Watch the AI Play

The AI will:

- Navigate the game world using the trained model
- Avoid obstacles
- Move strategically based on learned patterns
- Still use AI for shooting (existing AIShootingAgent)

## Controls

| Key            | Action                                 |
| -------------- | -------------------------------------- |
| `A`            | Toggle AI Mode (ON/OFF)                |
| `R`            | Restart game (when game over)          |
| Voice Commands | Control movement (when AI mode is OFF) |

## AI Behavior

The AI player uses the trained model with:

- **30-dimensional state space** (player position, velocity, enemies, lasers, etc.)
- **9 actions** (NORTH, SOUTH, EAST, WEST, NE, NW, SE, SW, STOP)
- **10% exploration rate** (90% use learned behavior, 10% random for variety)

## Performance

Based on training results:

- **Best actions**: NORTHEAST, SOUTHWEST (higher rewards)
- **Episodes trained**: 15
- **Total experiences**: 1,601
- **Model status**: ✅ Exported and ready

## Advanced Usage

### Adjust Exploration Rate

Edit `GameForm.cs` line ~156:

```csharp
aiPlayer = new AIPlayer(latestModel, explorationRate: 0.1f);  // 10% random
```

Lower values (e.g., `0.05f`) = more predictable, uses learned strategy  
Higher values (e.g., `0.3f`) = more exploratory, tries new things

### Load Specific Model

The game automatically loads the most recent `ai_model_*` file. To use a specific model:

```csharp
aiPlayer = new AIPlayer("ai_model_20251108_163128", explorationRate: 0.1f);
```

### Get AI Confidence Scores

For debugging or visualization:

```csharp
var confidence = aiPlayer.GetActionConfidence(currentState);
foreach (var action in confidence)
{
    Console.WriteLine($"{action.Key}: {action.Value:F2}");
}
```

## Next Steps

1. **Play the game** and watch the AI performance
2. **Collect more training data** by playing yourself
3. **Retrain the model** with: `dotnet run -- train`
4. **Compare AI performance** before and after additional training

## Tips

- The AI performs best in scenarios similar to the training data
- More training episodes = better AI performance
- The AI can get "stuck" in local optima - exploration helps
- Voice control + AI shooting remains the best hybrid approach

---

**Status**: ✅ AI Player fully integrated and ready to use!  
**Command**: Run game, press `A` to toggle AI mode
