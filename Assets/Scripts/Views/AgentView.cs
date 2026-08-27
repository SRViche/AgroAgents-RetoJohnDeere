using AgroAgents.Presentation.Mapping;
using AgroAgents.Presentation.Simulation;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Renders one agent by reading its bound <see cref="AgentBinding"/>'s
    /// <c>PreviousSnapshot</c>/<c>CurrentSnapshot</c>. Writes only
    /// <c>transform.position</c>, <c>transform.rotation</c>, renderer material/colour,
    /// and label text. Calls no port method.
    /// Req 2.2, 2.3, 2.5, 5.2, 5.4, 5.5, 5.7, 5.8, 5.9, 8.2, 8.3, 8.4, 9.6.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Binding")]
        [Tooltip("Unique within the scene. Becomes the PortAgentSpec.Id, and from there whatever id the connected adapter registers.")]
        [SerializeField] private string agentId = "";

        [Tooltip("Port enum, mirrors HarvestingCore.AgentRole. Harvester and Tractor map 1:1 in the in-memory adapter.")]
        [SerializeField] private PortAgentRole role = PortAgentRole.Harvester;

        [Tooltip("X = column, Y = row, core top-left origin. PortGridPosition is a readonly struct with get-only properties and no [SerializeField], so Unity cannot serialize it; this Vector2Int is the surrogate and is converted to a PortGridPosition at bootstrap.")]
        [SerializeField] private Vector2Int startCell = Vector2Int.zero;

        [Header("Capacity overrides")]
        [Tooltip("Off means the SimulationConfig defaults apply.")]
        [SerializeField] private bool overrideCapacities;

        [Min(1)]
        [SerializeField] private int maxLoad = 100;

        [Min(1)]
        [SerializeField] private int maxFuel = 1000;

        [Min(1)]
        [SerializeField] private int fuelConsumption = 1;

        [Header("Visuals")]
        [SerializeField] private StateVisualMap stateVisualMap;

        [Tooltip("Renderer whose material the StateVisualMap drives.")]
        [SerializeField] private Renderer bodyRenderer;

        [SerializeField] private Transform badgeAnchor;

        [Tooltip("Added to world Y so the model rests on the tile surface.")]
        [SerializeField] private float heightOffset;

        [Header("Rotation smoothing")]
        [Min(0f)]
        [Tooltip("Degrees per second, yaw only.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Range(-180f, 180f)]
        [Tooltip("Yaw correction for models whose forward axis is not +Z. Preserved from the deleted AgentController.")]
        [SerializeField] private float forwardOffsetY;

        [Header("Readouts")]
        [Tooltip("Optional. Shows Fuel and Load / MaxLoad read from the bound AgentBinding's PortAgentSnapshot.")]
        [SerializeField] private UnityEngine.UI.Text statusLabel;

        #endregion

        #region Public Surface

        public string AgentId => agentId;
        public PortAgentRole Role => role;

        /// <summary>
        /// Converted from the serialized <see cref="Vector2Int"/> surrogate.
        /// </summary>
        public PortGridPosition AuthoredStart => new PortGridPosition(startCell.x, startCell.y);

        public bool IsBound => _binding != null && !_markedUnbound;

        public bool OverrideCapacities => overrideCapacities;
        public int MaxLoad => maxLoad;
        public int MaxFuel => maxFuel;
        public int FuelConsumption => fuelConsumption;

        #endregion

        #region Private State

        private AgentBinding _binding;
        private CoordinateMapper _mapper;
        private bool _markedUnbound;
        private GameObject _activeBadge;

        #endregion

        #region Public Methods

        /// <summary>
        /// Called by <see cref="WorldBootstrapper"/> once the connection resolves and
        /// the matching <see cref="PortAgentSnapshot"/> is found in <c>WorldSnapshot.Agents</c>.
        /// </summary>
        public void Bind(AgentBinding binding, CoordinateMapper mapper)
        {
            _binding = binding;
            _mapper = mapper;
        }

        /// <summary>
        /// Req 9.6: logs one warning naming the id and renders nothing thereafter.
        /// </summary>
        public void MarkUnbound()
        {
            if (!_markedUnbound)
            {
                _markedUnbound = true;
                Debug.LogWarning($"[AgentView] Agent '{agentId}' has no matching snapshot entry. Rendering disabled.");
            }
        }

        /// <summary>
        /// Req 5.2-5.9, 8.2-8.4. Reads the binding's snapshots, writes only
        /// transform, renderer material/colour, and label text. Calls no port method.
        /// </summary>
        public void Render(float interpolationAlpha, float deltaTime)
        {
            if (_markedUnbound || _binding == null || _mapper == null)
            {
                return;
            }

            PortAgentSnapshot current = _binding.CurrentSnapshot;
            PortAgentSnapshot previous = _binding.PreviousSnapshot;

            // Position
            Vector3 previousWorld = _mapper.ToWorld(previous.Position, heightOffset);
            Vector3 currentWorld = _mapper.ToWorld(current.Position, heightOffset);

            if (current.CurrentState == PortStateId.Inactive)
            {
                // Inactive ignores the alpha and renders at the current grid position.
                transform.position = currentWorld;
            }
            else
            {
                transform.position = Vector3.Lerp(previousWorld, currentWorld, interpolationAlpha);
            }

            // Rotation — tick-to-tick direction, not frame-to-frame delta.
            Vector3 dir = currentWorld - previousWorld;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                Quaternion desired = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, forwardOffsetY, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, rotationSpeed * deltaTime);
            }

            // State visual
            ApplyStateVisual(current.CurrentState);

            // Status label
            if (statusLabel != null)
            {
                statusLabel.text = $"Fuel: {current.Fuel}  Load: {current.Load}/{current.MaxLoad}";
            }
        }

        #endregion

        #region Private Helpers

        private void ApplyStateVisual(PortStateId state)
        {
            if (stateVisualMap == null || bodyRenderer == null)
            {
                return;
            }

            StateVisual visual;
            if (!stateVisualMap.TryGet(state, out visual))
            {
                visual = stateVisualMap.Fallback;
            }

            if (visual.Material != null)
            {
                bodyRenderer.sharedMaterial = visual.Material;
            }

            bodyRenderer.material.color = visual.Tint;

            // Badge handling
            if (badgeAnchor != null)
            {
                if (visual.Badge != null)
                {
                    if (_activeBadge == null || _activeBadge.name != visual.Badge.name)
                    {
                        if (_activeBadge != null)
                        {
                            Destroy(_activeBadge);
                        }
                        _activeBadge = Instantiate(visual.Badge, badgeAnchor);
                    }
                }
                else if (_activeBadge != null)
                {
                    Destroy(_activeBadge);
                    _activeBadge = null;
                }
            }
        }

        #endregion
    }
}
