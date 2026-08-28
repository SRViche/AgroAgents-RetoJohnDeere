using System;
using System.Collections.Generic;
using AgroAgents.SimulationPort;
using HarvestingCore;
using HarvestingCore.World;

namespace AgroAgents.InMemoryAdapter
{
    /// <summary>
    /// Wraps one <see cref="SimulationWorld"/>. Translates <c>HarvestingCore</c>
    /// types to the mirrored port DTOs on every read; owns no Unity type and no
    /// mutable state beyond the wrapped world and the last-published cell states
    /// (Decision E's render-adjacent cache, used only to compute
    /// <see cref="WorldUpdate.ChangedCells"/>).
    /// </summary>
    internal sealed class InMemorySimulationSession : ISimulationSession
    {
        private readonly SimulationWorld _world;
        private readonly PortCellState[] _lastPublishedState;

        public WorldSnapshot InitialSnapshot { get; }
        public event Action<WorldUpdate> UpdateReceived;

        internal InMemorySimulationSession(SimulationWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lastPublishedState = SnapshotCellStates();
            InitialSnapshot = BuildSnapshot();
        }

        /// <summary>
        /// In-memory: synchronous. Ticks the world, diffs cell states against the
        /// last-published cache for <see cref="WorldUpdate.ChangedCells"/>, maps
        /// the resulting agent list, and raises <see cref="UpdateReceived"/>
        /// before returning.
        /// </summary>
        public void RequestTick()
        {
            _world.Tick();
            var changed = DiffCellStates();
            UpdateReceived?.Invoke(new WorldUpdate(
                _world.TickIndex,
                changed,
                Mappings.MapAgents(_world.Agents),
                _world.DischargedTotal,
                _world.IsHalted));
        }

        /// <summary>No unmanaged resources; present for a future adapter that owns a socket.</summary>
        public void Dispose()
        {
        }

        private PortCellState[] SnapshotCellStates()
        {
            var cells = _world.Model.Cells;
            var snapshot = new PortCellState[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                snapshot[i] = Mappings.MapCellState(cells[i].State);
            }
            return snapshot;
        }

        private List<PortCellSnapshot> DiffCellStates()
        {
            var model = _world.Model;
            var cells = model.Cells;
            var changed = new List<PortCellSnapshot>();
            for (int i = 0; i < cells.Count; i++)
            {
                var current = Mappings.MapCellState(cells[i].State);
                if (current != _lastPublishedState[i])
                {
                    _lastPublishedState[i] = current;
                    changed.Add(new PortCellSnapshot(Mappings.MapPosition(model.PositionOf(i)), current));
                }
            }
            return changed;
        }

        private WorldSnapshot BuildSnapshot()
        {
            var model = _world.Model;
            var cells = model.Cells;
            var portCells = new PortCellSnapshot[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                portCells[i] = new PortCellSnapshot(Mappings.MapPosition(model.PositionOf(i)), Mappings.MapCellState(cells[i].State));
            }

            return new WorldSnapshot(
                model.Width,
                model.Height,
                portCells,
                Mappings.MapAgents(_world.Agents),
                MapPositions(model.RefuelStations),
                MapPositions(model.DumpSites),
                _world.TickIndex,
                _world.DischargedTotal,
                _world.IsHalted);
        }

        private static IReadOnlyList<PortGridPosition> MapPositions(IReadOnlyList<GridPosition> positions)
        {
            var mapped = new PortGridPosition[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                mapped[i] = Mappings.MapPosition(positions[i]);
            }
            return mapped;
        }
    }
}
