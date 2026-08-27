using AgroAgents.SimulationPort;

namespace AgroAgents.Presentation.Simulation
{
    /// <summary>
    /// Binds one authored agent to its live port state. Holds the previous/current
    /// snapshot pair an <see cref="AgentBindingRegistry"/> maintains across ticks.
    /// Req 5.1, 5.6, 9.3.
    /// </summary>
    public sealed class AgentBinding
    {
        public string AgentId { get; }

        /// <summary>
        /// The bound view. Typed loosely because <c>AgentView</c> does not exist
        /// yet (introduced in group 6, task 31), which would otherwise be a
        /// forward reference to a type this assembly cannot see.
        /// </summary>
        public object View { get; }

        public PortAgentSnapshot PreviousSnapshot { get; internal set; }
        public PortAgentSnapshot CurrentSnapshot { get; internal set; }

        public PortGridPosition PreviousPosition => PreviousSnapshot.Position;
        public PortGridPosition CurrentPosition => CurrentSnapshot.Position;

        /// <summary>
        /// PreviousSnapshot and CurrentSnapshot both start out identical, taken
        /// from the matching entry of WorldSnapshot.Agents at bind time, so no
        /// null or sentinel case exists on the first frame. Req 5.1.
        /// </summary>
        public AgentBinding(string agentId, object view, PortAgentSnapshot initialSnapshot)
        {
            AgentId = agentId;
            View = view;
            PreviousSnapshot = initialSnapshot;
            CurrentSnapshot = initialSnapshot;
        }
    }
}
