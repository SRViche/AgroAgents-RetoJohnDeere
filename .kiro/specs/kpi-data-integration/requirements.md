# Requirements Document

## Introduction

This feature integrates the AgenticModel's per-tick simulation data into the Unity-side KPI
view model (`KpiViewModel`) so that future view components can render the five dashboard KPIs.
The scope is strictly the **data contract**: what data must flow, per tick, from the AgenticModel
simulation core, across the wire (the `SimulationSnapshot` DTO), through the Unity Port layer
(`WorldSnapshot` / `WorldUpdate`), and finally into every field of `KpiViewModel` and its
subordinate types (`AgentUtilizationKpi`, `KpiTimeSample`, `FieldCoverageKpi` /
`FieldCoverageCell`, `OutOfFuelEventsKpi`).

A prior gap analysis established that four of the five KPIs can be populated from data that
already exists in the per-tick snapshot, while KPI #3 (fuel efficiency) cannot: the snapshot only
carries each agent's **remaining** fuel, from which cumulative fleet fuel consumption cannot be
reconstructed once refuelling has reset a tank. This document expresses the technical requirements
for closing that gap and for populating the remaining KPI fields. Open design choices — most
notably whether accumulated counts (utilization ticks, out-of-fuel events) are computed on the
server or on the client, and how the fuel-consumed counter is wired through the core — are
deliberately left to the design stage. Requirements below state **what data must be available and
with what properties**, not **where the computation lives**.

## Glossary

- **Simulation_Core**: The `HarvestingCore` library, specifically `SimulationWorld` and `Agent`,
  which owns authoritative simulation state and advances it one tick at a time.
- **Snapshot_Producer**: The host component (`SimulationHostAdapter.GetSnapshot`) that builds a
  `SimulationSnapshot` from `SimulationWorld` state each tick.
- **Simulation_Snapshot**: The wire DTO `HarvestingCore.Transport.Dto.SimulationSnapshot` (plus its
  `AgentSnapshot` and `CellSnapshot` members) transmitted from the AgenticModel to the Unity client
  once per tick.
- **Port_Layer**: The Unity-side mirror types in `AgroAgents.SimulationPort` — `WorldSnapshot`,
  `WorldUpdate`, `PortAgentSnapshot`, `PortCellSnapshot` — that expose simulation data to the
  presentation layer.
- **KPI_Provider**: The component (to be defined in design) responsible for producing a
  `KpiViewModel` instance from Port_Layer data on each tick.
- **KPI_View_Model**: The immutable per-tick struct `AgroAgents.Presentation.Kpi.KpiViewModel` and
  its subordinate types, which is the target data contract of this feature.
- **Tick**: The simulation's discrete clock unit. `SimulationWorld.TickIndex` is the authoritative
  tick counter.
- **Remaining_Fuel**: An agent's current fuel level in its tank (`Agent.Fuel`), which is reset to
  `MaxFuel` on refuel and therefore is **not** a measure of consumption.
- **Fuel_Consumed_Total**: A cumulative, monotonically non-decreasing, fleet-wide count of fuel
  units burned by all agents since simulation start, analogous to `DischargedTotal`.
- **Discharged_Total**: The cumulative, fleet-wide count of product units discharged at dump sites
  since simulation start (`SimulationWorld.DischargedTotal`).
- **State_Category**: One of Productive, Idle, or Inactive, the bucket into which an agent's FSM
  state is classified for KPI #1, per the mapping in `KpiStateCategory`.
- **Out_Of_Fuel_Event**: A transition of an agent from any non-Inactive state into the Inactive
  state.
- **Workable_Cell**: A grid cell that was harvestable crop at simulation start; the denominator set
  for field-coverage percentage.

## Requirements

### Requirement 1: Cumulative fleet fuel consumption is tracked and surfaced

**User Story:** As a KPI dashboard developer, I want a cumulative fleet-wide fuel-consumed total
available per tick, so that I can populate `KpiTimeSample.FuelConsumedTotal` and compute KPI #3
(fuel consumed per ton harvested) without attempting to reconstruct it from remaining fuel.

#### Acceptance Criteria

1. THE Simulation_Core SHALL maintain a Fuel_Consumed_Total counter that increases by the amount of
   fuel deducted from any agent whenever that agent consumes fuel during a Tick.
2. THE Simulation_Core SHALL initialize the Fuel_Consumed_Total counter to zero before the first
   Tick.
3. WHEN an agent's fuel is deducted during a Tick, THE Simulation_Core SHALL increase the
   Fuel_Consumed_Total counter by the number of fuel units deducted.
4. WHILE the simulation advances across successive Ticks, THE Simulation_Core SHALL keep the
   Fuel_Consumed_Total counter monotonically non-decreasing.
5. WHEN an agent is refuelled, THE Simulation_Core SHALL leave the Fuel_Consumed_Total counter
   unchanged by the refuel action.
6. THE Snapshot_Producer SHALL include the current Fuel_Consumed_Total value in every
   Simulation_Snapshot as a fleet-wide integer field.
7. THE Port_Layer SHALL expose the Fuel_Consumed_Total value received in each per-tick update as an
   integer field readable by the KPI_Provider.
8. WHERE the Fuel_Consumed_Total value is present in a per-tick update, THE KPI_Provider SHALL use
   it as the value of `KpiTimeSample.FuelConsumedTotal` for that Tick.

### Requirement 2: Per-tick throughput and fuel history is available as a tick-indexed series

**User Story:** As a KPI dashboard developer, I want each tick's cumulative discharged total and
cumulative fuel-consumed total exposed as tick-indexed samples, so that I can populate
`KpiViewModel.ThroughputHistory` (KPI #2) and derive `KpiViewModel.FuelPerTon` (KPI #3).

#### Acceptance Criteria

1. THE Simulation_Snapshot SHALL carry the Tick value at which the snapshot was produced.
2. THE Simulation_Snapshot SHALL carry the Discharged_Total value as of that Tick.
3. THE Simulation_Snapshot SHALL carry the Fuel_Consumed_Total value as of that Tick.
4. WHEN a per-tick update is received, THE KPI_Provider SHALL produce a `KpiTimeSample` whose
   `Tick`, `DischargedTotal`, and `FuelConsumedTotal` equal the Tick, Discharged_Total, and
   Fuel_Consumed_Total of that update.
5. THE KPI_Provider SHALL order the samples in `KpiViewModel.ThroughputHistory` by ascending Tick.
6. WHEN the latest Discharged_Total value is greater than zero, THE KPI_Provider SHALL set
   `KpiViewModel.FuelPerTon` to the latest Fuel_Consumed_Total divided by the latest
   Discharged_Total.
7. IF the latest Discharged_Total value is zero, THEN THE KPI_Provider SHALL set
   `KpiViewModel.FuelPerTon` to zero.

### Requirement 3: Per-agent state category counts are available for utilization

**User Story:** As a KPI dashboard developer, I want per-agent counts of ticks spent in each
State_Category, so that I can populate `KpiViewModel.AgentUtilization` (KPI #1).

#### Acceptance Criteria

1. THE Simulation_Snapshot SHALL carry, for each agent, a stable agent identifier, the agent's role,
   and the agent's current FSM state for the Tick.
2. THE Port_Layer SHALL expose, for each agent per-tick update, the agent identifier, role, and
   current state as typed values readable by the KPI_Provider.
3. WHEN a per-tick update is processed, THE KPI_Provider SHALL classify each agent's current state
   into exactly one State_Category using the mapping defined in `KpiStateCategory`.
4. THE KPI_Provider SHALL produce one `AgentUtilizationKpi` per agent, populated with that agent's
   identifier and role.
5. THE KPI_Provider SHALL set each agent's `ProductiveTicks`, `IdleTicks`, and `InactiveTicks` to the
   number of Ticks that agent has been observed in the corresponding State_Category since simulation
   start.
6. THE KPI_Provider SHALL set each agent's `TotalTicks` equal to the sum of its `ProductiveTicks`,
   `IdleTicks`, and `InactiveTicks`.
7. WHERE a per-tick update omits an agent identifier that appeared in a prior update, THE
   KPI_Provider SHALL retain that agent's previously accumulated category counts.

### Requirement 4: Per-agent out-of-fuel event counts are available

**User Story:** As a KPI dashboard developer, I want a per-agent count of transitions into the
Inactive state, so that I can populate `KpiViewModel.OutOfFuelEvents` (KPI #5).

#### Acceptance Criteria

1. THE Simulation_Snapshot SHALL carry, for each agent, sufficient state information per Tick to
   determine whether the agent has entered the Inactive state on that Tick.
2. WHEN an agent's observed state changes from a non-Inactive State_Category to Inactive between two
   consecutive Ticks, THE KPI_Provider SHALL count exactly one Out_Of_Fuel_Event for that agent.
3. WHILE an agent remains continuously Inactive across successive Ticks, THE KPI_Provider SHALL count
   no additional Out_Of_Fuel_Event for that agent.
4. THE KPI_Provider SHALL produce one `OutOfFuelEventsKpi` per agent, populated with that agent's
   identifier and role.
5. THE KPI_Provider SHALL set each agent's `OutOfFuelEventCount` to the total number of
   Out_Of_Fuel_Events counted for that agent since simulation start.

### Requirement 5: Field coverage data is available per tick

**User Story:** As a KPI dashboard developer, I want field dimensions and per-cell state each tick,
so that I can populate `KpiViewModel.FieldCoverage` (KPI #4).

#### Acceptance Criteria

1. THE Simulation_Snapshot SHALL carry the field width and field height in cells.
2. THE Simulation_Snapshot SHALL carry, for each cell, the cell's grid position and current cell
   state for the Tick.
3. THE Port_Layer SHALL expose the field width, field height, and per-cell position and state as
   typed values readable by the KPI_Provider.
4. THE KPI_Provider SHALL set `FieldCoverageKpi.Width` and `FieldCoverageKpi.Height` to the field
   width and field height reported for the Tick.
5. THE KPI_Provider SHALL set `FieldCoverageKpi.WorkableCellCount` to the number of Workable_Cells
   established at simulation start.
6. THE KPI_Provider SHALL set `FieldCoverageKpi.HarvestedCellCount` to the number of Workable_Cells
   whose current state is Harvested as of the Tick.
7. WHEN `FieldCoverageKpi.WorkableCellCount` is greater than zero, THE KPI_Provider SHALL set
   `FieldCoverageKpi.CoveragePercent` to `HarvestedCellCount` divided by `WorkableCellCount`.
8. IF `FieldCoverageKpi.WorkableCellCount` is zero, THEN THE KPI_Provider SHALL set
   `FieldCoverageKpi.CoveragePercent` to zero.
9. THE KPI_Provider SHALL populate `FieldCoverageKpi.Cells` with one `FieldCoverageCell` per cell of
   interest, each carrying that cell's position and current state.

### Requirement 6: A complete KpiViewModel is produced per tick

**User Story:** As a KPI dashboard developer, I want a fully populated `KpiViewModel` produced each
tick, so that view components can consume a single, consistent data contract.

#### Acceptance Criteria

1. WHEN a per-tick update is processed, THE KPI_Provider SHALL produce exactly one `KpiViewModel`
   instance for that Tick.
2. THE KPI_Provider SHALL set `KpiViewModel.Tick` to the Tick of the update from which the instance
   was produced.
3. THE KPI_Provider SHALL populate all six members of `KpiViewModel` — `Tick`, `AgentUtilization`,
   `ThroughputHistory`, `FuelPerTon`, `FieldCoverage`, and `OutOfFuelEvents` — for each produced
   instance.
4. THE KPI_Provider SHALL treat every produced `KpiViewModel` instance and its referenced
   collections as read-only after construction.

### Requirement 7: Tick continuity is preserved for accumulated KPIs

**User Story:** As a KPI dashboard developer, I want tick ordering and continuity to be
verifiable, so that accumulated KPIs (utilization ticks and out-of-fuel events) remain accurate
when updates arrive.

#### Acceptance Criteria

1. THE Port_Layer SHALL expose the Tick value of each per-tick update as a monotonic identifier
   readable by the KPI_Provider.
2. WHEN a per-tick update arrives with a Tick value equal to or less than the most recently
   processed Tick, THE KPI_Provider SHALL discard the duplicate or out-of-order update without
   altering accumulated counts.
3. IF a per-tick update arrives with a Tick value more than one greater than the most recently
   processed Tick, THEN THE KPI_Provider SHALL record that a tick gap occurred.
4. WHILE processing updates in ascending Tick order, THE KPI_Provider SHALL increment accumulated
   category counts by exactly one Tick per agent per processed update.

### Requirement 8: Data contract consistency across the pipeline

**User Story:** As a maintainer of the AgenticModel-to-Unity pipeline, I want the new
Fuel_Consumed_Total field to be represented consistently across every layer, so that the value read
by the KPI_Provider equals the value produced by the Simulation_Core.

#### Acceptance Criteria

1. THE Simulation_Snapshot SHALL represent Fuel_Consumed_Total as an integer fleet-wide field
   alongside the existing Discharged_Total field.
2. THE Port_Layer SHALL represent Fuel_Consumed_Total as an integer field alongside the existing
   Discharged_Total field in its per-tick read surface.
3. WHEN a Simulation_Snapshot is transmitted and received, THE Port_Layer SHALL expose a
   Fuel_Consumed_Total value equal to the value the Snapshot_Producer placed in that snapshot.
4. WHEN the KPI_Provider reads Fuel_Consumed_Total, Discharged_Total, and Tick from a single
   per-tick update, THE KPI_Provider SHALL treat all three values as belonging to the same Tick.
