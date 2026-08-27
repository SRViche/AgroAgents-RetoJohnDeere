using System.Collections.Generic;

namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// Captured once, immediately after <see cref="ISimulationConnector.Connect"/>
    /// completes. Requirements 2.1, 2.2 read this and <see cref="WorldUpdate"/>
    /// exclusively; nothing else is an authoritative read surface.
    /// </summary>
    public readonly struct WorldSnapshot
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<PortCellSnapshot> Cells { get; }
        public IReadOnlyList<PortAgentSnapshot> Agents { get; }
        public IReadOnlyList<PortGridPosition> RefuelStations { get; }
        public IReadOnlyList<PortGridPosition> DumpSites { get; }
        public long TickIndex { get; }
        public int DischargedTotal { get; }
        public bool IsHalted { get; }

        public WorldSnapshot(
            int width,
            int height,
            IReadOnlyList<PortCellSnapshot> cells,
            IReadOnlyList<PortAgentSnapshot> agents,
            IReadOnlyList<PortGridPosition> refuelStations,
            IReadOnlyList<PortGridPosition> dumpSites,
            long tickIndex,
            int dischargedTotal,
            bool isHalted)
        {
            Width = width;
            Height = height;
            Cells = cells;
            Agents = agents;
            RefuelStations = refuelStations;
            DumpSites = dumpSites;
            TickIndex = tickIndex;
            DischargedTotal = dischargedTotal;
            IsHalted = isHalted;
        }
    }
}
