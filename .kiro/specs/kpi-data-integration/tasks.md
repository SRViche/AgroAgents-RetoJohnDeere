# Implementation Plan: KPI Data Integration

## Overview

Incremental, test-first delivery of the `Fuel_Consumed_Total` wire field and the client-side
`KpiProvider`. Layers are built bottom-up so each is testable before the next depends on it:
core counter → wire DTO + host producer → port types → adapters → `KpiProvider`. Property tests
are placed next to the implementation they validate and are all required. All 18 correctness
properties and all 8 requirements from the design are covered.

Testing conventions (from the design Testing Strategy, apply to every property-test task below):
- Use the existing PBT library integrated with xUnit (FsCheck; CsCheck acceptable), never hand-rolled.
  On the presentation/Unity side, use the same library from the plain .NET test project that
  references the presentation assembly, mirroring the existing WebSocket adapter test pattern.
- Configure each property test for a minimum of 100 iterations.
- Tag each property test with a comment: `Feature: kpi-data-integration, Property {number}: {property_text}`.
- Exactly one property-based test per correctness property (18 total).

## Tasks

- [x] 1. Core: track cumulative fleet fuel consumption
  - [x] 1.1 Add the counter and sink to `SimulationWorld`
    - Add `public int FuelConsumedTotal { get; private set; }` initialised to `0` in the constructor alongside `DischargedTotal`
    - Add `internal void AddFuelConsumed(int amount)` mirroring `AddDischarged`
    - In `Tick()`, pass `AddFuelConsumed` into the `AgentContext` constructor alongside `AddDischarged`
    - _Requirements: 1.1, 1.2, 1.4_

  - [x] 1.2 Add the parallel fuel-consumed sink to `AgentContext`
    - Add a second `Action<int>` field set from a new constructor parameter
    - Expose `public void AddFuelConsumed(int amount)` mirroring `AddDischarged`
    - _Requirements: 1.3_

  - [x] 1.3 Accumulate actual clamped burn in `Agent.Move`
    - Capture `fuelBefore`, call `SetFuel(Fuel - FuelConsumption)`, compute `burned = fuelBefore - Fuel`, and call `context.AddFuelConsumed(burned)` when `burned > 0`
    - Leave `Refuel()` untouched so it never touches the sink
    - _Requirements: 1.3, 1.5_

  - [x] 1.4 Write property test: counter equals actual fleet burn
    - Generator: random worlds/configs advanced by a random number of ticks
    - **Property 1: Fuel-consumed counter equals actual fleet burn** (`FuelConsumedTotal` == sum over agents of each step's `fuelBefore - fuelAfter`, excluding refuel)
    - **Validates: Requirements 1.1, 1.3**

  - [x] 1.5 Write property test: counter is monotonically non-decreasing
    - Generator: random worlds advanced over a sequence of ticks
    - **Property 2: Fuel-consumed counter is monotonically non-decreasing** (value after each tick >= value before)
    - **Validates: Requirements 1.4**

  - [x] 1.6 Write property test: refuel does not change the counter
    - Generator: random agent positioned on a refuel station
    - **Property 3: Refuel does not change the fuel-consumed counter**
    - **Validates: Requirements 1.5**

  - [x] 1.7 Write unit test: counter is zero before the first tick
    - Assert `FuelConsumedTotal == 0` on a freshly constructed world
    - _Requirements: 1.2_

- [ ] 2. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Wire + host: surface the counter in the snapshot
  - [x] 3.1 Add `FuelConsumedTotal` to `SimulationSnapshot`
    - Add `[JsonPropertyName("fuelConsumedTotal")] public int FuelConsumedTotal { get; set; }` alongside `DischargedTotal`
    - _Requirements: 2.3, 8.1_

  - [x] 3.2 Populate it in `SimulationHostAdapter.GetSnapshot()`
    - Set `FuelConsumedTotal = world.FuelConsumedTotal` next to `DischargedTotal = world.DischargedTotal`
    - _Requirements: 1.6, 2.3_

  - [x] 3.3 Write property test: fuel-consumed total survives the wire round-trip
    - Generators: random integer `FuelConsumedTotal` in a `SimulationSnapshot`; random world states for `GetSnapshot()`
    - Serialize then deserialize via `SnapshotSerializer` and assert the value is preserved; assert `GetSnapshot().FuelConsumedTotal == world.FuelConsumedTotal` (SnapshotSerializer needs no code change, only assertions)
    - **Property 4: Fuel-consumed total survives the wire round-trip**
    - **Validates: Requirements 1.6, 1.7, 2.3, 8.1, 8.2, 8.3**

- [x] 4. Port + adapters: expose the counter per tick
  - [x] 4.1 Add `FuelConsumedTotal` to `WorldSnapshot` and `WorldUpdate`
    - Add `public int FuelConsumedTotal { get; }` plus a constructor parameter next to `DischargedTotal` in both types
    - _Requirements: 1.7, 8.2_

  - [x] 4.2 Pass it through `InMemorySimulationSession`
    - Feed `_world.FuelConsumedTotal` into both the `WorldSnapshot` built in `BuildSnapshot()` and the `WorldUpdate` raised in `RequestTick()`
    - _Requirements: 8.3_

  - [x] 4.3 Carry it through the WebSocket adapter
    - Add `[JsonPropertyName("fuelConsumedTotal")] public int FuelConsumedTotal { get; set; }` to `WsSimulationSnapshot` in `ServerMessage.cs` (case-insensitive parse)
    - In `WebSocketSimulationSession`, carry `FuelConsumedTotal` through when mapping `WsSimulationSnapshot` into `WorldSnapshot`/`WorldUpdate`
    - _Requirements: 8.2, 8.3_

  - [x] 4.4 Write integration test: value flow through the in-memory adapter
    - Drive `SimulationWorld` → `RequestTick()` → `WorldUpdate` and assert `WorldUpdate.FuelConsumedTotal` equals the core's `FuelConsumedTotal`
    - _Requirements: 8.3, 8.4_

  - [x] 4.5 Write integration test: value flow through the WebSocket adapter
    - Feed a `WsSimulationSnapshot` JSON frame through the parser/session and assert the resulting `WorldUpdate.FuelConsumedTotal` equals the value in the frame
    - _Requirements: 8.3, 8.4_

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. KpiProvider: construction and state classification
  - [x] 6.1 Create `KpiProvider` skeleton and internal `AgentAccumulator`
    - New component in `AgroAgents.Presentation`, namespace `AgroAgents.Presentation.Kpi`
    - Constructor takes `ISimulationSession`; seed `_width`/`_height`, full-grid cell cache, and workable-cell baseline (Crop-class at start) from `InitialSnapshot`; fix `WorkableCellCount` at that size; set `_lastProcessedTick = InitialSnapshot.TickIndex`; subscribe to `UpdateReceived`
    - `AgentAccumulator` holds `Role`, `ProductiveTicks`, `IdleTicks`, `InactiveTicks`, `OutOfFuelEventCount`, `PreviousCategory`
    - _Requirements: 5.5_

  - [x] 6.2 Implement state-category classification helper
    - Map `PortStateId` to `KpiStateCategory` (Productive: Harvest, GoToDump; Idle: Idle, GoToRefuel, GoToMeetingPoint, WaitTractor, WaitHarvester; Inactive: Inactive)
    - _Requirements: 3.3_

  - [x] 6.3 Write property test: classification is total and single-valued
    - Generator: all `PortStateId` values
    - **Property 8: State classification is a total, single-valued function** (exactly one category per state; buckets mutually exclusive and jointly exhaustive)
    - **Validates: Requirements 3.3**

  - [x] 6.4 Write property test: workable-cell count fixed at start
    - Generator: random initial snapshots advanced by random ticks
    - **Property 15: Workable-cell count is fixed at simulation start** (equals Crop-class count at initial snapshot, unchanged across ticks)
    - **Validates: Requirements 5.5**

- [x] 7. KpiProvider: tick-continuity guard
  - [x] 7.1 Implement the continuity guard in `OnUpdateReceived`
    - Discard updates with `TickIndex <= _lastProcessedTick` (no accumulator mutation, no sample, no emit); record a gap when `TickIndex > _lastProcessedTick + 1`; otherwise process normally; set `_lastProcessedTick` after a processed update
    - _Requirements: 7.2, 7.3_

- [x] 8. KpiProvider: field coverage
  - [x] 8.1 Patch the cell cache and compute `FieldCoverageKpi`
    - Apply `update.ChangedCells` to the full-grid cache; compute `Width`/`Height`, `HarvestedCellCount` over the workable baseline, `CoveragePercent` with a zero-guard, and one `FieldCoverageCell` per workable position
    - _Requirements: 5.4, 5.6, 5.7, 5.8, 5.9_
- [ ] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. KpiProvider: per-agent accumulation
  - [ ] 10.1 Accumulate per-agent category counts, retention, and out-of-fuel events
    - For each agent in the update: classify current state, increment its category count by exactly one, and increment `OutOfFuelEventCount` when its category moved from non-Inactive to Inactive; retain accumulators for agents absent from the update; keep one accumulator per observed agent with id + role
    - _Requirements: 3.4, 3.5, 3.6, 3.7, 4.2, 4.3, 4.4, 4.5, 7.4_

- [ ] 11. KpiProvider: throughput history and fuel-per-ton
  - [ ] 11.1 Append time samples and compute `FuelPerTon`
    - Append `KpiTimeSample(update.TickIndex, update.DischargedTotal, update.FuelConsumedTotal)` to the ordered `ThroughputHistory`; compute `FuelPerTon` = latest `FuelConsumedTotal / DischargedTotal` when discharged > 0, else `0`
    - _Requirements: 1.8, 2.4, 2.5, 2.6, 2.7, 8.4_

  - [ ] 11.2 Write property test: a time sample mirrors its source update
    - Generator: ordered tick streams
    - **Property 5: A time sample mirrors its source update** (`Tick`/`DischargedTotal`/`FuelConsumedTotal` all taken from the single update)
    - **Validates: Requirements 1.8, 2.4, 8.4**

  - [ ] 11.3 Write property test: throughput history ordered by ascending tick
    - **Property 6: Throughput history is ordered by ascending tick**
    - **Validates: Requirements 2.5**

  - [ ] 11.4 Write property test: FuelPerTon ratio with zero guard
    - **Property 7: FuelPerTon is the fuel/discharged ratio with a zero guard** (ratio when discharged > 0, else zero)
    - **Validates: Requirements 2.6, 2.7**

  - [ ] 11.5 Write unit test: FuelPerTon with zero discharged returns zero
    - _Requirements: 2.7_

- [ ] 12. KpiProvider: emit the complete view model
  - [ ] 12.1 Build and emit one `KpiViewModel` per processed tick
    - Populate all six members (`Tick`, `AgentUtilization`, `ThroughputHistory`, `FuelPerTon`, `FieldCoverage`, `OutOfFuelEvents`) as read-only; emit exactly once per processed update; surface the latest instance for consumers
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ] 12.2 Write property test: exactly one well-formed view model per processed tick
    - Generator: ordered tick streams
    - **Property 16: Exactly one well-formed view model per processed tick** (`Tick` == `TickIndex`; all six members populated with non-null collections)
    - **Validates: Requirements 6.1, 6.2, 6.3**

  - [ ] 12.3 Write unit test: all six members populated for a processed update
    - _Requirements: 6.3_

- [ ] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Every property-based test task is required (not marked optional) so all 18 correctness properties
  from the design are covered by exactly one required test each.
- Each task references specific requirement clauses and, for property tests, the design property number.
- Checkpoints validate each layer before the next depends on it.
- Requirement coverage: R1 (1.1–1.7, 2, 3.1, 3.3, 4.1, 4.2/4.3), R2 (3.1, 3.3, 11), R3 (6, 10),
  R4 (10), R5 (6.1, 6.4, 8), R6 (12), R7 (7, 10.1), R8 (3, 4, 11) — all 8 requirements covered.
