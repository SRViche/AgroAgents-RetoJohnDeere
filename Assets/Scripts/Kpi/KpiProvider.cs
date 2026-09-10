using System;
using System.Collections.Generic;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// Stateful component that subscribes to the per-tick <see cref="WorldUpdate"/>
    /// stream and accumulates the per-agent and time-series state the dashboard KPIs
    /// require, emitting one immutable <see cref="KpiViewModel"/> per processed tick.
    ///
    /// This implementation covers construction/seeding, the tick-continuity guard,
    /// cell-diff patching, field-coverage computation, per-agent accumulation of
    /// category tick counts and out-of-fuel events, the tick-indexed throughput
    /// history with the fuel-per-ton headline, and building and emitting one
    /// immutable <see cref="KpiViewModel"/> per processed tick.
    /// </summary>
    public sealed class KpiProvider : IDisposable
    {
        // Field dimensions captured from the initial snapshot.
        private readonly int _width;
        private readonly int _height;

        // Full-grid cell-state cache, keyed by (X, Y) to avoid relying on
        // PortGridPosition's default struct equality. Seeded from the initial
        // snapshot's full cell list, then patched by per-tick diffs (later task).
        private readonly Dictionary<(int X, int Y), PortCellState> _cellCache;

        // Positions that were Crop-class (Crop or Harvested) at simulation start.
        // This set fixes WorkableCellCount for the whole session.
        private readonly HashSet<(int X, int Y)> _workablePositions;

        // Per-agent accumulators, keyed by the stable agent id. Populated by the
        // per-update accumulation logic added in a later task.
        private readonly Dictionary<string, AgentAccumulator> _agentAccumulators;

        // Ordered, tick-indexed history of cumulative discharged/fuel totals, one
        // sample appended per processed update. Because updates are only processed
        // when their tick strictly advances past the last processed tick, this list
        // is kept in strictly ascending Tick order (Requirements 2.4, 2.5).
        private readonly List<KpiTimeSample> _throughputHistory;

        // Latest fuel-per-ton ratio, recomputed from the most recent processed
        // sample. Zero until a sample with a positive DischargedTotal is seen
        // (Requirements 2.6, 2.7).
        private double _fuelPerTon;

        // The most recently processed tick; the continuity guard uses this to
        // discard duplicate/out-of-order updates (Requirement 7.2).
        private long _lastProcessedTick;

        // Set once any processed update arrives more than one tick after the last
        // processed tick, signalling that accumulated counts may undercount
        // (Requirement 7.3). Never reset for the lifetime of the session.
        private bool _gapDetected;

        private readonly ISimulationSession _session;
        private bool _disposed;

        // The most recently emitted view model. Null until the first update is
        // processed (Requirement 6.4). Refreshed on every processed tick.
        private KpiViewModel? _latest;

        /// <summary>
        /// Raised exactly once per processed update, carrying the freshly built
        /// <see cref="KpiViewModel"/> for that tick (Requirements 6.1, 6.2). Updates
        /// discarded by the tick-continuity guard do not raise this event.
        /// </summary>
        public event Action<KpiViewModel> KpiUpdated;

        /// <summary>
        /// The most recently emitted <see cref="KpiViewModel"/>, or <c>null</c> if no
        /// update has been processed yet. Lets consumers that subscribe late read the
        /// current KPI state without waiting for the next tick (Requirement 6.4).
        /// </summary>
        public KpiViewModel? Latest => _latest;

        /// <summary>
        /// Total count of workable (Crop-class at start) cells. Fixed at construction
        /// and never recomputed for the lifetime of the session.
        /// </summary>
        public int WorkableCellCount { get; }

        /// <summary>Field width in cells, from the initial snapshot.</summary>
        public int Width => _width;

        /// <summary>Field height in cells, from the initial snapshot.</summary>
        public int Height => _height;

        /// <summary>
        /// True once a processed update has arrived with a tick more than one greater
        /// than the previously processed tick, indicating a gap in the tick stream and
        /// that accumulated counts may undercount (Requirement 7.3). Latches on and is
        /// never cleared for the lifetime of the session.
        /// </summary>
        public bool GapDetected => _gapDetected;

        /// <summary>
        /// KPIs #2/#3 source data: the ordered, tick-indexed history of cumulative
        /// discharged product and cumulative fleet fuel consumption, one sample per
        /// processed tick. Kept in strictly ascending <c>Tick</c> order because the
        /// continuity guard only appends for updates whose tick advances past the
        /// last processed tick (Requirements 2.4, 2.5).
        /// </summary>
        public IReadOnlyList<KpiTimeSample> ThroughputHistory => _throughputHistory;

        /// <summary>
        /// KPI #3 headline value: the latest cumulative fuel consumed divided by the
        /// latest cumulative product discharged, or <c>0</c> when nothing has been
        /// discharged yet (zero-guard, Requirements 2.6, 2.7).
        /// </summary>
        public double FuelPerTon => _fuelPerTon;

        public KpiProvider(ISimulationSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            WorldSnapshot initial = session.InitialSnapshot;
            _width = initial.Width;
            _height = initial.Height;

            _cellCache = new Dictionary<(int X, int Y), PortCellState>();
            _workablePositions = new HashSet<(int X, int Y)>();
            _agentAccumulators = new Dictionary<string, AgentAccumulator>();
            _throughputHistory = new List<KpiTimeSample>();

            IReadOnlyList<PortCellSnapshot> cells = initial.Cells;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    PortCellSnapshot cell = cells[i];
                    var key = (cell.Position.X, cell.Position.Y);
                    _cellCache[key] = cell.State;

                    // Crop-class at start: was a Crop cell (or already Harvested).
                    if (cell.State == PortCellState.Crop || cell.State == PortCellState.Harvested)
                    {
                        _workablePositions.Add(key);
                    }
                }
            }

            WorkableCellCount = _workablePositions.Count;

            _lastProcessedTick = initial.TickIndex;

            _session.UpdateReceived += OnUpdateReceived;
        }

        /// <summary>
        /// Classifies a <see cref="PortStateId"/> into exactly one
        /// <see cref="KpiStateCategory"/>. The mapping is total over all defined
        /// <see cref="PortStateId"/> members:
        ///   Productive : Harvest, GoToDump
        ///   Idle       : Idle, GoToRefuel, GoToMeetingPoint, WaitTractor, WaitHarvester
        ///   Inactive   : Inactive
        /// </summary>
        internal static KpiStateCategory Classify(PortStateId state)
        {
            switch (state)
            {
                case PortStateId.Harvest:
                case PortStateId.GoToDump:
                    return KpiStateCategory.Productive;

                case PortStateId.Idle:
                case PortStateId.GoToRefuel:
                case PortStateId.GoToMeetingPoint:
                case PortStateId.WaitTractor:
                case PortStateId.WaitHarvester:
                    return KpiStateCategory.Idle;

                case PortStateId.Inactive:
                    return KpiStateCategory.Inactive;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state), state, "Unmapped PortStateId; classification must be total.");
            }
        }

        /// <summary>
        /// Per-update handler. Applies the tick-continuity guard (Requirement 7)
        /// before any accumulation occurs:
        ///   - Updates whose tick is at or behind the last processed tick are
        ///     duplicates or out-of-order and are discarded with no side effects
        ///     (Requirement 7.2).
        ///   - Updates whose tick is more than one beyond the last processed tick
        ///     leave a gap; the gap is recorded and the update is still processed
        ///     (Requirement 7.3).
        ///   - Contiguous updates are processed normally.
        /// After a processed update, <see cref="_lastProcessedTick"/> advances to that
        /// update's tick.
        ///
        /// Accumulation, cell-diff patching, throughput sampling, and view-model
        /// emission are added by later tasks; those steps run only for updates that
        /// pass this guard.
        /// </summary>
        private void OnUpdateReceived(WorldUpdate update)
        {
            // Requirement 7.2: discard duplicate or out-of-order updates without
            // touching accumulated counts, appending a sample, or emitting.
            if (update.TickIndex <= _lastProcessedTick)
            {
                return;
            }

            // Requirement 7.3: a jump of more than one tick means updates were
            // missed; record the gap. The newer update is still processed so the
            // provider keeps up rather than stalling.
            if (update.TickIndex > _lastProcessedTick + 1)
            {
                _gapDetected = true;
            }

            // --- Process normally (accumulation / sampling / emission added by
            //     later tasks) ---

            // Apply the per-tick cell diff to the full-grid cache so field-coverage
            // is computed from the latest known cell states (design step 2).
            ApplyCellDiff(update.ChangedCells);

            // Accumulate per-agent category counts and out-of-fuel events from the
            // agents observed in this update (design step 3).
            AccumulateAgents(update.Agents);

            // Append the tick-indexed throughput sample and refresh the fuel-per-ton
            // ratio from this update's cumulative totals (design step 4).
            AppendThroughputSample(update);

            // Advance the guard after a processed update.
            _lastProcessedTick = update.TickIndex;

            // Build and emit exactly one immutable view model for this processed
            // tick (design step 6, Requirements 6.1, 6.2, 6.3).
            EmitViewModel(update.TickIndex);
        }

        /// <summary>
        /// Builds one immutable <see cref="KpiViewModel"/> from the current
        /// accumulated state and emits it exactly once for the processed tick
        /// (design step 6):
        ///   - <see cref="KpiViewModel.Tick"/> is the processed update's tick.
        ///   - <see cref="KpiViewModel.AgentUtilization"/> carries one
        ///     <see cref="AgentUtilizationKpi"/> per observed agent (Requirement 3).
        ///   - <see cref="KpiViewModel.ThroughputHistory"/> is the ordered sample
        ///     history and <see cref="KpiViewModel.FuelPerTon"/> the latest ratio
        ///     (Requirement 2).
        ///   - <see cref="KpiViewModel.FieldCoverage"/> is computed from the cell
        ///     cache over the workable baseline (Requirement 5).
        ///   - <see cref="KpiViewModel.OutOfFuelEvents"/> carries one
        ///     <see cref="OutOfFuelEventsKpi"/> per observed agent (Requirement 4).
        /// All six members are populated with non-null collections and initialised
        /// values (Requirement 6.3). The built instance becomes <see cref="Latest"/>
        /// (Requirement 6.4) and is raised via <see cref="KpiUpdated"/> once
        /// (Requirements 6.1, 6.2).
        /// </summary>
        private void EmitViewModel(long tick)
        {
            var agentUtilization = new List<AgentUtilizationKpi>(_agentAccumulators.Count);
            var outOfFuelEvents = new List<OutOfFuelEventsKpi>(_agentAccumulators.Count);

            foreach (KeyValuePair<string, AgentAccumulator> entry in _agentAccumulators)
            {
                string agentId = entry.Key;
                AgentAccumulator accumulator = entry.Value;

                int totalTicks =
                    accumulator.ProductiveTicks
                    + accumulator.IdleTicks
                    + accumulator.InactiveTicks;

                agentUtilization.Add(new AgentUtilizationKpi(
                    agentId,
                    accumulator.Role,
                    accumulator.ProductiveTicks,
                    accumulator.IdleTicks,
                    accumulator.InactiveTicks,
                    totalTicks));

                outOfFuelEvents.Add(new OutOfFuelEventsKpi(
                    agentId,
                    accumulator.Role,
                    accumulator.OutOfFuelEventCount));
            }

            var viewModel = new KpiViewModel(
                tick,
                agentUtilization,
                _throughputHistory,
                _fuelPerTon,
                ComputeFieldCoverage(),
                outOfFuelEvents);

            _latest = viewModel;
            KpiUpdated?.Invoke(viewModel);
        }

        /// <summary>
        /// Folds the agents observed in a processed update into their per-agent
        /// accumulators (design step 3):
        ///   - Each observed agent's current state is classified into exactly one
        ///     <see cref="KpiStateCategory"/> and that category's tick count is
        ///     incremented by exactly one (Requirements 3.3, 7.4).
        ///   - When an agent's category moves from a non-Inactive category to
        ///     <see cref="KpiStateCategory.Inactive"/> relative to its previously
        ///     observed category, one out-of-fuel event is counted; remaining
        ///     continuously Inactive counts no further events (Requirements 4.2, 4.3).
        ///   - A fresh accumulator is created on first observation, carrying the
        ///     agent's id and role (Requirements 3.4, 4.4).
        ///   - Agents absent from this update are simply not visited, so their
        ///     accumulators are retained unchanged (Requirement 3.7).
        /// </summary>
        private void AccumulateAgents(IReadOnlyList<PortAgentSnapshot> agents)
        {
            if (agents == null)
            {
                return;
            }

            for (int i = 0; i < agents.Count; i++)
            {
                PortAgentSnapshot agent = agents[i];

                if (!_agentAccumulators.TryGetValue(agent.Id, out AgentAccumulator accumulator))
                {
                    accumulator = new AgentAccumulator { Role = agent.Role };
                    _agentAccumulators[agent.Id] = accumulator;
                }

                KpiStateCategory category = Classify(agent.CurrentState);

                switch (category)
                {
                    case KpiStateCategory.Productive:
                        accumulator.ProductiveTicks++;
                        break;
                    case KpiStateCategory.Idle:
                        accumulator.IdleTicks++;
                        break;
                    case KpiStateCategory.Inactive:
                        accumulator.InactiveTicks++;
                        break;
                }

                // Requirements 4.2/4.3: count one event only on a transition from a
                // non-Inactive category into Inactive. A first observation that is
                // already Inactive has no prior non-Inactive category, so it is not
                // an event; staying Inactive across ticks is likewise not an event.
                if (category == KpiStateCategory.Inactive
                    && accumulator.PreviousCategory.HasValue
                    && accumulator.PreviousCategory.Value != KpiStateCategory.Inactive)
                {
                    accumulator.OutOfFuelEventCount++;
                }

                accumulator.PreviousCategory = category;
            }
        }

        /// <summary>
        /// Appends the tick-indexed throughput sample for a processed update and
        /// refreshes the fuel-per-ton headline (design step 4):
        ///   - A <see cref="KpiTimeSample"/> carrying the update's <c>TickIndex</c>,
        ///     <c>DischargedTotal</c>, and <c>FuelConsumedTotal</c> is appended to the
        ///     ordered <see cref="ThroughputHistory"/>. Because this runs only for
        ///     updates whose tick strictly advances past the last processed tick, the
        ///     list stays in strictly ascending <c>Tick</c> order
        ///     (Requirements 1.8, 2.4, 2.5, 8.4).
        ///   - <see cref="FuelPerTon"/> is recomputed from this latest sample as
        ///     <c>FuelConsumedTotal / DischargedTotal</c> when discharged is greater
        ///     than zero, and is <c>0</c> otherwise (zero-guard, Requirements 2.6, 2.7).
        /// </summary>
        private void AppendThroughputSample(WorldUpdate update)
        {
            _throughputHistory.Add(new KpiTimeSample(
                update.TickIndex,
                update.DischargedTotal,
                update.FuelConsumedTotal));

            _fuelPerTon = update.DischargedTotal > 0
                ? (double)update.FuelConsumedTotal / update.DischargedTotal
                : 0.0;
        }

        /// <summary>
        /// Patches the full-grid cell-state cache with the per-tick diff carried by
        /// a <see cref="WorldUpdate"/>. <see cref="WorldUpdate.ChangedCells"/> is a
        /// diff, not the full grid, so each changed cell overwrites the cached state
        /// for its position; positions absent from the diff keep their prior state
        /// (design "Apply cell diff" step).
        /// </summary>
        private void ApplyCellDiff(IReadOnlyList<PortCellSnapshot> changedCells)
        {
            if (changedCells == null)
            {
                return;
            }

            for (int i = 0; i < changedCells.Count; i++)
            {
                PortCellSnapshot cell = changedCells[i];
                _cellCache[(cell.Position.X, cell.Position.Y)] = cell.State;
            }
        }

        /// <summary>
        /// Computes KPI #4 (field coverage) from the current cell-state cache,
        /// restricted to the workable-cell baseline fixed at construction:
        ///   - <see cref="FieldCoverageKpi.Width"/>/<see cref="FieldCoverageKpi.Height"/>
        ///     mirror the field dimensions (Requirement 5.4).
        ///   - <see cref="FieldCoverageKpi.WorkableCellCount"/> is the fixed baseline
        ///     size (Requirement 5.5).
        ///   - <see cref="FieldCoverageKpi.HarvestedCellCount"/> counts workable
        ///     positions whose current cached state is <see cref="PortCellState.Harvested"/>
        ///     (Requirement 5.6).
        ///   - <see cref="FieldCoverageKpi.CoveragePercent"/> is harvested / workable,
        ///     with a zero-guard when there are no workable cells (Requirements 5.7, 5.8).
        ///   - <see cref="FieldCoverageKpi.Cells"/> carries one
        ///     <see cref="FieldCoverageCell"/> per workable position with its current
        ///     state (Requirement 5.9).
        /// </summary>
        private FieldCoverageKpi ComputeFieldCoverage()
        {
            int harvestedCellCount = 0;
            var cells = new List<FieldCoverageCell>(_workablePositions.Count);

            foreach ((int X, int Y) position in _workablePositions)
            {
                PortCellState state = _cellCache.TryGetValue(position, out PortCellState cached)
                    ? cached
                    : PortCellState.Crop;

                if (state == PortCellState.Harvested)
                {
                    harvestedCellCount++;
                }

                cells.Add(new FieldCoverageCell(new PortGridPosition(position.X, position.Y), state));
            }

            double coveragePercent = WorkableCellCount > 0
                ? (double)harvestedCellCount / WorkableCellCount
                : 0.0;

            return new FieldCoverageKpi(
                _width,
                _height,
                harvestedCellCount,
                WorkableCellCount,
                coveragePercent,
                cells);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _session.UpdateReceived -= OnUpdateReceived;
            _disposed = true;
        }

        /// <summary>
        /// Per-agent accumulator: running category tick counts, out-of-fuel event
        /// count, and the previously-observed category used to detect transitions
        /// into Inactive. Populated by the per-update accumulation logic (later task).
        /// </summary>
        internal sealed class AgentAccumulator
        {
            public PortAgentRole Role { get; set; }
            public int ProductiveTicks { get; set; }
            public int IdleTicks { get; set; }
            public int InactiveTicks { get; set; }
            public int OutOfFuelEventCount { get; set; }
            public KpiStateCategory? PreviousCategory { get; set; }
        }
    }
}
