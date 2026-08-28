namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// A mirror of <c>HarvestingCore.World.GridPosition</c>. Immutable, get-only.
    /// </summary>
    public readonly struct PortGridPosition
    {
        public int X { get; }
        public int Y { get; }

        public PortGridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
