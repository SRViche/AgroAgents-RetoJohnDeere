# AgroAgents — Autonomous Harvesting Simulation (Unity Presentation)

A multi-agent cotton-harvesting simulation visualized in Unity. The Unity project is a **pure view** over the engine-agnostic `HarvestingCore` library — it renders the simulation but owns no simulation logic.

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Assembly Graph & Dependency Management](#assembly-graph--dependency-management)
- [Project Structure](#project-structure)
- [Core Integration (Git Submodule)](#core-integration-git-submodule)
- [Scene Authoring Guide](#scene-authoring-guide)
- [ScriptableObject Assets](#scriptableobject-assets)
- [Simulation Lifecycle](#simulation-lifecycle)
- [Coordinate System](#coordinate-system)
- [Debug Controls](#debug-controls)
- [Configuration Reference](#configuration-reference)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Unity Presentation Layer                        │
│  WorldBootstrapper · SimulationDriver · GridView · AgentView · Camera │
│              (AgroAgents.Presentation assembly)                        │
└────────────────────────────┬─────────────────────────────────────────┘
                             │ depends on (compile-time)
                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       Simulation Port (Interfaces)                     │
│  ISimulationConnector · ISimulationConnection · ISimulationSession    │
│  WorldSnapshot · WorldUpdate · PortCellState · PortStateId · etc.     │
│         (AgroAgents.SimulationPort assembly — no Unity refs)          │
└──────────────┬───────────────────────────────────────────────────────┘
               │ implemented by
               ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     In-Memory Adapter                                  │
│  InMemorySimulationConnector · InMemorySimulationConnection           │
│  InMemorySimulationSession · Mappings                                 │
│       (AgroAgents.InMemoryAdapter assembly)                           │
└──────────────┬───────────────────────────────────────────────────────┘
               │ wraps
               ▼
┌──────────────────────────────────────────────────────────────────────┐
│                        HarvestingCore                                  │
│  SimulationWorld · WorldModel · Agent · Harvester · Tractor           │
│  SimulationConfig · DeterministicRandom · Pathfinding                 │
│       (HarvestingCore assembly — noEngineReferences: true)            │
└──────────────────────────────────────────────────────────────────────┘
```

Key constraints:

1. **Compiler-enforced engine boundary** — `HarvestingCore.asmdef` has `noEngineReferences: true`. A `using UnityEngine;` in core code is a build error.
2. **Compiler-enforced port boundary** — `AgroAgents.Presentation` references only `AgroAgents.SimulationPort`, never `HarvestingCore`. A `using HarvestingCore;` in presentation code is a build error.
3. **Presentation is a projection** — The only mutating call in steady state is `ISimulationSession.RequestTick()`. Everything else is a read of immutable snapshots.
4. **Integer ticks, continuous rendering** — Simulation advances in discrete ticks; Unity renders with interpolation between tick states.

---

## Assembly Graph & Dependency Management

| Assembly | References | `noEngineReferences` | Purpose |
|----------|-----------|---------------------|---------|
| `HarvestingCore` | _(none)_ | `true` | Engine-agnostic simulation logic |
| `AgroAgents.SimulationPort` | _(none)_ | `true` | Interfaces + mirrored DTOs |
| `AgroAgents.InMemoryAdapter` | `SimulationPort`, `HarvestingCore` | `false` | Wraps `SimulationWorld` behind the port |
| `AgroAgents.Presentation` | `SimulationPort` | `false` | All Unity presentation scripts |

The concrete adapter (`InMemorySimulationConnector`) is wired into `WorldBootstrapper` via `[SerializeReference]` — Unity's type cache resolves it at the Inspector level, so no compile-time reference from Presentation to the adapter is needed. A future `WebSocketSimulationConnector` appears in the same dropdown with zero code changes.

### Adding a new adapter

1. Create a new assembly (e.g., `AgroAgents.WebSocketAdapter.asmdef`) referencing `AgroAgents.SimulationPort`.
2. Implement `ISimulationConnector` and `ISimulationConnection` as `[Serializable]` classes.
3. In the scene, select the new connector type from the `WorldBootstrapper > Connection` dropdown.

---

## Project Structure

```
AgroAgents-RetoJohnDeere/
├── External/
│   └── AgenticModel/                    ← git submodule (HarvestingCore repo)
│       └── src/HarvestingCore/          ← UPM local package root
│           ├── package.json
│           ├── HarvestingCore.asmdef
│           ├── World.cs
│           ├── Agents/
│           ├── Configuration/
│           ├── Coordination/
│           ├── Pathfinding/
│           └── World/
├── Packages/
│   └── manifest.json                    ← "com.agroagents.harvestingcore": "file:../External/AgenticModel/src/HarvestingCore"
├── Assets/
│   ├── Scripts/
│   │   ├── Port/                        ← AgroAgents.SimulationPort.asmdef
│   │   │   ├── SimulationInterfaces.cs  (ISimulationConnector, ISimulationConnection, ISimulationSession)
│   │   │   ├── Enums.cs                 (PortCellState, PortStateId, PortAgentRole, PortHeuristicKind)
│   │   │   ├── PortGridPosition.cs
│   │   │   ├── PortAgentSnapshot.cs
│   │   │   ├── PortAgentSpec.cs
│   │   │   ├── PortCellSnapshot.cs
│   │   │   ├── SessionRequest.cs
│   │   │   ├── WorldSnapshot.cs
│   │   │   └── WorldUpdate.cs
│   │   ├── Adapters/InMemory/           ← AgroAgents.InMemoryAdapter.asmdef
│   │   │   ├── InMemorySimulationConnector.cs
│   │   │   ├── InMemorySimulationConnection.cs
│   │   │   └── Mappings.cs
│   │   ├── Authoring/                   ← Scene bootstrapping
│   │   │   ├── WorldBootstrapper.cs
│   │   │   └── SiteMarker.cs
│   │   ├── Simulation/                  ← Tick driver
│   │   │   └── SimulationDriver.cs
│   │   ├── Views/                       ← Rendering
│   │   │   ├── GridView.cs
│   │   │   ├── AgentView.cs
│   │   │   ├── CellVisualMap.cs
│   │   │   └── StateVisualMap.cs
│   │   ├── Mapping/                     ← Coordinate conversion
│   │   │   └── CoordinateMapper.cs
│   │   └── CameraScripts/
│   │       └── IsometricView.cs
│   ├── Materials/
│   ├── Figures OBJ/
│   └── Tests/
└── .gitmodules
```

---

## Core Integration (Git Submodule)

The `HarvestingCore` source lives in its own repository and is brought in as a git submodule at `External/AgenticModel/`. Unity sees it as a local UPM package via a `file:` path in `Packages/manifest.json`.

### Initial clone

```bash
git clone --recursive <this-repo-url>
```

### Updating the submodule

```bash
git submodule update --remote External/AgenticModel
```

### Why a submodule + local package?

- No `.meta` file pollution in the core repo (packages outside `Assets/` don't get metas).
- The core remains independently buildable with `dotnet build`.
- Debugger stepping into core code works (source is present, not a precompiled DLL).
- MSBuild output (`bin/`, `obj/`) is redirected to `External/AgenticModel/artifacts/` so Unity never tries to compile generated `.cs` files.

---

## Scene Authoring Guide

### Required GameObjects

#### 1. WorldBootstrapper

The entry point. One per scene, execution order `-1000`.

| Inspector Section | Fields | Notes |
|-------------------|--------|-------|
| Connection | `connector` | Pick `InMemorySimulationConnector` from the `[SerializeReference]` dropdown |
| Wiring | `simulationDriver`, `gridView`, `agentViews[]`, `siteMarkers[]` | Drag explicit references — no `FindObjectOfType` anywhere |
| Grid | `gridOrigin`, `gridWidth`, `gridHeight`, `tileSize` | `gridOrigin` is a Transform; null uses self |
| World source | `worldSource`, `authoredGrid` | `Generated` or `AuthoredText` (TextAsset with `.` `W` `#` `_` chars) |
| Determinism | `seed` | Same seed = same world and agent behaviour |
| Grid generation | `cropDensity`, `blockedDensity` | Only used when `worldSource = Generated` |
| Terrain costs | `cropCost`, `emptyCost`, `harvestedCost`, `heuristic` | Pathfinding weights |
| Agent defaults | `defaultMaxLoad`, `defaultMaxFuel`, `defaultFuelConsumption` | Applied unless an AgentView overrides |
| Coordination | `dumpPreferenceFactor`, `capacityFactor`, `harvesterFuelReserveMultiplier`, `tractorFuelReserveMultiplier` | Coordination algorithm tunables |

#### 2. SimulationDriver

Drives the tick loop. **Author it disabled** — `WorldBootstrapper` enables it after connection.

| Field | Default | Description |
|-------|---------|-------------|
| `tickRate` | 4 | Ticks per second of unscaled real time |
| `tickBudget` | 4 | Max ticks per Unity frame |
| `speedMultiplier` | 1 | Scales simulation time (does not affect outcomes) |
| `startPaused` | false | Begin in paused state |
| `pauseKey` | P | Toggle pause |
| `stepKey` | Period (.) | Advance one tick while paused |

#### 3. GridView

Spawns floor tiles and content objects at runtime.

| Field | Description |
|-------|-------------|
| `floorPrefab` | Prefab with a `Renderer` — instantiated once per cell |
| `cellVisualMap` | `CellVisualMap` ScriptableObject (state → material + content prefab) |
| `refuelMarkerPrefab` | Visual placed at refuel station positions |
| `dumpMarkerPrefab` | Visual placed at dump site positions |
| `floorParent` | Transform to parent floor tiles under |
| `contentParent` | Transform to parent content objects under |
| `contentYOffset` | Y offset for content above floor |
| `useSharedMaterial` | Avoid per-tile material instances (recommended on) |

#### 4. AgentView (one per agent)

Each agent gets its own GameObject with the agent model as a child.

| Field | Description |
|-------|-------------|
| `agentId` | Unique string identifier (becomes the simulation id) |
| `role` | `Harvester` or `Tractor` |
| `startCell` | `Vector2Int` — grid position where the agent spawns |
| `overrideCapacities` | If true, uses per-agent maxLoad/maxFuel/fuelConsumption |
| `stateVisualMap` | `StateVisualMap` ScriptableObject |
| `bodyRenderer` | Renderer on the agent mesh driven by state visuals |
| `badgeAnchor` | Transform for status badge placement |
| `heightOffset` | Y offset so model rests on tile surface |
| `rotationSpeed` | Degrees/sec for smooth yaw rotation |
| `forwardOffsetY` | Yaw correction if model forward ≠ +Z |
| `statusLabel` | Optional `UI.Text` showing fuel/load |

#### 5. SiteMarker (one per station)

Empty GameObjects marking refuel stations and dump sites.

| Field | Description |
|-------|-------------|
| `kind` | `Refuel` or `Dump` |
| `useExplicitCell` | If true, uses `explicitCell` instead of world position |
| `explicitCell` | `Vector2Int` grid cell (only when `useExplicitCell` is on) |

When `useExplicitCell` is off, the marker's Transform world position is converted to a grid cell via `CoordinateMapper.TryToGrid` at bootstrap.

#### 6. IsometricView (Camera)

Attach to a camera GameObject for automatic grid-centered framing.

| Field | Description |
|-------|-------------|
| `worldBootstrapper` | Reference to the WorldBootstrapper |
| `distance` | Camera distance from focus point |
| `yaw` / `pitch` | Isometric angles (default 45° / 35.264°) |
| `useOrthographic` | Enable orthographic projection |
| `padding` | Extra margin beyond grid bounds |

### Minimal Hierarchy

```
Scene Root
├── WorldBootstrapper          [WorldBootstrapper]
├── SimulationDriver           [SimulationDriver] (disabled)
├── GridView                   [GridView]
│   ├── FloorParent
│   └── ContentParent
├── GridOrigin                 (empty Transform at world origin)
├── Agents
│   ├── Harvester_0            [AgentView] role=Harvester
│   ├── Harvester_1            [AgentView] role=Harvester
│   └── Tractor_0             [AgentView] role=Tractor
├── Sites
│   ├── RefuelStation_0        [SiteMarker] kind=Refuel
│   └── DumpSite_0            [SiteMarker] kind=Dump
└── Main Camera                [Camera, IsometricView]
```

---

## ScriptableObject Assets

### CellVisualMap

Create via: `Assets > Create > AgroAgents > Cell Visual Map`

Maps each `PortCellState` to floor material and optional content prefab:

| State | Typical Material | Content Prefab |
|-------|-----------------|----------------|
| `Empty` | Light brown/dirt | _(none)_ |
| `Crop` | Green | Crop model |
| `Blocked` | Dark gray | Rock/obstacle model |
| `Harvested` | Yellow/tan | _(none)_ |

Also provides a `fallbackFloorMaterial` for unmapped states.

### StateVisualMap

Create via: `Assets > Create > AgroAgents > State Visual Map`

Maps each `PortStateId` to material, tint color, and optional badge prefab:

| State | Default Tint | Meaning |
|-------|-------------|---------|
| `Idle` | Gray | Agent idle, no task |
| `Harvest` | Green | Actively harvesting |
| `GoToRefuel` | Orange | Moving to refuel station |
| `GoToDump` | Brown | Moving to dump site |
| `GoToMeetingPoint` | Cyan | Moving to tractor meeting point |
| `WaitTractor` | Blue | Harvester waiting for tractor |
| `WaitHarvester` | Blue | Tractor waiting for harvester |
| `Inactive` | Dark red | Agent deactivated (out of fuel) |

Missing states fall back to magenta and log one warning per session.

---

## Simulation Lifecycle

```
Awake (WorldBootstrapper, order -1000)
  ├─ Validate references
  ├─ Build CoordinateMapper
  ├─ Resolve SiteMarkers → PortGridPosition lists
  ├─ Validate and sort AgentViews by id
  ├─ Build SessionRequest from Inspector values
  └─ connector.Connect(request) → ISimulationConnection

Update (WorldBootstrapper)
  ├─ connection.Poll()
  ├─ On completion:
  │   ├─ Build AgentBindingRegistry
  │   ├─ Bind each AgentView to its snapshot agent
  │   ├─ simulationDriver.Initialize(session, mapper, bindings, gridView)
  │   └─ gridView.Initialize(snapshot, mapper)
  └─ (disables self polling after resolution)

Update (SimulationDriver, every frame)
  ├─ Accumulate unscaled dt × speedMultiplier
  ├─ While accumulator ≥ tickInterval && ticks < tickBudget:
  │   └─ session.RequestTick()
  ├─ Compute interpolationAlpha
  ├─ For each bound AgentView:
  │   └─ view.Render(alpha, dt)
  └─ On UpdateReceived event:
      ├─ bindings.ApplyUpdate(update)
      ├─ gridView.OnUpdateReceived(update)
      └─ Update DischargedTotal / halted flag
```

---

## Coordinate System

| Concept | Mapping |
|---------|---------|
| Grid X | Unity world X |
| Grid Y | Unity world Z |
| Grid (0,0) | `gridOrigin` Transform position |
| Cell centre | `GridOrigin + (X × TileSize, 0, Y × TileSize)` |
| Agent height | Cell centre Y replaced with `heightOffset` |

The `CoordinateMapper` is the sole authority for these conversions. It is created once by `WorldBootstrapper` and passed to all consumers.

---

## Debug Controls

| Key | Action |
|-----|--------|
| P | Toggle pause/resume |
| . (Period) | Step one tick (while paused) |

Speed and tick rate can be changed at runtime via the `SimulationDriver` Inspector or programmatically:

```csharp
simulationDriver.SetTickRate(8f);       // 8 ticks/sec
simulationDriver.SetSpeedMultiplier(2f); // 2x speed
simulationDriver.IsPaused = true;
simulationDriver.StepOneTick();
```

---

## Configuration Reference

### WorldBootstrapper — SessionRequest Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `gridWidth` | int | 32 | Grid columns (1–512) |
| `gridHeight` | int | 32 | Grid rows (1–512) |
| `seed` | int | 20240101 | Deterministic RNG seed |
| `cropDensity` | float | 0.55 | Fraction of cells with crops (0–1) |
| `blockedDensity` | float | 0.10 | Fraction of cells blocked (0–1) |
| `cropCost` | int | 1 | Pathfinding cost for crop cells |
| `emptyCost` | int | 2 | Pathfinding cost for empty cells |
| `harvestedCost` | int | 10 | Pathfinding cost for harvested cells |
| `heuristic` | PortHeuristicKind | Octile | Pathfinding heuristic |
| `defaultMaxLoad` | int | 100 | Default agent cargo capacity |
| `defaultMaxFuel` | int | 1000 | Default agent fuel tank |
| `defaultFuelConsumption` | int | 1 | Fuel consumed per move |
| `dumpPreferenceFactor` | float | 1.0 | Dump site selection weight |
| `capacityFactor` | float | 0.5 | Capacity threshold for dump trigger |
| `harvesterFuelReserveMultiplier` | float | 1.2 | Fuel reserve factor for harvesters |
| `tractorFuelReserveMultiplier` | float | 2.5 | Fuel reserve factor for tractors |

### Authored Grid Format (TextAsset)

When `worldSource = AuthoredText`, provide a `TextAsset` where each character maps to a cell state:

| Char | Cell State |
|------|-----------|
| `.` | Empty |
| `W` | Crop |
| `#` | Blocked |
| `_` | Harvested |

Example (4×4):
```
WW.#
W.W.
..W#
W_..
```

---

## Getting Started

1. Clone with submodules:
   ```bash
   git clone --recursive <repo-url>
   ```

2. Open in Unity 6000.5.7f1 or later.

3. Verify the local package resolves: check `Window > Package Manager` for "Harvesting Core".

4. Open/create a scene following the [Scene Authoring Guide](#scene-authoring-guide).

5. Create the required ScriptableObject assets (`CellVisualMap`, `StateVisualMap`) and assign prefabs.

6. Wire all Inspector references on `WorldBootstrapper`.

7. Press Play. The bootstrapper logs errors to the Console if any reference is missing or invalid.
