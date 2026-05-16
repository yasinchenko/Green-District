# Green District

A 2D top-down social strategy / modern society simulation game built with Godot 4 .NET and C#.

## Project Structure

```
green-district/
├── godot/                          # Godot project
├── src/
│   ├── GreenDistrict.Simulation/   # Simulation logic (C#)
│   └── GreenDistrict.Tests/        # Unit tests
├── data/                           # JSON game data
├── docs/                           # Documentation
└── saves/                          # Game saves
```

## Technology Stack

- **Engine**: Godot 4 .NET
- **Language**: C#
- **Data**: JSON
- **IDE**: VS Code
- **VCS**: Git + GitHub

## Development Principles

1. **Simulation-First**: Game logic in C#, rendering in Godot
2. **Data-Driven**: Configuration in JSON, not hardcoded
3. **Modular**: Independent, testable systems
4. **Scalable**: Ready for 1000+ simulated citizens

## Getting Started

### Prerequisites

- Godot 4 .NET
- .NET 7+ SDK
- VS Code (or any C# IDE)
- Git

### Setup

1. Clone the repository
2. Open the Godot project from `/godot` folder
3. Build the C# solution: `dotnet build src/GreenDistrict.sln`
4. Run tests: `dotnet test src/GreenDistrict.Tests`

## Project Status

Early development phase - core simulation infrastructure
