# Getting Started

## Prerequisites

- **Godot 4 .NET** (https://godotengine.org/)
- **.NET 7+ SDK** (https://dotnet.microsoft.com/)
- **Git** (https://git-scm.com/)
- **VS Code** (recommended IDE)

## Initial Setup

### 1. Build C# Solution

```bash
cd src
dotnet build GreenDistrict.sln
```

### 2. Run Tests

```bash
cd src
dotnet test GreenDistrict.Tests
```

Expected output: All tests should pass (40+ tests)

### 3. Open Godot Project

1. Launch Godot 4 .NET
2. Open project from `/godot` folder
3. Create a `project.godot` file if needed

### 4. Create Initial Scene

In Godot editor:
1. Create a new 2D scene (main.tscn)
2. Add a script to load SimulationClock
3. Test if C# can be called from Godot

## Project Organization

```
Development Workflow:
1. Implement logic in C# (GreenDistrict.Simulation)
2. Write unit tests
3. Integrate with Godot via adapters
4. Test end-to-end
```

## Common Commands

```bash
# Build
dotnet build src/GreenDistrict.sln

# Test
dotnet test src/GreenDistrict.Tests -v n

# Clean
dotnet clean src/GreenDistrict.sln

# Run specific test
dotnet test src/GreenDistrict.Tests -k SimulationClockTests
```

## Next Steps

1. ✅ Core systems created (SimulationClock, WorldState, UpdateManager)
2. ✅ Basic Citizen and data classes
3. ✅ Unit tests for core
4. ⏳ Implement Needs system
5. ⏳ Implement Economy system
6. ⏳ Create Godot scenes and adapters

## Troubleshooting

### "Cannot find .NET SDK"
- Install .NET 7+ from https://dotnet.microsoft.com/
- Restart terminal/IDE

### "Godot cannot find C# assembly"
- Ensure Godot is .NET version
- Build solution: `dotnet build`
- Restart Godot editor

### "Tests fail to run"
- Check xUnit is installed: `dotnet add package xunit`
- Ensure test project references simulation: see .csproj

## Resources

- [Godot C# Docs](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
- [xUnit Documentation](https://xunit.net/)
- [Green District Codex Instructions](../# Green District — Codex Instructions.md)
