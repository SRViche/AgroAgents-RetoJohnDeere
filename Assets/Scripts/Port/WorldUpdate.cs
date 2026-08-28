using System.Collections.Generic;

namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// Raised once per completed tick. Requirement 2.4's "exactly one mutating
    /// operation" is <see cref="ISimulationSession.RequestTick"/>; this is the
    /// read that follows it.
    /// </summary>
    public readonly struct WorldUpdate
    {
        public long TickIndex { get; }
        public IReadOnlyList<PortCellSnapshot> ChangedCells { get; }
        public IReadOnlyList<PortAgentSnapshot> Agents { get; }
        public int DischargedTotal { get; }
        public bool IsHalted { get; }

        public WorldUpdate(
            long tickIndex,
            IReadOnlyList<PortCellSnapshot> changedCells,
            IReadOnlyList<PortAgentSnapshot> agents,
            int dischargedTotal,
            bool isHalted)
        {
            TickIndex = tickIndex;
            ChangedCells = changedCells;
            Agents = agents;
            DischargedTotal = dischargedTotal;
            IsHalted = isHalted;
        }
    }
}
