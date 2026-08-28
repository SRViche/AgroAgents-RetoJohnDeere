namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// Mirrors <c>HarvestingCore.World.CellState</c> member-for-member.
    /// </summary>
    public enum PortCellState
    {
        Empty,
        Crop,
        Blocked,
        Harvested
    }

    /// <summary>
    /// Mirrors <c>HarvestingCore.Agents.StateId</c> member-for-member.
    /// </summary>
    public enum PortStateId
    {
        Idle,
        Harvest,
        GoToRefuel,
        GoToDump,
        GoToMeetingPoint,
        WaitTractor,
        WaitHarvester,
        Inactive
    }

    /// <summary>
    /// Mirrors <c>HarvestingCore.Agents.AgentRole</c> member-for-member.
    /// </summary>
    public enum PortAgentRole
    {
        Harvester,
        Tractor
    }

    /// <summary>
    /// Mirrors <c>HarvestingCore.Configuration.HeuristicKind</c> by ordinal.
    /// </summary>
    public enum PortHeuristicKind
    {
        Zero,
        Octile,
        SquaredEuclidean
    }
}
