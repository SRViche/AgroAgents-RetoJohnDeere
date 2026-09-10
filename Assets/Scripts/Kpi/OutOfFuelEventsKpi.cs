using AgroAgents.SimulationPort;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// KPI #5 ("'Out of fuel' (Inactive) events per agent"): how many times each
    /// agent has run out of fuel and entered the Inactive state.
    ///
    /// Intention: detect planning, supply, or fuel-management failures. An agent
    /// with a high count is repeatedly stranding itself, which points to poor
    /// refuel scheduling or an undersized fuel budget. The presentation layer is
    /// expected to render one bar per agent and to highlight the agents with the
    /// most events.
    ///
    /// An "event" is a <i>transition into</i> Inactive — i.e. the tick on which the
    /// agent's state changes from any state to Inactive. Remaining Inactive across
    /// several consecutive ticks counts as a single event; the agent must first
    /// leave Inactive (be refuelled) before another event can be counted.
    /// </summary>
    public readonly struct OutOfFuelEventsKpi
    {
        /// <summary>Stable identifier of the agent. Matches <c>PortAgentSnapshot.Id</c>.</summary>
        public string AgentId { get; }

        /// <summary>Role of the agent (Harvester or Tractor), enabling per-fleet grouping.</summary>
        public PortAgentRole Role { get; }

        /// <summary>
        /// Number of distinct transitions into the Inactive state observed for this
        /// agent since the simulation started.
        /// </summary>
        public int OutOfFuelEventCount { get; }

        public OutOfFuelEventsKpi(string agentId, PortAgentRole role, int outOfFuelEventCount)
        {
            AgentId = agentId;
            Role = role;
            OutOfFuelEventCount = outOfFuelEventCount;
        }
    }
}
