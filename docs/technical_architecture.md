# Technical Architecture

## Project Structure

```
green-district/
├── src/
│   ├── GreenDistrict.Simulation/        # Pure C# simulation logic
│   │   ├── Core/                        # Clock, state, updates
│   │   ├── World/                       # Districts, territory
│   │   ├── Agents/                      # Citizens, AI
│   │   ├── Needs/                       # Need system
│   │   ├── Economy/                     # Business, jobs, money
│   │   ├── Government/                  # Taxes, budget, projects
│   │   ├── Politics/                    # Elections, support
│   │   ├── Events/                      # Events, crises
│   │   ├── Save/                        # Serialization
│   │   └── Debug/                       # Debugging tools
│   └── GreenDistrict.Tests/             # Unit tests (xUnit)
├── godot/                               # Godot project
│   ├── scenes/                          # Scene files
│   ├── scripts/                         # C# Godot scripts
│   │   ├── presentation/                # UI visualization
│   │   ├── ui/                          # UI components
│   │   └── godot_adapters/              # Simulation ↔ Godot bridge
│   └── assets/                          # Images, audio, fonts
└── data/                                # JSON configuration files
```

## Separation of Concerns

### Simulation Layer (C#)
- Pure logic, no Godot dependencies
- Deterministic and testable
- Handles all game rules

### Godot Adapter Layer (C# + Godot)
- Bridges simulation and presentation
- Passes player input to simulation
- Updates UI based on simulation state

### Presentation Layer (Godot)
- Rendering
- User input
- UI display
- Audio/video

## Data Flow

```
Player Input
    ↓
Godot UI Handler
    ↓
Adapter Layer (command)
    ↓
Simulation System (processes command)
    ↓
WorldState (updates)
    ↓
Adapter Layer (reads state)
    ↓
Godot UI (displays)
```

## System Dependencies

```
UpdateManager
    ↓
SimulationClock
    ↓
WorldState (contains everything)
    ├── Citizens
    ├── Districts
    ├── Businesses
    ├── Events
    └── Player State
```

## Testing Strategy

1. **Unit Tests** - Test individual systems in isolation
2. **Integration Tests** - Test system interactions
3. **Simulation Tests** - Run full cycles, check state consistency
4. **Performance Tests** - Ensure scalability to 1000+ citizens

All simulation logic should be unit-tested without Godot.
