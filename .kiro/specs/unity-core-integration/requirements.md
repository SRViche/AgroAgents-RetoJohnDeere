# Requirements Document

## Introduction

This feature integrates the engine-agnostic `HarvestingCore` simulation library into the Unity presentation project `AgroAgents-RetoJohnDeere`, so that the Unity project stops owning simulation logic and becomes a pure view over the core's world model.

The core becomes fully authoritative: it owns the grid, all agent state, pathfinding, the agent finite state machines, and coordination. Unity owns only presentation concerns: assembly wiring, tick driving, prefab instantiation, material selection, camera, visual interpolation, rotation smoothing, and debug controls. Every duplicated algorithm currently living in Unity scripts is removed rather than kept behind a toggle.

The core source is brought into the Unity project behind an assembly definition that does not reference `UnityEngine`, so engine-agnosticism is enforced by the compiler rather than by convention.

The Presentation_Assembly does not depend on the Harvesting_Core directly. It depends on a Simulation_Port: a small set of interfaces and mirrored value types with no dependency on either Unity or the Harvesting_Core. One adapter, the In_Memory_Adapter, implements the Simulation_Port today by constructing and wrapping a `SimulationWorld` in process. This indirection exists so that a future networked simulation (for example, over WebSockets) can implement the same Simulation_Port and be substituted without any change to the Presentation_Assembly: the dependency is resolved at the object-instance level, in an authored scene asset, never in source. The dependency direction is one-way throughout: the Presentation_Assembly depends on the Simulation_Port, the In_Memory_Adapter depends on the Simulation_Port and the Harvesting_Core, and the Harvesting_Core depends on nothing in this project.

## Glossary

- **Harvesting_Core**: The existing engine-agnostic C# simulation library, rooted at the `HarvestingCore` namespace, whose façade type is `SimulationWorld`.
- **Core_Assembly**: The Unity assembly definition that compiles the Harvesting_Core source inside the Unity project.
- **Simulation_Port_Assembly**: The Unity assembly definition that declares the Simulation_Port: the Simulation_Session, Simulation_Connector, and Simulation_Connection interfaces, and the mirrored value types (Cell_State, State_Id, Agent_Role, and related snapshot types) that describe simulation state without depending on the Harvesting_Core or on Unity.
- **Simulation_Adapter_Assembly**: The Unity assembly definition that implements the Simulation_Port. This document specifies exactly one: the In_Memory_Adapter_Assembly.
- **In_Memory_Adapter_Assembly**: The Simulation_Adapter_Assembly that implements the Simulation_Port by constructing and wrapping a `SimulationWorld` in the same process. It is the only assembly in this design that declares a reference to both the Core_Assembly and the Simulation_Port_Assembly.
- **Presentation_Assembly**: The Unity assembly definition that compiles all Unity-side scripts of the project.
- **Simulation_Port**: The compile-time abstraction the Presentation_Assembly depends on in place of the Harvesting_Core, comprising the Simulation_Session, Simulation_Connector, and Simulation_Connection interfaces and their associated value types.
- **Simulation_Connector**: The Simulation_Port interface that opens a Simulation_Session from a Session_Request.
- **Simulation_Connection**: The Simulation_Port interface representing an in-progress or completed attempt to open a Simulation_Session, polled once per frame until it completes or fails.
- **Simulation_Session**: The Simulation_Port interface representing a live, running simulation: it exposes one World_Snapshot captured at connection time, a `RequestTick` operation, and a stream of Simulation_Update values, one per completed Tick.
- **Session_Request**: The value passed to a Simulation_Connector describing the simulation to open: grid dimensions, seed, densities, terrain costs, agents, and site positions.
- **World_Snapshot**: The complete simulation state captured once, at the moment a Simulation_Session is opened.
- **Simulation_Update**: The incremental simulation state delivered once per completed Tick, comprising the cells whose Cell_State changed, the full current agent list, the discharged total, and the halted flag.
- **Simulation_Driver**: The Presentation_Assembly component that owns the single Simulation_Session instance, advances simulation Ticks through it, and exposes debug controls.
- **Tick**: One discrete simulation step, produced by one call to `RequestTick()` on a Simulation_Session and observed by the Presentation_Assembly as one Simulation_Update.
- **Tick_Rate**: The configured number of Ticks the Simulation_Driver targets per second of unscaled real time.
- **Tick_Interval**: The real-time duration of one Tick, equal to the Speed_Multiplier-scaled reciprocal of Tick_Rate.
- **Speed_Multiplier**: The positive factor applied to simulation time progression by the Simulation_Driver.
- **Tick_Budget**: The maximum number of Ticks the Simulation_Driver executes within a single Unity frame.
- **Interpolation_Alpha**: The value in the closed interval `[0, 1]` describing progress between the previously rendered Tick state and the current Tick state.
- **Coordinate_Mapper**: The Presentation_Assembly component that converts between a Simulation_Port grid position and Unity world-space positions.
- **Grid_Origin**: The Unity world-space position corresponding to grid position `(0, 0)`.
- **Tile_Size**: The Unity world-space edge length of one grid cell.
- **Grid_View**: The Presentation_Assembly component that spawns and updates floor prefabs, content prefabs, and floor materials from the Simulation_Session's World_Snapshot and Simulation_Update values.
- **Agent_View**: The Presentation_Assembly component attached to one agent GameObject that renders the position, orientation, and state of exactly one agent, as reported by the Simulation_Session.
- **Agent_Binding**: The association between one Simulation_Session agent identifier and one Agent_View instance.
- **State_Visual_Map**: The Presentation_Assembly data that maps each State_Id value to a visual representation.
- **Cell_State**: The Simulation_Port enumeration with values `Empty`, `Crop`, `Blocked`, `Harvested`, mirroring `HarvestingCore.World.CellState` member-for-member. The Simulation_Port declares this enumeration independently of the Harvesting_Core; the In_Memory_Adapter is responsible for translating between the two without loss.
- **State_Id**: The Simulation_Port enumeration with values `Idle`, `Harvest`, `GoToRefuel`, `GoToDump`, `GoToMeetingPoint`, `WaitTractor`, `WaitHarvester`, `Inactive`, mirroring `HarvestingCore.Agents.StateId` member-for-member, on the same terms as Cell_State above.
- **Agent_Role**: The Simulation_Port enumeration with values `Harvester`, `Tractor`, mirroring `HarvestingCore.Agents.AgentRole` member-for-member, on the same terms as Cell_State above.
- **World_Bootstrapper**: The Presentation_Assembly component that builds a Session_Request from authored Unity Inspector values, opens a Simulation_Session through an authored Simulation_Connector, and hands the resulting session to the Simulation_Driver once the connection completes.
- **Site_Marker**: A Unity scene object authored to designate a refuel station or a dump site position.
- **Legacy_Simulation_Script**: Any Unity script in the project that decides simulation outcomes, listed explicitly in Requirement 12.

## Assumptions

The following defaults keep the specification complete and remain open to revision during design.

1. Grid axis mapping is grid position `X` to Unity world `x` and grid position `Y` to Unity world `z`, with Unity `y` reserved for height.
2. Grid_Origin corresponds to grid position `(0, 0)`, which is the core's top-left origin, so increasing grid position `Y` increases Unity world `z`.
3. The Unity project runs exactly one Simulation_Session instance per loaded scene.
4. Default Tick_Rate is `4` Ticks per second, default Speed_Multiplier is `1.0`, and default Tick_Budget is `4` Ticks per frame.
5. Agent identifiers are authored in the scene and are unique within the scene.
6. Refuel stations and dump sites occupy grid cells inside the world bounds; positions outside the grid are not supported.
7. The core's `MoveOrder` permits eight-directional movement, so interpolation covers diagonal transitions between adjacent cells.
8. Existing Unity identifiers use Spanish names (`TileState.Cosechado`, `TileContent.Cultivo`) while the core uses English names; the integration adopts the core's English vocabulary for all new and migrated presentation types.
9. Unity `6000.5.7f1` compiles `netstandard2.1`-compatible C# 8.0 source, so the Harvesting_Core source compiles unchanged inside the Core_Assembly.

## Requirements

### Requirement 1: Compile-Time Assembly Boundaries

**User Story:** As a developer, I want the simulation core compiled into its own Unity assembly with no engine reference, and the presentation layer compiled with no reference to the simulation core, so that both engine coupling and implementation coupling are impossible rather than merely discouraged.

#### Acceptance Criteria

1. THE Core_Assembly SHALL compile every Harvesting_Core source file present in the Unity project.
2. THE Core_Assembly SHALL declare no reference to `UnityEngine`, `UnityEditor`, or any Unity-provided assembly.
3. IF a Core_Assembly source file references a Unity type, THEN THE Core_Assembly SHALL fail compilation with an unresolved-type error.
4. THE Presentation_Assembly SHALL declare a reference to the Simulation_Port_Assembly and SHALL declare no reference to the Core_Assembly.
5. THE Core_Assembly SHALL declare no reference to the Presentation_Assembly, and THE Simulation_Port_Assembly SHALL declare no reference to the Core_Assembly.
6. THE Core_Assembly SHALL contain no type that derives from `UnityEngine.Object`.
7. WHEN the Unity project is compiled, THE Presentation_Assembly SHALL contain every Unity script of the project and the Core_Assembly SHALL contain no Unity script of the project.
8. IF a Presentation_Assembly source file references a Harvesting_Core type, THEN THE Presentation_Assembly SHALL fail compilation with an unresolved-type error.
9. THE In_Memory_Adapter_Assembly SHALL declare a reference to the Core_Assembly and to the Simulation_Port_Assembly.
10. THE Simulation_Port_Assembly SHALL declare no reference to `UnityEngine`, `UnityEditor`, or any Unity-provided assembly.

### Requirement 2: Simulation as Single Source of Truth, Accessed Through the Simulation_Port

**User Story:** As a developer, I want the presentation layer to depend only on an abstraction over the simulation, so that the rendered scene always reflects one authoritative model and the concrete simulation implementation can be replaced without changing presentation code.

#### Acceptance Criteria

1. THE Presentation_Assembly SHALL read grid cell state exclusively from the World_Snapshot and Simulation_Update values delivered by the Simulation_Session.
2. THE Presentation_Assembly SHALL read agent position, fuel, load, meeting point, and state exclusively from the World_Snapshot and Simulation_Update values delivered by the Simulation_Session.
3. THE Presentation_Assembly SHALL store no field that duplicates a Cell_State, agent position, agent fuel, agent load, or agent State_Id value as authoritative data.
4. THE Presentation_Assembly SHALL invoke exactly one Simulation_Session mutating operation during steady-state operation, namely `RequestTick()`.
5. WHEN the Presentation_Assembly renders a frame, THE Presentation_Assembly SHALL leave every value of the most recently delivered World_Snapshot and Simulation_Update unchanged.
6. THE Presentation_Assembly SHALL derive the harvested-crop total for display from the Simulation_Update's discharged total.
7. THE Presentation_Assembly SHALL invoke every Simulation_Port operation from the Unity main thread only.
8. THE Presentation_Assembly SHALL declare no reference, direct or transitive, to the Core_Assembly.
9. THE World_Bootstrapper SHALL select the concrete Simulation_Connector through an authored scene reference, and no Presentation_Assembly source file SHALL name a specific Simulation_Adapter_Assembly type.

### Requirement 3: Fixed-Rate Tick Driver

**User Story:** As a developer, I want simulation progression driven by a fixed tick rate independent of framerate, so that simulated outcomes do not depend on rendering performance.

#### Acceptance Criteria

1. THE Simulation_Driver SHALL expose a configurable Tick_Rate greater than zero.
2. IF a Tick_Rate less than or equal to zero is configured, THEN THE Simulation_Driver SHALL reject the value and retain the previous Tick_Rate.
3. WHEN a Unity frame elapses, THE Simulation_Driver SHALL add the frame's unscaled elapsed real time multiplied by the Speed_Multiplier to an internal time accumulator.
4. WHILE the time accumulator is greater than or equal to the Tick_Interval and the executed Tick count for the current frame is less than the Tick_Budget, THE Simulation_Driver SHALL invoke `RequestTick()` on the Simulation_Session once and subtract one Tick_Interval from the time accumulator.
5. THE Simulation_Driver SHALL invoke `RequestTick()` on the Simulation_Session at most Tick_Budget times within one Unity frame.
6. IF the time accumulator exceeds the product of Tick_Budget and Tick_Interval after the Tick loop completes, THEN THE Simulation_Driver SHALL clamp the time accumulator to the Tick_Interval multiplied by Tick_Budget.
7. WHEN the most recently delivered Simulation_Update reports its halted flag as `true`, THE Simulation_Driver SHALL stop invoking `RequestTick()`.
8. FOR ALL sequences of frame durations whose unscaled sum is identical, THE Simulation_Driver SHALL invoke `RequestTick()` the same number of times, provided no clamp from criterion 6 occurs.

### Requirement 4: Debug Playback Controls

**User Story:** As a developer, I want to pause, single-step, and re-speed the simulation, so that I can inspect agent behaviour frame by frame.

#### Acceptance Criteria

1. WHILE the Simulation_Driver is paused, THE Simulation_Driver SHALL invoke no `RequestTick()` call and SHALL leave the time accumulator unchanged.
2. WHEN a step-one-tick command is issued WHILE the Simulation_Driver is paused, THE Simulation_Driver SHALL invoke `RequestTick()` on the Simulation_Session exactly once and SHALL remain paused.
3. WHILE the Simulation_Driver is paused, THE Presentation_Assembly SHALL continue rendering frames.
4. THE Simulation_Driver SHALL expose a configurable Speed_Multiplier greater than zero.
5. IF a Speed_Multiplier less than or equal to zero is configured, THEN THE Simulation_Driver SHALL reject the value and retain the previous Speed_Multiplier.
6. WHEN the Speed_Multiplier changes, THE Simulation_Driver SHALL preserve the current time accumulator value.
7. FOR ALL Speed_Multiplier and Tick_Rate values, THE Simulation_Driver SHALL produce the same ordered sequence of Simulation_Update values per Tick index as an unscaled run with the same seed.

### Requirement 5: Visual Interpolation Between Ticks

**User Story:** As a viewer, I want agents to glide between cells instead of snapping, so that motion looks continuous at any framerate.

#### Acceptance Criteria

1. THE Agent_View SHALL retain the Unity world-space position corresponding to the agent's position observed at the previous Tick.
2. WHEN a frame is rendered, THE Agent_View SHALL set the rendered position to the linear interpolation between the previous-Tick world position and the current-Tick world position, using the Interpolation_Alpha.
3. THE Simulation_Driver SHALL compute the Interpolation_Alpha as the time accumulator divided by the Tick_Interval, clamped to the closed interval `[0, 1]`.
4. WHEN the Interpolation_Alpha equals `0`, THE Agent_View SHALL render the agent at the world position of its previous-Tick grid position.
5. WHEN the Interpolation_Alpha equals `1`, THE Agent_View SHALL render the agent at the world position of its current-Tick grid position.
6. WHEN a Tick completes, THE Agent_View SHALL replace the previous-Tick world position with the world position observed before that Tick.
7. WHILE an agent's position is unchanged across two consecutive Ticks, THE Agent_View SHALL render the agent at the world position of that grid position for every Interpolation_Alpha value.
8. THE Agent_View SHALL rotate the agent GameObject toward the interpolation direction without altering any value delivered by the Simulation_Session.
9. WHILE the Simulation_Driver is paused, THE Agent_View SHALL hold the rendered position at a constant value.

### Requirement 6: Coordinate Mapping

**User Story:** As a developer, I want one authoritative conversion between grid coordinates and Unity world space, so that visuals never drift from the model.

#### Acceptance Criteria

1. THE Coordinate_Mapper SHALL convert a Simulation_Port grid position to the Unity world position `Grid_Origin + (X * Tile_Size, 0, Y * Tile_Size)`.
2. THE Coordinate_Mapper SHALL convert a Unity world position to the Simulation_Port grid position whose cell centre is nearest to that world position on the world `x` and `z` axes.
3. FOR ALL Simulation_Port grid position values inside the grid bounds, converting to a Unity world position and back SHALL yield the original grid position (round-trip property).
4. FOR ALL Unity world positions whose nearest cell centre lies inside the grid bounds, converting to a Simulation_Port grid position and back SHALL yield a world position within half of the Tile_Size of the input on the world `x` and `z` axes.
5. IF a world position maps to a Simulation_Port grid position outside the grid bounds, THEN THE Coordinate_Mapper SHALL report the position as out of bounds without returning a grid position.
6. THE Presentation_Assembly SHALL perform every grid-to-world and world-to-grid conversion through the Coordinate_Mapper.

### Requirement 7: Cell State Projection

**User Story:** As a viewer, I want tiles to look like what the model says they are, so that the rendered field matches the simulation.

#### Acceptance Criteria

1. WHEN the Grid_View is initialised, THE Grid_View SHALL instantiate exactly one floor prefab per cell present in the World_Snapshot, positioned by the Coordinate_Mapper.
2. THE Grid_View SHALL assign to each floor prefab the material configured for that cell's current Cell_State.
3. THE Grid_View SHALL instantiate a crop content prefab for each cell whose Cell_State is `Crop` and an obstacle content prefab for each cell whose Cell_State is `Blocked`.
4. WHEN a cell's Cell_State changes between two Ticks, THE Grid_View SHALL update that cell's material and content prefab to the representation configured for the new Cell_State.
5. WHEN a cell's Cell_State changes from `Crop` to `Harvested`, THE Grid_View SHALL remove the crop content prefab instantiated for that cell.
6. THE Grid_View SHALL define a distinct visual representation for each of the four Cell_State values.
7. THE Grid_View SHALL leave every value of the most recently delivered World_Snapshot and Simulation_Update unchanged during initialisation and during updates.
8. WHERE a content prefab is unassigned for a Cell_State, THE Grid_View SHALL render the floor prefab with the material configured for that Cell_State and instantiate no content prefab.

### Requirement 8: Agent State Representation

**User Story:** As a viewer, I want each agent's current state visible, so that I can tell harvesting from refuelling from an inactive agent.

#### Acceptance Criteria

1. THE State_Visual_Map SHALL define a visual representation for each of the eight State_Id values.
2. WHEN an agent's state changes between two Ticks, THE Agent_View SHALL apply the State_Visual_Map representation of the new State_Id.
3. WHILE an agent's state is `Inactive`, THE Agent_View SHALL apply the representation configured for `Inactive` and SHALL hold the agent at the world position of the agent's current grid position.
4. THE Agent_View SHALL display the agent's fuel, load, and maximum load values as read from the Simulation_Session.
5. IF a State_Id value has no configured representation, THEN THE Agent_View SHALL apply a documented fallback representation and SHALL log a warning naming the State_Id.
6. THE Presentation_Assembly SHALL declare no enumeration describing agent state that is independent of, and inconsistent with, the Simulation_Port's State_Id.

### Requirement 9: Agent Binding

**User Story:** As a developer, I want scene GameObjects bound to simulation agents by identifier, so that mismatches surface immediately instead of producing silent misrendering.

#### Acceptance Criteria

1. THE World_Bootstrapper SHALL include one agent entry per Agent_View present in the scene in the Session_Request, using the Agent_View's authored identifier and authored start grid position.
2. THE World_Bootstrapper SHALL mark each agent entry in the Session_Request with the harvester Agent_Role for each Agent_View authored with the harvester role and the tractor Agent_Role for each Agent_View authored with the tractor role.
3. THE Agent_Binding SHALL associate each Agent_View with exactly one agent identifier reported by the Simulation_Session.
4. IF two Agent_View instances declare the same identifier, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming the duplicated identifier.
5. IF the World_Snapshot contains an agent with no bound Agent_View, THEN THE World_Bootstrapper SHALL log a warning naming the unbound agent identifier and SHALL continue initialisation.
6. IF an Agent_View declares an identifier that matches no agent in the World_Snapshot, THEN THE Agent_View SHALL render no agent representation and SHALL log a warning naming the unmatched identifier.
7. IF an Agent_View declares a start grid position that is out of bounds or whose Cell_State is `Blocked`, THEN THE Simulation_Connection SHALL fail and THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming the identifier and the rejected position.
8. WHERE the connected Simulation_Adapter_Assembly is the In_Memory_Adapter_Assembly, THE In_Memory_Adapter_Assembly SHALL invoke `SimulationWorld.RedistributeAreas()` exactly once while opening a Simulation_Session.

### Requirement 10: Refuel Stations and Dump Sites as Simulation-Owned Data

**User Story:** As a developer, I want stations and dump sites known to the simulation, so that pathfinding and the fuel guard operate on real targets instead of scene-only positions.

#### Acceptance Criteria

1. THE World_Bootstrapper SHALL convert each refuel Site_Marker to a Simulation_Port grid position through the Coordinate_Mapper and SHALL include the resulting positions in the Session_Request as refuel stations.
2. THE World_Bootstrapper SHALL convert each dump Site_Marker to a Simulation_Port grid position through the Coordinate_Mapper and SHALL include the resulting positions in the Session_Request as dump sites.
3. IF a Site_Marker maps to a Simulation_Port grid position outside the grid bounds, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming the Site_Marker.
4. IF two Site_Markers of the same kind map to the same Simulation_Port grid position, THEN THE World_Bootstrapper SHALL reject initialisation and SHALL log an error naming both Site_Markers.
5. THE Presentation_Assembly SHALL select refuel and dump targets exclusively through Simulation_Session operations, and SHALL provide no nearest-station or nearest-dump search of its own.
6. THE Grid_View SHALL render each refuel station position and each dump site position reported in the World_Snapshot at the world position produced by the Coordinate_Mapper.

### Requirement 11: World Construction and Determinism

**User Story:** As a developer, I want a seeded run to produce identical results regardless of framerate, so that behaviour is reproducible and debuggable.

#### Acceptance Criteria

1. THE World_Bootstrapper SHALL expose grid width, grid height, Tile_Size, seed, crop density, and blocked density as authored Unity Inspector values.
2. WHERE the connected Simulation_Adapter_Assembly is the In_Memory_Adapter_Assembly, THE In_Memory_Adapter_Assembly SHALL construct the `SimulationConfig` from the Session_Request's values and SHALL construct the `IRandomSource` as a `DeterministicRandom` created from the Session_Request's seed.
3. IF the In_Memory_Adapter_Assembly's `SimulationConfig` construction rejects a value from the Session_Request, THEN THE Simulation_Connection SHALL fail and THE World_Bootstrapper SHALL reject initialisation and SHALL log an error containing the rejection message.
4. WHERE the Session_Request specifies no authored world text, THE In_Memory_Adapter_Assembly SHALL invoke `SimulationWorld.GenerateGrid()` exactly once while opening a Simulation_Session.
5. THE Presentation_Assembly SHALL generate no grid content of its own.
6. FOR ALL pairs of Simulation_Session instances opened from an identical Session_Request, the sequence of Simulation_Update values after `N` Ticks SHALL be identical, independent of frame durations and rendering.
7. WHERE the Session_Request specifies authored world text, THE In_Memory_Adapter_Assembly SHALL construct the `WorldModel` through `WorldModel.Parse` instead of invoking `SimulationWorld.GenerateGrid()`.
8. FOR ALL `WorldModel` instances the In_Memory_Adapter_Assembly constructs while opening a Simulation_Session, serialising the model and parsing the serialised text SHALL produce a model with identical Cell_State values at every position (round-trip property).

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
8. THE Presentation_Assembly SHALL contain no code path that changes a simulated outcome, so removing every Presentation_Assembly script SHALL leave the ordered sequence of Simulation_Update values per Tick index unchanged for a given seed.
9. THE Presentation_Assembly SHALL retain camera control, prefab instantiation, material selection, interpolation, rotation smoothing, debug controls, and user interface concerns.

### Requirement 13: Migration Sequencing

**User Story:** As a developer, I want the project to compile and the scene to run at every step of the migration, so that integration progress is verifiable.

#### Acceptance Criteria

1. WHEN any migration step completes, THE Unity project SHALL compile with zero errors.
2. WHEN any migration step completes, THE scene `Assets/Scenes/SimulationScene.unity` SHALL enter play mode without an unhandled exception.
3. THE migration SHALL introduce the Core_Assembly, the Simulation_Port_Assembly, the In_Memory_Adapter_Assembly, and the Presentation_Assembly before any Legacy_Simulation_Script is removed.
4. WHEN a Legacy_Simulation_Script is removed, THE migration SHALL have the replacing Simulation_Session-backed behaviour present in the same step.
5. THE migration SHALL preserve the existing scene's prefab assignments, material assignments, and camera configuration.
6. THE Presentation_Assembly SHALL name migrated types and members in English, replacing the Spanish identifiers of the removed scripts.
