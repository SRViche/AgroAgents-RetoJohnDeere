# Requirements Document

## Introduction

This feature integrates the engine-agnostic `HarvestingCore` simulation library into the Unity presentation project `AgroAgents-RetoJohnDeere`, so that the Unity project stops owning simulation logic and becomes a pure view over the core's world model.

The core becomes fully authoritative: it owns the grid, all agent state, pathfinding, the agent finite state machines, and coordination. Unity owns only presentation concerns: assembly wiring, tick driving, prefab instantiation, material selection, camera, visual interpolation, rotation smoothing, and debug controls. Every duplicated algorithm currently living in Unity scripts is removed rather than kept behind a toggle.

The core source is brought into the Unity project behind an assembly definition that does not reference `UnityEngine`, so engine-agnosticism is enforced by the compiler rather than by convention. The dependency direction is one-way: the presentation assembly references the core assembly, never the reverse.

## Glossary

- **Harvesting_Core**: The existing engine-agnostic C# simulation library, rooted at the `HarvestingCore` namespace, whose façade type is `SimulationWorld`.
- **Core_Assembly**: The Unity assembly definition that compiles the Harvesting_Core source inside the Unity project.
- **Presentation_Assembly**: The Unity assembly definition that compiles all Unity-side scripts of the project.
- **Simulation_Driver**: The Presentation_Assembly component that owns the single `SimulationWorld` instance, advances simulation Ticks, and exposes debug controls.
- **Tick**: One discrete simulation step, produced by one call to `SimulationWorld.Tick()`.
- **Tick_Rate**: The configured number of Ticks the Simulation_Driver targets per second of unscaled real time.
- **Tick_Interval**: The real-time duration of one Tick, equal to the Speed_Multiplier-scaled reciprocal of Tick_Rate.
- **Speed_Multiplier**: The positive factor applied to simulation time progression by the Simulation_Driver.
- **Tick_Budget**: The maximum number of Ticks the Simulation_Driver executes within a single Unity frame.
- **Interpolation_Alpha**: The value in the closed interval `[0, 1]` describing progress between the previously rendered Tick state and the current Tick state.
- **Coordinate_Mapper**: The Presentation_Assembly component that converts between `HarvestingCore.World.GridPosition` and Unity world-space positions.
- **Grid_Origin**: The Unity world-space position corresponding to `GridPosition(0, 0)`.
- **Tile_Size**: The Unity world-space edge length of one grid cell.
- **Grid_View**: The Presentation_Assembly component that spawns and updates floor prefabs, content prefabs, and floor materials from the core's `Cells`.
- **Agent_View**: The Presentation_Assembly component attached to one agent GameObject that renders the position, orientation, and state of exactly one core `Agent`.
- **Agent_Binding**: The association between one core `Agent` identifier and one Agent_View instance.
- **State_Visual_Map**: The Presentation_Assembly data that maps each `HarvestingCore.Agents.StateId` value to a visual representation.
- **Cell_State**: The core enumeration `HarvestingCore.World.CellState` with values `Empty`, `Crop`, `Blocked`, `Harvested`.
- **State_Id**: The core enumeration `HarvestingCore.Agents.StateId` with values `Idle`, `Harvest`, `GoToRefuel`, `GoToDump`, `GoToMeetingPoint`, `WaitTractor`, `WaitHarvester`, `Inactive`.
- **World_Bootstrapper**: The Presentation_Assembly component that builds the `WorldModel`, `SimulationConfig`, and `IRandomSource`, registers agents, and hands the resulting `SimulationWorld` to the Simulation_Driver.
- **Site_Marker**: A Unity scene object authored to designate a refuel station or a dump site position.
- **Legacy_Simulation_Script**: Any Unity script in the project that decides simulation outcomes, listed explicitly in Requirement 12.

## Assumptions

The following defaults keep the specification complete and remain open to revision during design.

1. Grid axis mapping is `GridPosition.X` to Unity world `x` and `GridPosition.Y` to Unity world `z`, with Unity `y` reserved for height.
2. Grid_Origin corresponds to `GridPosition(0, 0)`, which is the core's top-left origin, so increasing `GridPosition.Y` increases Unity world `z`.
3. The Unity project runs exactly one `SimulationWorld` instance per loaded scene.
4. Default Tick_Rate is `4` Ticks per second, default Speed_Multiplier is `1.0`, and default Tick_Budget is `4` Ticks per frame.
5. Agent identifiers are authored in the scene and are unique within the scene.
6. Refuel stations and dump sites occupy grid cells inside the world bounds; positions outside the grid are not supported.
7. The core's `MoveOrder` permits eight-directional movement, so interpolation covers diagonal transitions between adjacent cells.
8. Existing Unity identifiers use Spanish names (`TileState.Cosechado`, `TileContent.Cultivo`) while the core uses English names; the integration adopts the core's English vocabulary for all new and migrated presentation types.
9. Unity `6000.5.7f1` compiles `netstandard2.1`-compatible C# 8.0 source, so the Harvesting_Core source compiles unchanged inside the Core_Assembly.

## Requirements

### Requirement 1: Compile-Time Assembly Boundary

**User Story:** As a developer, I want the simulation core compiled into its own Unity assembly with no engine reference, so that engine coupling is impossible rather than merely discouraged.

#### Acceptance Criteria

1. THE Core_Assembly SHALL compile every Harvesting_Core source file present in the Unity project.
2. THE Core_Assembly SHALL declare no reference to `UnityEngine`, `UnityEditor`, or any Unity-provided assembly.
3. IF a Core_Assembly source file references a Unity type, THEN THE Core_Assembly SHALL fail compilation with an unresolved-type error.
4. THE Presentation_Assembly SHALL declare a reference to the Core_Assembly.
5. THE Core_Assembly SHALL declare no reference to the Presentation_Assembly.
6. THE Core_Assembly SHALL contain no type that derives from `UnityEngine.Object`.
7. WHEN the Unity project is compiled, THE Presentation_Assembly SHALL contain every Unity script of the project and the Core_Assembly SHALL contain no Unity script of the project.

### Requirement 2: Core as Single Source of Truth

**User Story:** As a developer, I want the core to own all simulation state, so that the rendered scene always reflects one authoritative model.

#### Acceptance Criteria

1. THE Presentation_Assembly SHALL read grid cell state exclusively from `SimulationWorld.Cells`.
2. THE Presentation_Assembly SHALL read agent position, fuel, load, path, meeting point, and state exclusively from `SimulationWorld.Agents`.
3. THE Presentation_Assembly SHALL store no field that duplicates a core cell state, agent position, agent fuel, agent load, or agent state value as authoritative data.
4. THE Presentation_Assembly SHALL invoke exactly one core mutating operation during steady-state operation, namely `SimulationWorld.Tick()`.
5. WHEN the Presentation_Assembly renders a frame, THE Presentation_Assembly SHALL leave every core `Cell` state, `Agent` state, and `TickIndex` value unchanged.
6. THE Presentation_Assembly SHALL derive the harvested-crop total for display from `SimulationWorld.DischargedTotal`.
7. THE Presentation_Assembly SHALL invoke every core operation from the Unity main thread only.

### Requirement 3: Fixed-Rate Tick Driver

**User Story:** As a developer, I want simulation progression driven by a fixed tick rate independent of framerate, so that simulated outcomes do not depend on rendering performance.

#### Acceptance Criteria

1. THE Simulation_Driver SHALL expose a configurable Tick_Rate greater than zero.
2. IF a Tick_Rate less than or equal to zero is configured, THEN THE Simulation_Driver SHALL reject the value and retain the previous Tick_Rate.
3. WHEN a Unity frame elapses, THE Simulation_Driver SHALL add the frame's unscaled elapsed real time multiplied by the Speed_Multiplier to an internal time accumulator.
4. WHILE the time accumulator is greater than or equal to the Tick_Interval and the executed Tick count for the current frame is less than the Tick_Budget, THE Simulation_Driver SHALL invoke `SimulationWorld.Tick()` once and subtract one Tick_Interval from the time accumulator.
5. THE Simulation_Driver SHALL invoke `SimulationWorld.Tick()` at most Tick_Budget times within one Unity frame.
6. IF the time accumulator exceeds the product of Tick_Budget and Tick_Interval after the Tick loop completes, THEN THE Simulation_Driver SHALL clamp the time accumulator to the Tick_Interval multiplied by Tick_Budget.
7. WHEN `SimulationWorld.IsHalted` reports `true`, THE Simulation_Driver SHALL stop invoking `SimulationWorld.Tick()`.
8. FOR ALL sequences of frame durations whose unscaled sum is identical, THE Simulation_Driver SHALL invoke `SimulationWorld.Tick()` the same number of times, provided no clamp from criterion 6 occurs.

### Requirement 4: Debug Playback Controls

**User Story:** As a developer, I want to pause, single-step, and re-speed the simulation, so that I can inspect agent behaviour frame by frame.

#### Acceptance Criteria

1. WHILE the Simulation_Driver is paused, THE Simulation_Driver SHALL invoke no `SimulationWorld.Tick()` call and SHALL leave the time accumulator unchanged.
2. WHEN a step-one-tick command is issued WHILE the Simulation_Driver is paused, THE Simulation_Driver SHALL invoke `SimulationWorld.Tick()` exactly once and SHALL remain paused.
3. WHILE the Simulation_Driver is paused, THE Presentation_Assembly SHALL continue rendering frames.
4. THE Simulation_Driver SHALL expose a configurable Speed_Multiplier greater than zero.
5. IF a Speed_Multiplier less than or equal to zero is configured, THEN THE Simulation_Driver SHALL reject the value and retain the previous Speed_Multiplier.
6. WHEN the Speed_Multiplier changes, THE Simulation_Driver SHALL preserve the current time accumulator value.
7. FOR ALL Speed_Multiplier and Tick_Rate values, THE Simulation_Driver SHALL produce the same ordered sequence of core state values per Tick index as an unscaled run with the same seed.

### Requirement 5: Visual Interpolation Between Ticks

**User Story:** As a viewer, I want agents to glide between cells instead of snapping, so that motion looks continuous at any framerate.

#### Acceptance Criteria

1. THE Agent_View SHALL retain the Unity world-space position corresponding to the agent's `Position` observed at the previous Tick.
2. WHEN a frame is rendered, THE Agent_View SHALL set the rendered position to the linear interpolation between the previous-Tick world position and the current-Tick world position, using the Interpolation_Alpha.
3. THE Simulation_Driver SHALL compute the Interpolation_Alpha as the time accumulator divided by the Tick_Interval, clamped to the closed interval `[0, 1]`.
4. WHEN the Interpolation_Alpha equals `0`, THE Agent_View SHALL render the agent at the world position of the previous-Tick `GridPosition`.
5. WHEN the Interpolation_Alpha equals `1`, THE Agent_View SHALL render the agent at the world position of the current-Tick `GridPosition`.
6. WHEN a Tick completes, THE Agent_View SHALL replace the previous-Tick world position with the world position observed before that Tick.
7. WHILE an agent's `Position` is unchanged across two consecutive Ticks, THE Agent_View SHALL render the agent at the world position of that `GridPosition` for every Interpolation_Alpha value.
8. THE Agent_View SHALL rotate the agent GameObject toward the interpolation direction without altering any core `Agent` value.
9. WHILE the Simulation_Driver is paused, THE Agent_View SHALL hold the rendered position at a constant value.

### Requirement 6: Coordinate Mapping

**User Story:** As a developer, I want one authoritative conversion between grid coordinates and Unity world space, so that visuals never drift from the model.

#### Acceptance Criteria

1. THE Coordinate_Mapper SHALL convert a `GridPosition` to the Unity world position `Grid_Origin + (GridPosition.X * Tile_Size, 0, GridPosition.Y * Tile_Size)`.
2. THE Coordinate_Mapper SHALL convert a Unity world position to the `GridPosition` whose cell centre is nearest to that world position on the world `x` and `z` axes.
3. FOR ALL `GridPosition` values inside the grid bounds, converting to a Unity world position and back SHALL yield the original `GridPosition` (round-trip property).
4. FOR ALL Unity world positions whose nearest cell centre lies inside the grid bounds, converting to a `GridPosition` and back SHALL yield a world position within half of the Tile_Size of the input on the world `x` and `z` axes.
5. IF a world position maps to a `GridPosition` outside the grid bounds, THEN THE Coordinate_Mapper SHALL report the position as out of bounds without returning a `GridPosition`.
6. THE Presentation_Assembly SHALL perform every grid-to-world and world-to-grid conversion through the Coordinate_Mapper.

### Requirement 7: Cell State Projection

**User Story:** As a viewer, I want tiles to look like what the model says they are, so that the rendered field matches the simulation.

#### Acceptance Criteria

1. WHEN the Grid_View is initialised, THE Grid_View SHALL instantiate exactly one floor prefab per core `Cell`, positioned by the Coordinate_Mapper.
2. THE Grid_View SHALL assign to each floor prefab the material configured for that `Cell`'s current Cell_State.
3. THE Grid_View SHALL instantiate a crop content prefab for each `Cell` whose Cell_State is `Crop` and an obstacle content prefab for each `Cell` whose Cell_State is `Blocked`.
4. WHEN a `Cell`'s Cell_State changes between two Ticks, THE Grid_View SHALL update that cell's material and content prefab to the representation configured for the new Cell_State.
5. WHEN a `Cell`'s Cell_State changes from `Crop` to `Harvested`, THE Grid_View SHALL remove the crop content prefab instantiated for that cell.
6. THE Grid_View SHALL define a distinct visual representation for each of the four Cell_State values.
7. THE Grid_View SHALL leave every core `Cell` value unchanged during initialisation and during updates.
8. WHERE a content prefab is unassigned for a Cell_State, THE Grid_View SHALL render the floor prefab with the material configured for that Cell_State and instantiate no content prefab.

### Requirement 8: Agent State Representation

**User Story:** As a viewer, I want each agent's current state visible, so that I can tell harvesting from refuelling from an inactive agent.

#### Acceptance Criteria

1. THE State_Visual_Map SHALL define a visual representation for each of the eight State_Id values.
2. WHEN an agent's `CurrentState` changes between two Ticks, THE Agent_View SHALL apply the State_Visual_Map representation of the new State_Id.
3. WHILE an agent's `CurrentState` is `Inactive`, THE Agent_View SHALL apply the representation configured for `Inactive` and SHALL hold the agent at the world position of the agent's current `GridPosition`.
4. THE Agent_View SHALL display the agent's `Fuel`, `Load`, and `MaxLoad` values as read from the core `Agent`.
5. IF a State_Id value has no configured representation, THEN THE Agent_View SHALL apply a documented fallback representation and SHALL log a warning naming the State_Id.
6. THE Presentation_Assembly SHALL declare no enumeration that duplicates State_Id.

### Requirement 9: Agent Binding

**User Story:** As a developer, I want scene GameObjects bound to core agents by identifier, so that mismatches surface immediately instead of producing silent misrendering.

#### Acceptance Criteria

1. WHEN the World_Bootstrapper initialises, THE World_Bootstrapper SHALL register one core `Agent` per Agent_View present in the scene, using the Agent_View's authored identifier and authored start `GridPosition`.
2. THE World_Bootstrapper SHALL register a `Harvester` for each Agent_View authored with the harvester role and a `Tractor` for each Agent_View authored with the tractor role.
3. THE Agent_Binding SHALL associate each Agent_View with exactly one core `Agent` identifier.
4. IF two Agent_View instances declare the same identifier, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming the duplicated identifier.
5. IF a core `Agent` exists with no bound Agent_View, THEN THE World_Bootstrapper SHALL log a warning naming the unbound agent identifier and SHALL continue initialisation.
6. IF an Agent_View declares an identifier that matches no registered core `Agent`, THEN THE Agent_View SHALL render no agent representation and SHALL log a warning naming the unmatched identifier.
7. IF an Agent_View declares a start `GridPosition` that is out of bounds or whose Cell_State is `Blocked`, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming the identifier and the rejected position.
8. WHEN initialisation completes, THE World_Bootstrapper SHALL invoke `SimulationWorld.RedistributeAreas()` exactly once.

### Requirement 10: Refuel Stations and Dump Sites as Core World Data

**User Story:** As a developer, I want stations and dump sites known to the core, so that pathfinding and the fuel guard operate on real targets instead of scene-only positions.

#### Acceptance Criteria

1. THE World_Bootstrapper SHALL convert each refuel Site_Marker to a `GridPosition` through the Coordinate_Mapper and SHALL pass the resulting positions to the `WorldModel` constructor as refuel stations.
2. THE World_Bootstrapper SHALL convert each dump Site_Marker to a `GridPosition` through the Coordinate_Mapper and SHALL pass the resulting positions to the `WorldModel` constructor as dump sites.
3. IF a Site_Marker maps to a `GridPosition` outside the grid bounds, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming the Site_Marker.
4. IF two Site_Markers of the same kind map to the same `GridPosition`, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming both Site_Markers.
5. THE Presentation_Assembly SHALL select refuel and dump targets exclusively through core operations, and SHALL provide no nearest-station or nearest-dump search of its own.
6. THE Grid_View SHALL render each `WorldModel.RefuelStations` position and each `WorldModel.DumpSites` position at the world position produced by the Coordinate_Mapper.

### Requirement 11: World Construction and Determinism

**User Story:** As a developer, I want a seeded run to produce identical results regardless of framerate, so that behaviour is reproducible and debuggable.

#### Acceptance Criteria

1. THE World_Bootstrapper SHALL expose grid width, grid height, Tile_Size, seed, crop density, and blocked density as authored Unity Inspector values.
2. THE World_Bootstrapper SHALL construct the `SimulationConfig` from the authored values and SHALL construct the `IRandomSource` as a `DeterministicRandom` created from the authored seed.
3. IF the `SimulationConfig` constructor rejects an authored value, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error containing the rejection message.
4. THE World_Bootstrapper SHALL invoke `SimulationWorld.GenerateGrid()` exactly once per scene load.
5. THE Presentation_Assembly SHALL generate no grid content of its own.
6. FOR ALL pairs of runs with an identical seed, identical authored configuration, and identical agent registration order, the core state after `N` Ticks SHALL be identical, independent of frame durations and rendering.
7. WHERE an authored world text grid is supplied, THE World_Bootstrapper SHALL construct the `WorldModel` through `WorldModel.Parse` instead of invoking `SimulationWorld.GenerateGrid()`.
8. FOR ALL `WorldModel` instances constructed by the World_Bootstrapper, serialising the model and parsing the serialised text SHALL produce a model with identical Cell_State values at every position (round-trip property).

### Requirement 12: Removal of Duplicated Simulation Logic

**User Story:** As a developer, I want the Unity-side algorithms deleted rather than disabled, so that only one implementation of each behaviour exists.

#### Acceptance Criteria

1. THE Presentation_Assembly SHALL contain no pathfinding implementation, and `Assets/Scripts/GridScripts/GridPathFinder.cs` SHALL be absent from the project.
2. THE Presentation_Assembly SHALL contain no `TileData` matrix as authoritative grid data, and the Unity `TileState` and `TileContent` enumerations SHALL be absent from the project.
3. THE Presentation_Assembly SHALL contain no procedural terrain generation, and the `useRandomSeed`, `customSeed`, `obstacleChance`, and `cropChance` fields SHALL be absent from the Grid_View.
4. THE Presentation_Assembly SHALL contain no agent finite state machine, and the Unity `AgentState` enumeration SHALL be absent from the project.
5. THE Presentation_Assembly SHALL contain no fuel accounting, refuel-threshold decision, load accounting, or harvest decision.
6. THE Presentation_Assembly SHALL contain no tractor-to-harvester pairing, meeting point negotiation, load transfer resolution, or discharge accounting.
7. THE Presentation_Assembly SHALL contain no nearest-crop, nearest-refuel-station, nearest-dump-site, or nearest-walkable-tile search.
8. THE Presentation_Assembly SHALL contain no code path that changes a simulated outcome, so removing every Presentation_Assembly script SHALL leave the ordered sequence of core state values per Tick index unchanged for a given seed.
9. THE Presentation_Assembly SHALL retain camera control, prefab instantiation, material selection, interpolation, rotation smoothing, debug controls, and user interface concerns.

### Requirement 13: Migration Sequencing

**User Story:** As a developer, I want the project to compile and the scene to run at every step of the migration, so that integration progress is verifiable.

#### Acceptance Criteria

1. WHEN any migration step completes, THE Unity project SHALL compile with zero errors.
2. WHEN any migration step completes, THE scene `Assets/Scenes/SimulationScene.unity` SHALL enter play mode without an unhandled exception.
3. THE migration SHALL introduce the Core_Assembly and the Presentation_Assembly before any Legacy_Simulation_Script is removed.
4. WHEN a Legacy_Simulation_Script is removed, THE migration SHALL have the replacing core-backed behaviour present in the same step.
5. THE migration SHALL preserve the existing scene's prefab assignments, material assignments, and camera configuration.
6. THE Presentation_Assembly SHALL name migrated types and members in English, replacing the Spanish identifiers of the removed scripts.
