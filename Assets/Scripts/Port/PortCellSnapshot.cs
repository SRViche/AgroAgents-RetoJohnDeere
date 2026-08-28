namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// A mirror of <c>HarvestingCore.World.Cell</c>. <c>Popularity</c> and
    /// <c>OwnerId</c> are deliberately not mirrored (see design's Data Models).
    /// </summary>
    public readonly struct PortCellSnapshot
    {
        public PortGridPosition Position { get; }
        public PortCellState State { get; }

        public PortCellSnapshot(PortGridPosition position, PortCellState state)
        {
            Position = position;
            State = state;
        }
    }
}
