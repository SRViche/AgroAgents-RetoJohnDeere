using System;
using AgroAgents.SimulationPort;

namespace AgroAgents.InMemoryAdapter
{
    /// <summary>
    /// The one implementation of <see cref="ISimulationConnector"/> this release
    /// ships. A plain <c>[Serializable]</c> class, not a <c>MonoBehaviour</c> —
    /// it has no scene lifecycle of its own and is wired into
    /// <c>WorldBootstrapper</c> behind a <c>[SerializeReference]</c> field
    /// (Decision B1).
    /// </summary>
    [Serializable]
    public sealed class InMemorySimulationConnector : ISimulationConnector
    {
        public ISimulationConnection Connect(SessionRequest request)
        {
            return new InMemorySimulationConnection(request);
        }
    }
}
