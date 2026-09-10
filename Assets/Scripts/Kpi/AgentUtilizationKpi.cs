using AgroAgents.SimulationPort;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// KPI #1 breakdown for a single agent: how many ticks that agent has spent in
    /// each <see cref="KpiStateCategory"/> since the simulation started.
    ///
    /// Intention: expose, per agent (and by aggregation, per fleet), the share of
    /// simulated time spent Productive vs. Idle vs. Inactive. This surfaces
    /// under-utilised or frequently-stalled machines. The presentation layer is
    /// expected to render these three counts as a stacked bar (one bar per agent
    /// or per fleet), converting counts to percentages of <see cref="TotalTicks"/>.
    ///
    /// Units: all counts are in <b>ticks</b> (the simulation's discrete clock).
    /// Converting ticks to wall-clock time is purely presentational and out of
    /// scope for this contract.
    ///
    /// Invariant: <see cref="ProductiveTicks"/> + <see cref="IdleTicks"/> +
    /// <see cref="InactiveTicks"/> == <see cref="TotalTicks"/>.
    /// </summary>
    public readonly struct AgentUtilizationKpi
    {
        /// <summary>Stable identifier of the agent this breakdown describes. Matches <c>PortAgentSnapshot.Id</c>.</summary>
        public string AgentId { get; }

        /// <summary>Role of the agent (Harvester or Tractor), enabling per-fleet grouping.</summary>
        public PortAgentRole Role { get; }

        /// <summary>Ticks spent in a Productive state (Harvest, GoToDump).</summary>
        public int ProductiveTicks { get; }

        /// <summary>Ticks spent in an Idle state (Idle, GoToRefuel, GoToMeetingPoint, WaitTractor, WaitHarvester).</summary>
        public int IdleTicks { get; }

        /// <summary>Ticks spent Inactive (out of fuel).</summary>
        public int InactiveTicks { get; }

        /// <summary>
        /// Total ticks this agent has been observed. Equals the sum of the three
        /// category counts and is provided for convenience so consumers need not
        /// recompute it when deriving percentages.
        /// </summary>
        public int TotalTicks { get; }

        public AgentUtilizationKpi(
            string agentId,
            PortAgentRole role,
            int productiveTicks,
            int idleTicks,
            int inactiveTicks,
            int totalTicks)
        {
            AgentId = agentId;
            Role = role;
            ProductiveTicks = productiveTicks;
            IdleTicks = idleTicks;
            InactiveTicks = inactiveTicks;
            TotalTicks = totalTicks;
        }
    }
}
