using System;
using System.Collections.Generic;
using AgroAgents.SimulationPort;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// Stateful component that subscribes to the per-tick <see cref="WorldUpdate"/>
    /// stream and accumulates the per-agent and time-series state the dashboard KPIs
    /// require, emitting one immutable <see cref="KpiViewModel"/> per processed tick.
    ///
    /// This partial (foundation) implementation covers construction/seeding, the
    /// per-agent accumulator type, and the state-category classification helper.
    /// Per-update accumulation, field-coverage computation, throughput history,
    /// view-model emission, and the tick-continuity guard are added by later tasks.
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

        // The most recently processed tick; the continuity guard uses this to
        // discard duplicate/out-of-order updates (Requirement 7.2).
        private long _lastProcessedTick;

        // Set once any processed update arrives more than one tick after the last
        // processed tick, signalling that accumulated counts may undercount
        // (Requirement 7.3). Never reset for the lifetime of the session.
        private bool _gapDetected;

        private readonly ISimulationSession _session;
        private bool _disposed;

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

        public KpiProvider(ISimulationSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            WorldSnapshot initial = session.InitialSnapshot;
            _width = initial.Width;
            _height = initial.Height;

            _cellCache = new Dictionary<(int X, int Y), PortCellState>();
            _workablePositions = new HashSet<(int X, int Y)>();
            _agentAccumulators = new Dictionary<string, AgentAccumulator>();

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

            // Advance the guard after a processed update.
            _lastProcessedTick = update.TickIndex;
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
