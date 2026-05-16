Ниже готовый текст для файла **`AGENTS.md`** в корне проекта. Godot официально поддерживает C# через .NET-версию редактора; C#-проекты поддерживают desktop export, включая Windows, но не web export. ([Godot Engine documentation][1])

````md
# Green District — Codex Instructions

## 1. Project Goal

Green District is a 2D top-down social strategy / modern society simulation game built with Godot.

The project is not a quick prototype.  
The MVP is a reduced version of the future full game and must be scalable to 1000+ simulated citizens.

Primary target platform:

- Windows desktop
- Export result: playable Windows EXE build

---

## 2. Final Technology Stack

Engine:

- Godot 4 .NET

Main language:

- C#

Data format:

- JSON for game data, balance, events, professions, projects, districts and scenario configuration

IDE:

- VS Code

AI assistant:

- Codex inside VS Code

Version control:

- Git
- GitHub remote repository

Target architecture:

- Data-driven simulation
- Modular systems
- Clear separation between simulation logic and presentation layer

---

## 3. Main Development Principle

The game must be developed as a simulation-first project.

Godot is responsible for:

- rendering;
- scenes;
- UI;
- input;
- camera;
- visual map;
- audio;
- export to Windows.

C# simulation code is responsible for:

- world state;
- citizens;
- needs;
- jobs;
- economy;
- businesses;
- districts;
- government decisions;
- events;
- crises;
- politics;
- elections;
- save/load logic.

The UI must not contain core game logic.

Correct flow:

```text
Player clicks UI button
→ UI sends command to simulation layer
→ simulation changes world state
→ UI reads updated state
→ Godot redraws panels and map
````

Incorrect flow:

```text
UI button directly changes citizens, budget, district stats and support rating
```

---

## 4. Recommended Project Structure

```text
green-district/
│
├─ godot/
│  ├─ project.godot
│  ├─ scenes/
│  ├─ scripts/
│  │  ├─ presentation/
│  │  ├─ ui/
│  │  └─ godot_adapters/
│  ├─ assets/
│  ├─ audio/
│  └─ fonts/
│
├─ src/
│  ├─ GreenDistrict.Simulation/
│  │  ├─ Core/
│  │  ├─ World/
│  │  ├─ Agents/
│  │  ├─ Needs/
│  │  ├─ Economy/
│  │  ├─ Government/
│  │  ├─ Politics/
│  │  ├─ Events/
│  │  ├─ Save/
│  │  └─ Debug/
│  │
│  └─ GreenDistrict.Tests/
│
├─ data/
│  ├─ balance/
│  ├─ citizens/
│  ├─ districts/
│  ├─ jobs/
│  ├─ businesses/
│  ├─ projects/
│  ├─ events/
│  ├─ crises/
│  └─ localization/
│
├─ docs/
│  ├─ game_design.md
│  ├─ technical_architecture.md
│  ├─ simulation_rules.md
│  ├─ mvp_scope.md
│  └─ codex_tasks.md
│
├─ saves/
├─ tools/
├─ AGENTS.md
├─ README.md
└─ .gitignore
```

---

## 5. Architecture Layers

The project must be separated into three major layers:

### 5.1 Simulation Layer

Pure C# logic.

This layer should not depend on Godot UI scenes.

Contains:

* SimulationClock
* WorldState
* DistrictSystem
* CitizenSystem
* NeedsSystem
* EconomySystem
* BusinessSystem
* GovernmentSystem
* PoliticsSystem
* EventSystem
* CrisisSystem
* ElectionSystem
* SaveSystem

### 5.2 Godot Adapter Layer

C# scripts attached to Godot nodes.

Purpose:

* connect Godot scenes to the simulation;
* pass player commands to simulation;
* read simulation state;
* update UI and map.

This layer may use Godot APIs.

### 5.3 Data Layer

External JSON data.

Contains:

* citizens;
* districts;
* professions;
* businesses;
* government projects;
* events;
* crises;
* balance constants;
* localization.

Game balance should not be hardcoded inside systems unless absolutely necessary.

---

## 6. Core Simulation Systems

The project architecture must follow these main systems:

1. Simulation Core
   Time, ticks, update order, save/load.

2. World System
   Settlement, districts, territory parameters.

3. Agent System
   Citizens, needs, work, income, mood.

4. Economy System
   Goods, businesses, jobs, money flow.

5. Government System
   Budget, taxes, projects, player decisions.

6. Politics System
   Support rating, elections, loss of power.

7. Event System
   Events, triggers, decisions, delayed effects, crisis chains.

8. UI & Debug System
   Map, panels, event log, analytics and debug tools.

---

## 7. Performance and Scalability Rules

The project must be designed for future simulation of 1000+ citizens.

Do not write logic that requires every citizen to perform heavy calculations every frame.

Prefer:

* fixed simulation ticks;
* batched updates;
* daily/hourly recalculations where possible;
* cached district-level aggregates;
* event-driven updates;
* separation between visual citizens and simulated citizens.

Avoid:

* per-frame full population scans;
* UI reading all citizens every frame;
* deeply nested loops across citizens, businesses and districts;
* hardcoded one-off logic;
* putting simulation logic inside Godot scene nodes.

---

## 8. Update Order

Simulation tick should use predictable order:

```text
1. Time update
2. Citizen needs update
3. Job and income update
4. Economy update
5. Business update
6. District aggregates update
7. Event trigger check
8. Crisis progression
9. Political support update
10. Election check
11. Notification generation
12. UI refresh request
```

The exact implementation can evolve, but the order must remain explicit and easy to debug.

---

## 9. Coding Rules

Use C# as the main language.

Prefer:

* clear classes;
* interfaces for systems where useful;
* DTOs for JSON-loaded data;
* deterministic simulation where possible;
* readable code over clever code;
* small focused systems;
* explicit method names.

Avoid:

* large God classes;
* hidden side effects;
* direct dependencies between unrelated systems;
* business logic in UI code;
* hardcoded balance values;
* premature visual polish before simulation works.

---

## 10. Naming Conventions

Use English names in code.

Examples:

```text
Citizen
District
NeedState
EconomySystem
GovernmentProject
PoliticalSupportSystem
ElectionSystem
CrisisChain
WorldState
SimulationTickResult
```

Russian can be used in:

* documentation;
* localization files;
* event texts;
* UI text;
* comments only when needed.

---

## 11. Data-Driven Rules

Events, businesses, jobs, projects and balance values should be loaded from JSON.

Example data categories:

```text
data/events/
data/jobs/
data/businesses/
data/projects/
data/balance/
data/localization/
```

Do not hardcode event text or balance numbers inside systems if they belong to game design.

---

## 12. Debug Requirements

Every major system should be debuggable.

MVP debug tools should include:

* view all districts;
* view citizen parameters;
* view economy state;
* view support calculation reasons;
* trigger event manually;
* trigger crisis manually;
* change district parameters;
* advance time faster;
* save and load test world.

Debug features are part of the development process and should not be postponed.

---

## 13. Codex Task Rules

When working on this project, Codex should:

1. Make small, focused changes.
2. Avoid rewriting unrelated systems.
3. Explain which files were changed.
4. Keep simulation code separate from UI code.
5. Prefer scalable C# architecture.
6. Add simple test or debug entry point for new simulation logic when possible.
7. Avoid hardcoding balance values.
8. Preserve existing data-driven structure.
9. Avoid introducing dependencies without clear need.
10. Keep code readable for a solo developer.

---

## 14. Good Task Examples

Good Codex task:

```text
Create SimulationClock in C#.
It should support pause, x1, x2, x5 speed, current minute/hour/day,
and emit a tick result that other systems can consume.
Do not connect it to UI yet.
```

Good Codex task:

```text
Create NeedsSystem.
It should update citizen needs once per simulation hour,
use values from data/balance/needs.json,
and return reasons for mood/support changes for debug display.
```

Good Codex task:

```text
Create EventSystem.
It should load event definitions from JSON,
check trigger conditions against WorldState,
generate active events,
and apply selected player decision effects.
```

Bad Codex task:

```text
Make the whole game.
```

Bad Codex task:

```text
Create city simulation with UI and economy and politics all at once.
```

---

## 15. Current Strategic Decision

The project must use:

```text
Godot 4 .NET + C#
```

Do not convert the core simulation to GDScript.

GDScript may be used only for minor disposable experiments, but production simulation code should be written in C#.

Reason:

* the MVP is intended to scale;
* the game may later simulate 1000+ citizens;
* the architecture must remain maintainable;
* rewriting the simulation from GDScript to C# later should be avoided.

---

## 16. Final Goal

The goal is to build a small but complete living simulation:

```text
time passes
→ citizens live and work
→ economy changes
→ needs rise and fall
→ districts become stable or unstable
→ events appear from world state
→ player makes decisions
→ society reacts
→ support changes
→ elections happen
→ the player may keep or lose power
→ the world continues
```

The project should feel not like a city builder or tycoon, but like a simulation of a living modern society where the player governs through conditions, priorities and consequences.

```
::contentReference[oaicite:1]{index=1}
```

[1]: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html?utm_source=chatgpt.com "C#/.NET — Godot Engine (stable) documentation in English"
