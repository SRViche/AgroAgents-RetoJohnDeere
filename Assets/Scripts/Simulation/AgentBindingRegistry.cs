using System.Collections.Generic;
using AgroAgents.SimulationPort;

namespace AgroAgents.Presentation.Simulation
{
    /// <summary>
    /// Holds the agent bindings of Req 9.3 and the previous/current snapshot pair
    /// of Req 5.1. No longer holds a live core agent reference — there is no such
    /// type available to this assembly.
    /// </summary>
    public sealed class AgentBindingRegistry
    {
        private readonly List<AgentBinding> _bindings = new List<AgentBinding>();
        private readonly Dictionary<string, AgentBinding> _byId = new Dictionary<string, AgentBinding>();

        /// <summary>Bindings in ordinal-id order.</summary>
        public IReadOnlyList<AgentBinding> Bindings => _bindings;

        public bool TryGet(string agentId, out AgentBinding binding)
        {
            return _byId.TryGetValue(agentId, out binding);
        }

        public void Add(AgentBinding binding)
        {
            _bindings.Add(binding);
            _bindings.Sort((a, b) => string.CompareOrdinal(a.AgentId, b.AgentId));
            _byId[binding.AgentId] = binding;
        }

        /// <summary>
        /// Req 5.1, 5.6: for each binding, shifts CurrentSnapshot into
        /// PreviousSnapshot, then installs the matching entry of
        /// update.Agents as the new CurrentSnapshot. Called once from
        /// SimulationDriver.OnUpdateReceived.
        /// </summary>
        public void ApplyUpdate(WorldUpdate update)
        {
            foreach (PortAgentSnapshot snapshot in update.Agents)
            {
                if (_byId.TryGetValue(snapshot.Id, out AgentBinding binding))
                {
                    binding.PreviousSnapshot = binding.CurrentSnapshot;
                    binding.CurrentSnapshot = snapshot;
                }
            }
        }
    }
}
