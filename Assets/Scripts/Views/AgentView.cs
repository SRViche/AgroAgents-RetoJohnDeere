using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Renders one agent from its bound <see cref="Simulation.AgentBinding"/>.
    /// Full implementation is task 30; this stub provides the serialized fields
    /// and public surface <see cref="Authoring.WorldBootstrapper"/> needs now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentView : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("Unique within the scene. Becomes the PortAgentSpec.Id, and from there whatever id the connected adapter registers.")]
        [SerializeField] private string agentId = "";

        [Tooltip("Port enum, mirrors HarvestingCore.AgentRole. Harvester and Tractor map 1:1 in the in-memory adapter.")]
        [SerializeField] private PortAgentRole role = PortAgentRole.Harvester;

        [Tooltip("X = column, Y = row, core top-left origin. PortGridPosition is a readonly struct with get-only properties and no [SerializeField], so Unity cannot serialize it; this Vector2Int is the surrogate and is converted to a PortGridPosition at bootstrap.")]
        [SerializeField] private Vector2Int startCell = new Vector2Int(0, 0);

        [Header("Capacity overrides")]
        [Tooltip("Off means the SimulationConfig defaults apply.")]
        [SerializeField] private bool overrideCapacities = false;

        [Min(1)]
        [SerializeField] private int maxLoad = 100;

        [Min(1)]
        [SerializeField] private int maxFuel = 1000;

        [Min(1)]
        [SerializeField] private int fuelConsumption = 1;

        public string AgentId => agentId;
        public PortAgentRole Role => role;
        public Vector2Int StartCell => startCell;
        public bool OverrideCapacities => overrideCapacities;
        public int MaxLoad => maxLoad;
        public int MaxFuel => maxFuel;
        public int FuelConsumption => fuelConsumption;

        /// <summary>
        /// Called by WorldBootstrapper when this view's id matches no agent
        /// in the initial snapshot. Logs one warning and disables rendering.
        /// Full implementation in task 30.
        /// </summary>
        public void MarkUnbound()
        {
            Debug.LogWarning($"[AgentView] '{agentId}' has no matching agent in the session. Rendering disabled.");
        }
    }
}
