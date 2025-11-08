# Enemy Learning System - Implementation Summary

## Overview

Enemies now learn from their gameplay experiences using reinforcement learning! They improve their movement and shooting strategies over time.

## What Was Added

### 1. **EnemyLearningAgent.cs** (New File)

A comprehensive learning system for enemies that trains on:

- **Movement decisions**: Learning optimal positioning relative to player
- **Shooting decisions**: Learning when and where to shoot (direct vs. predictive)

**Key Features:**

- Separate AI trainers for movement and shooting
- 20-dimensional state space (enemy position, player position/velocity, laser threats, ally positions)
- 9 movement actions (8 directions + stop)
- 3 shooting actions (don't shoot, shoot direct, shoot predictive)
- Reward system:
  - ✅ +50 for hitting player
  - ✅ +5 for avoiding lasers
  - ✅ +1 for shooting
  - ❌ -100 for being destroyed

### 2. **Enemy Record Updated**

Added `LearningId` to track each enemy's learning progress:

```csharp
public record Enemy(PointF Position, float Speed, DateTime LastShotTime,
                   EnemyBehavior Behavior, DateTime LastBehaviorChange, int LearningId);
```

### 3. **GameForm Integration**

- Enemies now use learned movement instead of just rule-based AI
- Enemies use learned shooting decisions
- Rewards tracked in real-time
- Models automatically saved when game ends

### 4. **Training Data Output**

- Enemy models saved to `training_data/` folder:
  - `enemy_movement_model_YYYYMMDD_HHMMSS.json`
  - `enemy_shooting_model_YYYYMMDD_HHMMSS.json`
- Player AI models also saved to `training_data/`:
  - `ai_model_YYYYMMDD_HHMMSS.json`

## How It Works

### During Gameplay:

1. **Enemy Spawns** → Registered with `EnemyLearningAgent` → Gets unique `LearningId`

2. **Each Game Tick:**

   - Enemy encodes game state (player position, lasers, other enemies)
   - Queries learning agent for best movement action
   - Queries learning agent for shooting decision
   - Executes learned behaviors

3. **Rewards Collected:**

   - When enemy bullet hits player: +50 reward
   - When enemy is destroyed: -100 penalty
   - Small rewards for shooting attempts

4. **Game End:**
   - Learning models automatically saved to `training_data/`
   - Ready for analysis and retraining

### Learning Process:

```
Gameplay → Experience Collection → Model Training → Improved Behavior
    ↑                                                       ↓
    └──────────────── Repeat & Improve ←───────────────────┘
```

## Training Enemy Models

The enemy models are saved automatically, but you can explicitly train them:

```bash
# Run the game to collect enemy experiences
dotnet run

# Train player AI (already implemented)
dotnet run -- train

# Analyze all training data (includes enemy data)
dotnet run -- analyze
```

## Comparison: Old vs. New Enemy AI

### Before (Rule-Based):

- Fixed behavior patterns (Aggressive, Flanking, Cautious, Ambush)
- Predictable movements
- Simple shooting logic
- No adaptation

### After (Learning-Based):

- ✅ Learns from experience
- ✅ Adapts movement based on success/failure
- ✅ Learns shooting timing and accuracy
- ✅ Gets smarter over time
- ✅ Can discover novel strategies
- ⚡ Still uses exploration (30%) to try new tactics

## Expected Evolution

### Early Game (0-10 episodes):

- Random, exploratory behavior
- High exploration rate (30%)
- Learning basic cause-and-effect

### Mid Game (10-50 episodes):

- Patterns emerge (flanking, spacing)
- Better shooting accuracy
- Avoiding obvious player lasers

### Late Game (50+ episodes):

- Coordinated movements
- Predictive shooting mastery
- Strategic positioning
- Optimal engagement distances

## Configuration

Adjust learning parameters in `EnemyLearningAgent.cs`:

```csharp
// Line ~41: Movement trainer config
var movementConfig = new AITrainer.ModelConfig
{
    LearningRate = 0.002f,      // How fast enemies learn
    ExplorationRate = 0.3f      // 30% random actions
};

// Line ~50: Shooting trainer config
var shootingConfig = new AITrainer.ModelConfig
{
    LearningRate = 0.002f,
    ExplorationRate = 0.3f
};
```

**Tuning Tips:**

- Lower `LearningRate` (e.g., 0.001) = slower, more stable learning
- Higher `ExplorationRate` (e.g., 0.5) = more variety, less consistency
- Lower `ExplorationRate` (e.g., 0.1) = more predictable, uses learned strategy

## File Outputs

After each game session, check `training_data/` for:

```
training_data/
├── enemy_movement_model_20251108_163200.json
├── enemy_shooting_model_20251108_163200.json
├── ai_model_20251108_163128.json
└── training_data_ep*.json
```

## Monitoring Enemy Learning

Watch the console output during gameplay:

```
🧠 Enemy Learning Agent initialized
💾 Enemy learning models saved
```

During game end, enemy models are automatically saved.

## Future Enhancements

Potential improvements:

1. **Bullet ownership tracking** - Know exactly which enemy fired which bullet for precise rewards
2. **Team coordination** - Enemies learn to work together
3. **Memory system** - Remember effective strategies against specific player behaviors
4. **Difficulty scaling** - Adjust exploration rate based on player skill
5. **Transfer learning** - Load pre-trained models for instant smart enemies

---

## Summary

✅ **Enemies now learn from experience**  
✅ **Movement and shooting both trained**  
✅ **Models automatically saved to `training_data/`**  
✅ **Rewards tracked in real-time**  
✅ **Gets smarter with more gameplay**

**Result**: A dynamic, evolving AI that adapts to player strategies and improves over time!
