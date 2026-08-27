# Implementation Plan

Task groups follow the design's Migration Plan (8 steps). Requirement 13.1 and 13.2 demand the Unity project compiles and the scene enters play mode at the end of every group, so the group order is fixed and must not be rearranged. Within a group, tasks are ordered so nothing references a type that does not yet exist.

Property numbers refer to the design's Correctness Properties section. Each property gets exactly one property-based test, placed in the earliest group where its subject exists. Because the port and the in-memory adapter (`AgroAgents.SimulationPort`, `AgroAgents.InMemoryAdapter`) are both Unity-free assemblies introduced in group 2, several property tests that used to need EditMode now live in the `dotnet` host from group 2 onward: Properties 10, 11, 12, 17, 18, 19, 21, 22, 23.

## 1. Assembly boundary

- [x] 1. Add `AgenticModel` as a git submodule at `AgroAgents-RetoJohnDeere/External/AgenticModel`
  - Submodule sits outside `Assets/` so Unity never writes `.meta` files into the core repo
  - _Requirements: 1.1_
  - _Design: Decision A_

- [ ] 2. Redirect the core's MSBuild output away from the package root
  - Modify `External/AgenticModel/src/HarvestingCore/HarvestingCore.csproj`: add the `BaseOutputPath` and `BaseIntermediateOutputPath` property group from Decision A, pointing at `../../artifacts/`
  - Add `artifacts/` to the core repo's `.gitignore`
  - Verify `dotnet build HarvestingCore.sln` still succeeds from the core repo root
  - _Requirements: 1.1_
  - _Design: Decision A_

- [ ] 3. Delete the empty `External/AgenticModel/src/HarvestingCore/__tests__/` folder
  - Nothing under the package root may be test source; core tests live in a sibling project (task 11)
  - _Requirements: 1.1_
  - _Design: Decision A_

- [ ] 4. Create the core UPM package manifest and assembly definition
  - Create `External/AgenticModel/src/HarvestingCore/package.json` with the contents given in Decision A
  - Create `External/AgenticModel/src/HarvestingCore/HarvestingCore.asmdef` exactly as specified in Decision B: `noEngineReferences: true`, `autoReferenced: false`, empty `references`, `overrideReferences: true`, `rootNamespace` `HarvestingCore`
  - _Requirements: 1.1, 1.2, 1.5, 1.6_
  - _Design: Decision A, Decision B_

- [ ] 5. Register the core package with Unity
  - Modify `Packages/manifest.json`: add `"com.agroagents.harvestingcore": "file:../External/AgenticModel/src/HarvestingCore"`
  - Confirm Unity imports the package and compiles `HarvestingCore` as its own assembly
  - _Requirements: 1.1, 1.7_
  - _Design: Decision A_

- [ ] 6. Create the presentation assembly definition with an empty reference list
  - Create `Assets/Scripts/AgroAgents.Presentation.asmdef` exactly as specified in Decision B: `rootNamespace` `AgroAgents.Presentation`, `noEngineReferences: false`, and — unlike a direct-reference design — an **empty** `references` array
  - `AgroAgents.SimulationPort` does not exist yet (it is added in group 2), so the reference to it is deferred to group 3, once it does; this asmdef never references `HarvestingCore` at any point in the migration
  - All existing scripts under `Assets/Scripts/` now compile into this assembly; no script is modified in this group
  - _Requirements: 1.4, 1.7, 1.8, 13.3_
  - _Design: Decision B_

- [ ] 7. Manually verify the compile-time engine barrier (one-off, not automated)
  - Temporarily add `using UnityEngine;` and a `Vector3` field to any file under the core package root
  - Confirm the compiler reports `CS0246` for `Vector3` in the `HarvestingCore` assembly, then revert the edit
  - Automating this needs a scripted compile of a deliberately broken tree, which is out of scope
  - _Requirements: 1.3_
  - _Design: Decision B, Testing Strategy_

## 2. Port and in-memory adapter

The port and its one adapter are introduced unused: `AgroAgents.Presentation` still has an empty `references` array (group 1), so nothing in the presentation assembly can see either new assembly yet. `GridManager` and the legacy controllers are still untouched and still the live path. This group is where `HarvestingCore` gets a second consumer, `AgroAgents.InMemoryAdapter`, besides the eventual `WorldBootstrapper`. It is placed ahead of the coordinate mapper and tick accumulator because `CoordinateMapper` is written directly against `PortGridPosition`, so the port must exist first.

- [ ] 8. Create the `AgroAgents.SimulationPort` assembly and its DTOs
  - Create `Assets/Scripts/Port/AgroAgents.SimulationPort.asmdef` exactly as specified in Decision B1: empty `references`, `noEngineReferences: true`, `autoReferenced: false`, `rootNamespace` `AgroAgents.SimulationPort`
  - Create `PortGridPosition`, `PortCellState`, `PortStateId`, `PortAgentRole`, `PortHeuristicKind`, `PortCellSnapshot`, `PortAgentSnapshot`, `WorldSnapshot`, `WorldUpdate`, `PortAgentSpec`, `SessionRequest` exactly as given in Components and Interfaces. `PortHeuristicKind` mirrors `HarvestingCore.Configuration.HeuristicKind` by ordinal; the other three enums mirror `CellState`, `StateId`, `AgentRole` member-for-member
  - _Requirements: 1.4, 1.5, 1.10, 8.6, 9.1, 9.2, 10.1, 10.2, 11.1, 12.2_
  - _Design: Decision B1, Components and Interfaces, Data Models_

- [ ] 9. Declare the port interfaces
  - In `AgroAgents.SimulationPort`, add `ISimulationSession` (`InitialSnapshot`, `RequestTick()`, `UpdateReceived` event, `IDisposable`), `ISimulationConnector` (`Connect(SessionRequest)`), and `ISimulationConnection` (`IsComplete`, `Failed`, `Error`, `Warnings`, `Session`, `Poll()`), exactly as specified in Components and Interfaces
  - _Requirements: 2.1, 2.2, 2.4, 2.9_
  - _Design: Decision B1, Components and Interfaces_

- [ ] 10. Create the `AgroAgents.InMemoryAdapter` assembly and its connector/session
  - Create `Assets/Scripts/Adapters/InMemory/AgroAgents.InMemoryAdapter.asmdef` exactly as specified in Decision B1: `references` containing `AgroAgents.SimulationPort` and `HarvestingCore`, `autoReferenced: true`, `noEngineReferences: false`
  - Implement `[Serializable] InMemorySimulationConnector : ISimulationConnector` and `InMemorySimulationSession : ISimulationSession`, wrapping one `SimulationWorld`. `RequestTick()` snapshots cell states, calls `SimulationWorld.Tick()`, diffs cell states for `WorldUpdate.ChangedCells`, maps agents via `MapAgents`, and raises `UpdateReceived` before returning (Decision D, "in-memory: synchronous")
  - Implement the mapping functions (`MapCellState`, `MapStateId`, `MapAgentRole`, `MapAgents`, and the reverse `PortGridPosition` → `GridPosition` direction) as exhaustive `switch` expressions with no default arm, entirely inside this assembly. Nothing upstream of the port sees a `HarvestingCore` type
  - _Requirements: 1.9, 2.1, 2.2, 2.4, 8.6, 9.8, 12.2_
  - _Design: Decision B1, Components and Interfaces (InMemorySimulationConnector / InMemorySimulationSession), Decision E_

- [ ] 11. Implement `InMemorySimulationConnection.TryBuildWorld`
  - Implement `internal sealed class InMemorySimulationConnection : ISimulationConnection` and its `internal static SimulationWorld TryBuildWorld(SessionRequest request, out string error, out List<string> warnings)`, Unity-free and testable in the `dotnet` host
  - `Poll()` completes synchronously on its first call: build `SimulationConfig` inside `try/catch (ArgumentOutOfRangeException)` (Req 11.3); build `IRandomSource` as `DeterministicRandom(seed)` (Req 11.2); build the `WorldModel` (`AuthoredGridText == null` → `GenerateGrid()` exactly once, Req 11.4; otherwise → `WorldModel.Parse`, no `GenerateGrid()` call, Req 11.7); validate each agent's start cell against the now-known grid, naming id and position on failure (Req 9.7); construct and register agents in sorted order (Req 9.1, 9.2); call `RedistributeAreas()` exactly once (Req 9.8); wrap the result in an `InMemorySimulationSession`
  - Widen the `SessionRequest`'s `double` density/factor fields into the `SimulationConfig` constructor's own `double` parameters
  - A second `Poll()` call after `IsComplete` is already true is a no-op
  - Use the exact message shapes from the design's Error Handling table for every hard-failure and soft-warning row attributed to `InMemorySimulationConnection`
  - _Requirements: 9.4, 9.5, 9.7, 10.1, 10.2, 11.2, 11.3, 11.4, 11.7_
  - _Design: Decision G', Error Handling_

- [ ] 12. Create the core-side test project and add it to the solution
  - Create `AgenticModel/tests/HarvestingCore.Tests/HarvestingCore.Tests.csproj` targeting `net8.0` with a `PackageReference` on CsCheck, a `ProjectReference` on `src/HarvestingCore/HarvestingCore.csproj`, and plain `ProjectReference`s on the (also Unity-free) `AgroAgents.SimulationPort` and `AgroAgents.InMemoryAdapter` source
  - Add the test project to `HarvestingCore.sln`. `src/HarvestingCore/HarvestingCore.csproj` gains nothing, so the shipped library keeps its zero references
  - Configure property tests for a minimum of 100 iterations; failures are pinned by adding the printed CsCheck seed to the `Sample` call
  - _Requirements: 11.6_
  - _Design: Testing Strategy_

- [ ] 13. Write the enum-mirroring property test
  - Create `AgenticModel/tests/HarvestingCore.Tests/EnumMirrorProperties.cs`
  - Property 22: for every `HarvestingCore.CellState` value and every `HarvestingCore.Agents.StateId` value, exhaustively (small finite domain, not randomly sampled); assert the adapter's mapping function is total and injective and that the two enumerations have equal member counts
  - _Requirements: 8.6, 12.2_
  - _Design: Correctness Property 22_

- [ ] 14. Write the bootstrap validation and request-fidelity property tests
  - Create `BootstrapProperties.cs` in the `dotnet` test project
  - Property 17: random `PortAgentSpec` sets with an injected duplicate id; assert `TryBuildWorld` returns null, the error names the duplicated id, and no agent is registered
  - Property 18: `PortAgentSpec` with an out-of-bounds or `Blocked` start; assert null result and an error containing both id and position
  - Property 19: random distinct-id spec sets and arbitrary permutations of them; assert equal `RegistrationIndex` per id and identical core state after `N` ticks
  - Property 21: density pairs constrained to sum above 1, plus out-of-range individual tunables; assert null result and an error containing the `ArgumentOutOfRangeException` text
  - Property 23: random valid `SessionRequest`s; after `Poll()` reaches `IsComplete`, assert `Session.InitialSnapshot.Width`/`Height` equal the request's, `Agents` contains exactly one entry per requested id at its requested start position, and `RefuelStations`/`DumpSites` equal the request's lists in the same order
  - _Requirements: 9.1, 9.2, 9.4, 9.7, 10.1, 10.2, 11.3_
  - _Design: Correctness Properties 17, 18, 19, 21, 23_

- [ ] 15. Write the determinism and `WorldModel` round-trip property tests
  - Create `DeterminismProperties.cs` and `WorldModelRoundTripProperties.cs` in the `dotnet` test project
  - Property 10: seed, config, 1-6 agents, `N` in `[1, 200]`, two positive `SpeedMultiplier` values with differing frame durations, both driven through `ISimulationSession.RequestTick()`; assert identical serialised `WorldModel`, `TickIndex`, `DischargedTotal`, and per-agent state at every tick index
  - Property 11: same fixture, two different frame-duration sequences at a fixed multiplier; assert identical `WorldUpdate` state after `N` ticks
  - Property 12: generated and parsed `WorldModel`s over random dimensions and densities; assert `Parse(Serialize(m))` yields identical `CellState` at every position
  - _Requirements: 3.8, 4.7, 11.6, 11.8, 12.8_
  - _Design: Correctness Properties 10, 11, 12_

## 3. Coordinate mapper and tick accumulator

Both types are introduced unused. `GridManager.GridToWorld` still exists and is still the live path, so the project compiles unchanged.

- [ ] 16. Add `AgroAgents.SimulationPort` as the presentation assembly's first reference
  - Modify `Assets/Scripts/AgroAgents.Presentation.asmdef`: `references` gains `AgroAgents.SimulationPort`
  - No file under `Assets/Scripts/` yet uses a port type; this task exists only to make the reference available for task 17
  - _Requirements: 1.4_
  - _Design: Migration Plan Step 3_

- [ ] 17. Implement `CoordinateMapper`
  - Create `Assets/Scripts/Mapping/CoordinateMapper.cs` in namespace `AgroAgents.Presentation.Mapping`
  - Immutable plain C# class, not a `MonoBehaviour`, with the public surface given in Components and Interfaces: `GridOrigin`, `TileSize`, `Width`, `Height`, `ToWorld(PortGridPosition)`, `ToWorld(PortGridPosition, float height)`, `TryToGrid(Vector3, out PortGridPosition)`, `InBounds(PortGridPosition)`, `GridCentreWorld`
  - `ToWorld` is `GridOrigin + new Vector3(p.X * TileSize, 0f, p.Y * TileSize)`; `TryToGrid` rounds local x/z over `TileSize` with `Mathf.RoundToInt` and returns `false` without producing a position when the rounded cell falls outside `[0,Width) × [0,Height)`
  - _Requirements: 6.1, 6.2, 6.5, 6.6_
  - _Design: Decision C (CoordinateMapper)_

- [ ] 18. Implement `TickPlan` and `TickAccumulator`
  - Create `Assets/Scripts/Simulation/TickPlan.cs` and `Assets/Scripts/Simulation/TickAccumulator.cs` in namespace `AgroAgents.Presentation.Simulation`
  - `TickPlan` is a readonly struct carrying `TickCount`, `InterpolationAlpha`, `Clamped`
  - `TickAccumulator` is a plain C# class with the public surface given in Components and Interfaces. `TickRate`, `TickBudget` and `SpeedMultiplier` setters reject non-positive (and for budget, sub-1) values and retain the previous value rather than clamping
  - Implement `Advance(float deltaSeconds, bool halted)` exactly per the Decision D pseudocode: halted zeroes the accumulator and returns a zero plan; paused returns early with `TickCount == 0` and an untouched accumulator; otherwise add `dt * SpeedMultiplier`, drain whole intervals up to `TickBudget`, clamp the remainder to `TickInterval * TickBudget`, then compute alpha as `Clamp01(accumulated / TickInterval)`
  - `Advance` never calls `RequestTick()`; it returns a count. `RequestSingleStep()` returns 1 when paused and does not touch the accumulator
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.2, 4.4, 4.5, 4.6, 5.3_
  - _Design: Decision D_

- [ ] 19. Write the `TickAccumulator` property tests in the `dotnet` host
  - Create `AgenticModel/tests/HarvestingCore.Tests/TickAccumulatorProperties.cs`. Each test carries the tagging comment `// Feature: unity-core-integration, Property N: <statement>`
  - Property 4: two duration sequences with equal sums, the second built as a repartition of the same total to avoid float-sum drift, durations in `(0, 0.5]`; assert equal total `TickCount`, skipping samples where `Clamped` fired
  - Property 5: duration in `(0, 10]`, `tickRate` in `[0.1, 240]`, `tickBudget` in `[1, 64]`, `speed` in `[0.01, 100]`; assert `TickCount` in `[0, TickBudget]` and the accumulator in `[0, TickInterval * TickBudget]`
  - Property 6: arbitrary positive durations and settings over a sequence of `Advance` calls; assert every `InterpolationAlpha` lies in `[0, 1]`
  - Property 7: arbitrary non-positive floats assigned to `TickRate` and `SpeedMultiplier`; assert the previous value and the accumulated time are both retained
  - Property 8: arbitrary duration sequences with `IsPaused = true`; assert zero ticks throughout, unchanged accumulated value, and an identical alpha on every call
  - Property 9: paused accumulator at an arbitrary accumulated value; assert `RequestSingleStep()` returns 1, `IsPaused` stays true, accumulated value unchanged
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.8, 4.1, 4.2, 4.3, 4.4, 4.5, 5.3, 5.9_
  - _Design: Correctness Properties 4-9, Testing Strategy_

## 4. Bootstrapper and driver

The session now ticks inside Unity but nothing renders from it; the legacy controllers still drive the visuals. Two simulations are deliberately alive in this group only, which is what proves the session ticks before anything is deleted.

- [ ] 20. Implement `SiteKind` and `SiteMarker`
  - Create `Assets/Scripts/Authoring/SiteKind.cs` (`{ Refuel, Dump }`, presentation-only, no port or core counterpart to duplicate) and `Assets/Scripts/Authoring/SiteMarker.cs`
  - `SiteMarker` is a `MonoBehaviour` with the serialized fields from the `SiteMarker` field table: `kind`, `useExplicitCell`, `explicitCell` (`Vector2Int`)
  - `TryResolveCell(CoordinateMapper, out PortGridPosition)` resolves from the transform's world position through `TryToGrid` by default, or returns the authored explicit cell
  - Also add the presentation-only `WorldSource { Generated, AuthoredText }` enum
  - _Requirements: 10.1, 10.2, 10.3_
  - _Design: Decision H, Serialized Field Surface_

- [ ] 21. Implement `AgentBinding` and `AgentBindingRegistry`
  - Create `Assets/Scripts/Simulation/AgentBinding.cs` and `Assets/Scripts/Simulation/AgentBindingRegistry.cs`
  - `AgentBinding` holds `AgentId`, the `AgentView`, `PreviousSnapshot`/`CurrentSnapshot` (`PortAgentSnapshot`, internal setters), and `PreviousPosition`/`CurrentPosition` convenience properties reading from those snapshots
  - `AgentBindingRegistry` exposes `Bindings` in ordinal-id order, `TryGet`, `Add`, and `ApplyUpdate(WorldUpdate)` which shifts each binding's `CurrentSnapshot` into `PreviousSnapshot` and installs the matching entry of `update.Agents` as the new `CurrentSnapshot`
  - `PreviousSnapshot`/`CurrentSnapshot` are both initialised to the matching entry of `WorldSnapshot.Agents` at bind time, so no null or sentinel case exists on the first frame
  - The `AgentView` reference is typed loosely or the field left unassigned until group 6 introduces `AgentView`; do not forward-reference a type that does not exist yet
  - _Requirements: 5.1, 5.6, 9.3_
  - _Design: Decision D (previous-position snapshot, now port-mediated), Decision F_

- [ ] 22. Implement `SimulationDriver`
  - Create `Assets/Scripts/Simulation/SimulationDriver.cs`
  - `MonoBehaviour` with `[DisallowMultipleComponent]`, the public surface from Components and Interfaces, and the serialized fields from the `SimulationDriver` field table: `tickRate`, `tickBudget`, `speedMultiplier`, `startPaused`, `pauseKey`, `stepKey`
  - Owns the single `ISimulationSession`; nothing else in the project holds a reference to it, and nothing in this class ever names `SimulationWorld`. `Initialize` enables the component, which is authored disabled, and subscribes to `session.UpdateReceived`
  - Implement `Update()` and `OnUpdateReceived(WorldUpdate)` exactly per the Decision D pseudocode: return early on a null session; `Advance` with `Time.unscaledDeltaTime` and the cached `_lastHalted`; call `Session.RequestTick()` in a loop up to the planned count, breaking mid-loop when `_lastHalted` turns true; assign alpha **after** the loop; render views last in the same `Update`. `OnUpdateReceived` calls `Bindings.ApplyUpdate(update)`, `GridView.OnUpdateReceived(update)`, and refreshes `DischargedTotal`/`_lastHalted`
  - `OnValidate` pushes the three tunables through the `TickAccumulator` setters so non-positive values are rejected and the previous value retained
  - _Requirements: 2.4, 2.6, 2.7, 3.1, 3.2, 3.7, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.3_
  - _Design: Decision D_

- [ ] 23. Implement `WorldBootstrapper`'s request-building and connect handshake
  - Create `Assets/Scripts/Authoring/WorldBootstrapper.cs`
  - `MonoBehaviour` with `[DefaultExecutionOrder(-1000)]` and `[DisallowMultipleComponent]`, plus every serialized field in the `WorldBootstrapper` field table with the exact `[Range]`, `[Min]`, `[Header]` and `[Tooltip]` attributes listed there, including `[SerializeReference] private ISimulationConnector connector`
  - No `FindObjectOfType` or `FindObjectsOfType` anywhere; all references are authored `[SerializeField]`/`[SerializeReference]` links
  - Implement `Awake()` as steps 1-4 of Decision G': resolve and validate `SiteMarker`s (Req 10.3, 10.4); sort `agentViews` by `string.CompareOrdinal` and pre-validate ids (Req 9.1, 9.4); build a `SessionRequest`, widening authored `float` tunables to `double` at that single construction point; call `connector.Connect(request)` and build `CoordinateMapper` from `gridOrigin`/`tileSize`/`gridWidth`/`gridHeight`
  - _Requirements: 9.1, 9.2, 9.4, 10.1, 10.2, 10.3, 10.4, 11.1_
  - _Design: Decision G' (steps 1-4), Serialized Field Surface_

- [ ] 24. Implement `WorldBootstrapper`'s polling loop and completion
  - Implement `Update()` as steps 5-7 of Decision G': call `connection.Poll()` every frame until `IsComplete`; on `connection.Failed`, hard-fail using `connection.Error` verbatim; relay any `connection.Warnings` as soft warnings; on success, read `connection.Session.InitialSnapshot`, call `simulationDriver.Initialize(session, mapper, bindings, gridView)`, and call `gridView.Initialize(snapshot, mapper)`
  - `InitializationFailed`, `Session`, and `Mapper` are the public surface from Components and Interfaces
  - _Requirements: 7.1, 9.5, 10.6, 11.5, 13.2_
  - _Design: Decision G' (steps 5-7)_

- [ ] 25. Implement the hard-fail and soft-warning paths owned by `WorldBootstrapper`
  - Implement every `WorldBootstrapper`-attributed row of the Error Handling hard-failure table with the exact message shapes given: log error, set `InitializationFailed = true`, leave `Session` null, leave the driver and grid view disabled, and `return` without throwing
  - Implement the soft-warning rows owned by `WorldBootstrapper`: unmatched `AgentView` id, non-positive inspector value
  - Gate every warning that can fire per frame or per cell behind a `HashSet<string>` of already-logged keys
  - _Requirements: 9.4, 9.6, 10.3, 10.4, 13.2_
  - _Design: Error Handling_

## 5. Grid view replaces GridManager

The largest group. `TileData` and `GridManager` are mutually dependent and every legacy controller reaches into both, so the replacement and the deletions land together to keep Requirement 13.4.

- [ ] 26. Implement `CellVisual` and `CellVisualMap`
  - Create `Assets/Scripts/Views/CellVisual.cs` and `Assets/Scripts/Views/CellVisualMap.cs` in namespace `AgroAgents.Presentation.Views`
  - `CellVisualMap` is a `ScriptableObject` with `[CreateAssetMenu]`, the serialized fields from its field table, `TryGet(PortCellState, out CellVisual)` and a `Fallback`
  - Populate the four-entry default from the `PortCellState` → visual mapping table: `Empty`, `Crop`, `Blocked`, `Harvested`, each with a distinct floor material slot
  - `TileState.Deteriorado` has no entry and is not remapped; the concept is dropped
  - _Requirements: 7.6, 7.8, 12.2_
  - _Design: Decision E, Data Models_

- [ ] 27. Implement `GridView`
  - Create `Assets/Scripts/Views/GridView.cs`
  - `MonoBehaviour` with `[DisallowMultipleComponent]`, the public surface from Components and Interfaces, and the serialized fields from the `GridView` field table. `width`, `height`, `useRandomSeed`, `customSeed`, `obstacleChance` and `cropChance` are absent by requirement; dimensions come from `WorldSnapshot.Width`/`Height`
  - `Initialize(WorldSnapshot, CoordinateMapper)` instantiates one floor prefab per cell in `snapshot.Cells` positioned through the mapper, applies the mapped floor material, instantiates content prefabs for `Crop` and `Blocked`, and renders the refuel and dump marker prefabs at every `RefuelStations` and `DumpSites` position
  - `OnUpdateReceived(WorldUpdate)` applies `update.ChangedCells` directly — no polling, no local shadow array; the diff was already computed inside the adapter (Decision E)
  - Content variant selection is `flatIndex % variants.Length`, never `UnityEngine.Random`
  - A `Crop` → `Harvested` transition destroys the crop content instance
  - _Requirements: 2.1, 2.3, 2.5, 7.1, 7.2, 7.3, 7.4, 7.5, 7.7, 7.8, 10.6, 12.3_
  - _Design: Decision E_

- [ ] 28. Delete `GridManager` and `TileData`, and strip their call sites
  - Delete `Assets/Scripts/GridScripts/GridManager.cs` and `Assets/Scripts/GridScripts/TileData.cs`, which removes the `TileState` and `TileContent` enums with them
  - Modify `Assets/Scripts/CameraScripts/IsometricView.cs`: swap the `gridManager` field for `WorldBootstrapper`/`CoordinateMapper` and read `Width`, `Height`, `TileSize` and `GridCentreWorld` from the mapper. Otherwise unchanged
  - Modify `AgentController.cs`, `HarvesterController.cs`, `TractorController.cs` and `FieldManager.cs`: remove every `GridManager` and `TileData` call site, which reduces them to inert shells holding only their serialized fields. They are deleted wholesale in group 6
  - _Requirements: 12.2, 12.3, 12.9, 13.1, 13.4_
  - _Design: Migration Plan Step 5_
  - Editor authoring pending before play mode is clean: replace the `GridManager` component in `SimulationScene.unity` with `GridView` and `WorldBootstrapper`, and assign the floor prefab, `CellVisualMap` asset and its four materials

## 6. Agent views replace the controllers

- [ ] 29. Implement `StateVisual` and `StateVisualMap`
  - Create `Assets/Scripts/Views/StateVisual.cs` and `Assets/Scripts/Views/StateVisualMap.cs`
  - `StateVisualMap` is a `ScriptableObject` with `[CreateAssetMenu]`, the serialized fields from its field table, `TryGet(PortStateId, out StateVisual)`, `Fallback`, and `MissingStates()` for editor validation
  - Populate all eight entries from the `PortStateId` → visual mapping table, including `Inactive`
  - A missing entry applies the magenta `Fallback` and logs one warning per state per session
  - No presentation enum duplicates `PortStateId`
  - _Requirements: 8.1, 8.5, 8.6_
  - _Design: Data Models_

- [ ] 30. Implement `AgentView`
  - Create `Assets/Scripts/Views/AgentView.cs`
  - `MonoBehaviour` with `[DisallowMultipleComponent]`, the public surface from Components and Interfaces, and every serialized field in the `AgentView` field table. `moveSpeed` and `arrivalTolerance` from the deleted `AgentController` are gone; `forwardOffsetY` is preserved
  - `startCell` is a `Vector2Int` surrogate because `PortGridPosition` is a readonly struct Unity cannot serialize
  - `Render(float alpha, float deltaTime)` writes only `transform.position`, `transform.rotation`, renderer material or colour, and label text, reading only the bound `AgentBinding`'s `PreviousSnapshot`/`CurrentSnapshot`. It calls no port method
  - Position is `Vector3.Lerp(ToWorld(PreviousPosition), ToWorld(CurrentPosition), alpha)` plus `heightOffset`. Alpha `0` renders at the previous cell, alpha `1` at the current cell, and an unchanged position renders constant for every alpha
  - Rotation uses the tick-to-tick direction, not the frame-to-frame delta, so the target yaw is constant across the interval. Preserve the `Quaternion.LookRotation(dir) * Quaternion.Euler(0f, forwardOffsetY, 0f)` and `RotateTowards` form from the deleted `AgentController`, and leave rotation alone when the direction is near zero
  - `CurrentState == Inactive` ignores the alpha and renders at the current grid position
  - Display `Fuel`, `Load` and `MaxLoad` read from the bound `PortAgentSnapshot`. `MarkUnbound()` logs one warning naming the id and renders nothing thereafter
  - Diagonal steps are rendered at their true `sqrt(2)` distance; the speed-up is accepted, not normalised
  - _Requirements: 2.2, 2.3, 2.5, 5.2, 5.4, 5.5, 5.7, 5.8, 5.9, 8.2, 8.3, 8.4, 9.6_
  - _Design: Decision F_

- [ ] 31. Wire `AgentView` into the binding registry, driver, and bootstrapper
  - Modify `Assets/Scripts/Simulation/AgentBinding.cs` and `AgentBindingRegistry.cs` to hold the concrete `AgentView` reference
  - Modify `WorldBootstrapper`'s completion step (task 24) to find each `AgentView`'s matching entry in `snapshot.Agents` by id, call `Bind` on each binding with both snapshots set to that entry, and call `MarkUnbound()` on any `AgentView` whose id matched nothing
  - Modify `SimulationDriver.OnUpdateReceived` (task 22) to call `Render(alpha, dt)` on every bound view after `ApplyUpdate`
  - _Requirements: 5.2, 9.3, 9.5, 9.6_
  - _Design: Decision G' (step 7), Decision D_

- [ ] 32. Delete the legacy agent scripts
  - Delete `Assets/Scripts/AgentsScripts/AgentController.cs`, which removes the Unity `AgentState` enum with it
  - Delete `Assets/Scripts/AgentsScripts/HarvesterController.cs`, `TractorController.cs` and `FieldManager.cs`
  - All Unity-side fuel accounting, refuel thresholds, load accounting, harvest decisions, tractor pairing, meeting-point negotiation, transfer resolution, discharge counting and nearest-target searches disappear with these four files
  - Group 5 already emptied their call sites, so nothing references the deleted types
  - _Requirements: 12.4, 12.5, 12.6, 12.7, 12.8, 13.1, 13.4, 13.6_
  - _Design: Migration Plan Step 6_
  - Editor authoring pending before play mode is clean: swap the controller components on the harvester and tractor prefabs for `AgentView`, and assign the `StateVisualMap` asset

## 7. Delete the Unity pathfinder

- [ ] 33. Delete `Assets/Scripts/GridScripts/GridPathFinder.cs`
  - The file is `GridPathFinder.cs`; the type inside is `public static class GridPathfinder` with a lowercase `f`
  - It is a four-directional BFS over `TileData`, already broken by group 5's deletion of `TileData`, and removable now that its last caller `AgentController.SetPathTo` is gone
  - Zero references remain; all pathfinding is `HarvestingCore.Pathfinding.PathFinder`, reachable only from `AgroAgents.InMemoryAdapter`
  - _Requirements: 12.1, 13.1_
  - _Design: Migration Plan Step 7_

## 8. Unity test assemblies

- [ ] 34. Add CsCheck and the two Unity test assemblies
  - Add `Assets/Plugins/CsCheck/CsCheck.dll`
  - Create `Assets/Tests/EditMode/AgroAgents.Tests.EditMode.asmdef` and `Assets/Tests/PlayMode/AgroAgents.Tests.PlayMode.asmdef`, both referencing `AgroAgents.Presentation`, `AgroAgents.SimulationPort`, and `AgroAgents.InMemoryAdapter` — **not** `HarvestingCore` directly — both with `"defineConstraints": ["UNITY_INCLUDE_TESTS"]` and `overrideReferences: true` taking `precompiledReferences` on `nunit.framework.dll` and `CsCheck.dll`
  - The define constraint keeps the PBT dependency out of player builds. Where a test needs to seed or inspect simulation state precisely, it does so through `AgroAgents.InMemoryAdapter`'s test-only accessors rather than referencing `HarvestingCore`
  - _Requirements: 1.4_
  - _Design: Decision B, Decision B1, Testing Strategy_

- [ ] 35. Write the `CoordinateMapper` property tests in EditMode
  - Create `Assets/Tests/EditMode/CoordinateMapperProperties.cs`, 1000 iterations each
  - Generators: `tileSize` in `[0.01, 100]`, origin components in `[-1000, 1000]`, `width`/`height` in `[1, 64]`, positions both inside and outside bounds
  - Property 1: `TryToGrid(ToWorld(p))` succeeds and returns `p` for every in-bounds position
  - Property 2: for any world position whose nearest cell centre is in bounds, `ToWorld(TryToGrid(w))` differs from `w` by at most half a tile on each of x and z
  - Property 3: for any world position whose nearest cell is out of bounds, `TryToGrid` returns `false` and writes no usable position
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
  - _Design: Correctness Properties 1-3_

- [ ] 36. Write the visual-map and grid-projection property tests in EditMode
  - Create `Assets/Tests/EditMode/VisualMapProperties.cs` and `GridViewProjectionProperties.cs`
  - Property 16: all eight `PortStateId`s crossed with maps missing random subsets; assert the applied visual is never null and that a warning is emitted exactly when the entry is absent
  - Property 15: random `WorldModel` plus random `Harvest`/`Plant` sequences translated into a `WorldSnapshot` + sequence of `WorldUpdate`s through `AgroAgents.InMemoryAdapter`; assert every cell's rendered floor material equals the material the `CellVisualMap` assigns to its current `PortCellState`, and that a content prefab instance exists at a cell if and only if that state has a configured content prefab
  - _Requirements: 7.2, 7.4, 7.5, 7.6, 7.8, 8.1, 8.5_
  - _Design: Correctness Properties 15, 16_

- [ ] 37. Write the site-marker property test in EditMode
  - Create `Assets/Tests/EditMode/SiteMarkerProperties.cs`
  - Property 20: random marker sets over random grids with injected out-of-bounds markers and same-kind collisions; assert bootstrap succeeds iff every marker resolves in bounds and no two same-kind markers share a cell, that `WorldSnapshot.RefuelStations` and `DumpSites` come out in row-major order regardless of authoring order, and that every station and dump cell holds `PortCellState.Empty` after generation
  - _Requirements: 10.1, 10.2, 10.3, 10.4_
  - _Design: Correctness Property 20, Decision H_

- [ ] 38. Write the render-purity and interpolation-endpoint property tests in PlayMode
  - Create `Assets/Tests/PlayMode/RenderPurityProperties.cs` and `InterpolationProperties.cs` with a scene fixture that bootstraps a small world through `InMemorySimulationConnector`
  - Property 13: random session state and random alpha sequence; assert repeated `Render`/`OnUpdateReceived` calls with the same input produce the same output and that no additional `RequestTick()` is observed as a side effect of rendering
  - Property 14: adjacent `PortGridPosition` pairs drawn from the port's mirrored move offsets plus the equal pair, alpha in `[0, 1]`; assert alpha `0` places the transform at `ToWorld(previous)`, alpha `1` at `ToWorld(current)` within `1e-4`, and that an equal pair renders at `ToWorld(current)` for every alpha
  - _Requirements: 2.5, 5.2, 5.4, 5.5, 5.7, 5.8, 7.7_
  - _Design: Correctness Properties 13, 14_

- [ ] 39. Write the assembly-boundary and legacy-absence reflection tests in EditMode
  - Create `Assets/Tests/EditMode/AssemblyBoundaryTests.cs`
  - Assert `typeof(SimulationWorld).Assembly.GetName().Name == "HarvestingCore"` and that its referenced-assembly list contains no name starting with `Unity` (Req 1.2, 1.5)
  - Assert no type in that assembly is assignable to `UnityEngine.Object` (Req 1.6)
  - Assert `typeof(SimulationDriver).Assembly.GetName().Name == "AgroAgents.Presentation"` and that its referenced-assembly list contains no assembly named `HarvestingCore` (Req 1.8, 2.8) — proving the port boundary the same way the engine boundary is proven
  - Assert `AgroAgents.SimulationPort`'s referenced-assembly list contains no `HarvestingCore` and no `Unity`-prefixed name (Req 1.10)
  - Assert no type named `TileData`, `TileState`, `TileContent`, `AgentState`, `GridPathfinder` or `GridManager` exists in the `AgroAgents.Presentation` assembly
  - The project compiling at all is the proof of Requirements 1.1, 1.4, 1.7, and 1.9; Requirement 1.3 was verified manually in task 7
  - _Requirements: 1.2, 1.5, 1.6, 1.8, 1.9, 1.10, 2.8, 12.1, 12.2, 12.3, 12.4, 12.9_
  - _Design: Testing Strategy_

- [ ] 40. Write the example and edge-case unit tests
  - Group these across the EditMode and PlayMode assemblies by what each needs, rather than one file per assertion
  - Alpha exactly `0` and exactly `1` render at the previous and current cell (named instances of Property 14)
  - A single `Crop` → `Harvested` transition destroys exactly the crop content instance (named instance of Property 15 and Requirement 7.5)
  - Halted `WorldUpdate` stops the tick loop and agents render at their current cell
  - A diagonal step renders at `sqrt(2)` of an orthogonal step, guarding the accepted Decision F behaviour against silent regression
  - `PathInvalidatedThisTick` with an unchanged position renders constant
  - Empty agent list: bootstrap succeeds, halted stays false, nothing renders
  - A 1×1 grid with a station on the only cell
  - `worldSource == AuthoredText` with a `Blocked` character on a station cell emits the soft warning
  - A missing serialized reference — including `connector` — produces one error and a disabled driver
  - `ISimulationConnection.Poll()` called after `IsComplete` is already true is a no-op
  - _Requirements: 3.7, 5.4, 5.5, 5.7, 7.5, 8.3, 10.1, 11.7, 13.2_
  - _Design: Testing Strategy, Decision F_

## Deferred authoring work

Outside the coding scope, recorded so it is not silently dropped. These are the deferrals the design states in its Overview and traceability table.

- [ ] Re-wire `SimulationScene.unity` after groups 5 and 6: replace `GridManager` with `GridView` plus `WorldBootstrapper`, and the legacy controllers with `AgentView` (Requirement 13.5)
- [ ] Author a `WorldBootstrapper` scene instance with `connector` assigned to an `InMemorySimulationConnector` — the `[SerializeReference]` field has no default and must be set per scene (Requirement 13.5)
- [ ] Swap the controller components on the harvester and tractor prefabs for `AgentView`, preserving existing prefab assignments (Requirement 13.5)
- [ ] Author the `CellVisualMap` asset: four distinct floor materials and the crop and obstacle content prefabs, reusing the existing material and prefab assets. `deterioradoMaterial` is left unassigned since `Deteriorado` is dropped (Requirement 7.3)
- [ ] Author the `StateVisualMap` asset: eight entries plus the fallback material (Requirement 8.1)
- [ ] Confirm the camera configuration still frames the field after `IsometricView` switches to the `CoordinateMapper` (Requirement 13.5)
- [ ] Confirm on first import that Unity resolves a `file:` package path pointing outside the project root. Documented behaviour, not exercised in this project
- [ ] Confirm the `obj/`-generated `AssemblyInfo.cs` duplicate-attribute concern is moot once the output redirect from task 2 is in place
- [ ] Confirm `[SerializeReference]` correctly discovers `InMemorySimulationConnector` across the `AgroAgents.InMemoryAdapter` assembly boundary in the installed Unity version. Documented Unity behaviour since 2019.3, not exercised in this project
