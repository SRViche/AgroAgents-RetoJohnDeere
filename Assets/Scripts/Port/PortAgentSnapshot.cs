namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// A mirror of <c>HarvestingCore.Agents.Agent</c>. <c>Path</c> is deliberately
    /// not mirrored; only <see cref="PathInvalidatedThisTick"/>, a boolean fact
    /// about the path, is (see design's Data Models).
    /// </summary>
    public readonly struct PortAgentSnapshot
    {
        public string Id { get; }
        public PortAgentRole Role { get; }
        public PortGridPosition Position { get; }
        public PortStateId CurrentState { get; }
        public int Fuel { get; }
        public int Load { get; }
        public int MaxLoad { get; }
        public bool PathInvalidatedThisTick { get; }
        public PortGridPosition? MeetingPoint { get; }

        public PortAgentSnapshot(
            string id,
            PortAgentRole role,
            PortGridPosition position,
            PortStateId currentState,
            int fuel,
            int load,
            int maxLoad,
            bool pathInvalidatedThisTick,
            PortGridPosition? meetingPoint)
        {
            Id = id;
            Role = role;
            Position = position;
            CurrentState = currentState;
            Fuel = fuel;
            Load = load;
            MaxLoad = maxLoad;
            PathInvalidatedThisTick = pathInvalidatedThisTick;
            MeetingPoint = meetingPoint;
        }
    }
}
