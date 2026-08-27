using System;
using System.Collections.Generic;
using AgroAgents.Presentation.Mapping;
using AgroAgents.Presentation.Simulation;
using AgroAgents.Presentation.Views;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Authoring
{
    /// <summary>
    /// Builds a <see cref="SessionRequest"/> from authored fields, opens a session
    /// via the <see cref="ISimulationConnector"/>, and drives the connection handshake
    /// to completion. No <c>FindObjectOfType</c> anywhere; all references are authored
    /// <c>[SerializeField]</c>/<c>[SerializeReference]</c> links.
    /// Req 9.1, 9.2, 9.4, 10.1-10.4, 11.1. Decision G' steps 1-4.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class WorldBootstrapper : MonoBehaviour
    {
        // --- Connection ---
        [Header("Connection")]
        [SerializeReference]
        [Tooltip("Which simulation implementation to open a session against. Only InMemorySimulationConnector ships in this release; the field exists so a future connector needs no code change here.")]
        private ISimulationConnector connector;

        // --- Wiring ---
        [Header("Wiring")]
        [Tooltip("Explicit reference. No FindObjectOfType anywhere in this project.")]
        [SerializeField] private SimulationDriver simulationDriver;

        [SerializeField] private GridView gridView;

        [Tooltip("Authored list. Registration order is derived by sorting these by ordinal id, so drag order does not affect the simulation.")]
        [SerializeField] private AgentView[] agentViews = Array.Empty<AgentView>();

        [SerializeField] private SiteMarker[] siteMarkers = Array.Empty<SiteMarker>();

        // --- Grid ---
        [Header("Grid")]
        [Tooltip("World position of GridPosition(0,0). Null falls back to this transform.")]
        [SerializeField] private Transform gridOrigin;

        [Range(1, 512)]
        [SerializeField] private int gridWidth = 32;

        [Range(1, 512)]
        [SerializeField] private int gridHeight = 32;

        [Min(0.0001f)]
        [SerializeField] private float tileSize = 1f;

        // --- World source ---
        [Header("World source")]
        [Tooltip("Generated fills SessionRequest.AuthoredGridText = null; AuthoredText fills it with the parsed text. The chosen connector decides what that means — the in-memory adapter maps Generated to GenerateGrid() and AuthoredText to WorldModel.Parse, per Decision G'.")]
        [SerializeField] private WorldSource worldSource = WorldSource.Generated;

        [Tooltip("Char grid: '.' empty, 'W' crop, '#' blocked, '_' harvested. Used only when worldSource is AuthoredText.")]
        [SerializeField] private TextAsset authoredGrid;

        // --- Determinism ---
        [Header("Determinism")]
        [Tooltip("Copied into SessionRequest.Seed.")]
        [SerializeField] private int seed = 20240101;

        // --- Grid generation ---
        [Header("Grid generation")]
        [Range(0f, 1f)]
        [SerializeField] private float cropDensity = 0.55f;

        [Range(0f, 1f)]
        [SerializeField] private float blockedDensity = 0.10f;

        // --- Terrain costs ---
        [Header("Terrain costs")]
        [Min(1)]
        [SerializeField] private int cropCost = 1;

        [Min(1)]
        [SerializeField] private int emptyCost = 2;

        [Min(1)]
        [SerializeField] private int harvestedCost = 10;

        [Tooltip("Port enum, mirrors HarvestingCore.Configuration.HeuristicKind by ordinal. Int-backed, so Unity serializes it directly, same as the core enum did before the port existed.")]
        [SerializeField] private PortHeuristicKind heuristic = PortHeuristicKind.Octile;

        // --- Agent defaults ---
        [Header("Agent defaults")]
        [Min(1)]
        [SerializeField] private int defaultMaxLoad = 100;

        [Min(1)]
        [SerializeField] private int defaultMaxFuel = 1000;

        [Min(1)]
        [SerializeField] private int defaultFuelConsumption = 1;

        // --- Coordination tunables ---
        [Header("Coordination tunables")]
        [Min(0f)]
        [SerializeField] private float dumpPreferenceFactor = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float capacityFactor = 0.5f;

        [Min(0f)]
        [SerializeField] private float harvesterFuelReserveMultiplier = 1.2f;

        [Min(0f)]
        [SerializeField] private float tractorFuelReserveMultiplier = 2.5f;

        // --- Runtime state ---
        private ISimulationConnection _connection;
        private bool _connectionResolved;
        private readonly HashSet<string> _loggedWarnings = new HashSet<string>();

        public bool InitializationFailed { get; private set; }
        public ISimulationSession Session { get; private set; }
        public CoordinateMapper Mapper { get; private set; }

        private void Awake()
        {
            // --- Pre-validate required references ---
            if (!ValidateRequiredReferences())
            {
                return;
            }

            // Build the CoordinateMapper (pure presentation geometry, independent of the connection)
            Vector3 origin = gridOrigin != null ? gridOrigin.position : transform.position;
            Mapper = new CoordinateMapper(origin, tileSize, gridWidth, gridHeight);

            // --- Step 1: Resolve and validate SiteMarkers (Req 10.3, 10.4) ---
            if (!ResolveSiteMarkers(out List<PortGridPosition> refuelStations, out List<PortGridPosition> dumpSites))
            {
                return;
            }

            // --- Step 2: Sort agentViews by ordinal id and pre-validate (Req 9.1, 9.4) ---
            if (!ValidateAndSortAgentViews())
            {
                return;
            }

            // --- Step 3: Build the SessionRequest ---
            var agents = BuildAgentSpecs();
            string authoredGridText = worldSource == WorldSource.AuthoredText && authoredGrid != null
                ? authoredGrid.text
                : null;

            var request = new SessionRequest(
                width: gridWidth,
                height: gridHeight,
                seed: seed,
                cropDensity: (double)cropDensity,
                blockedDensity: (double)blockedDensity,
                authoredGridText: authoredGridText,
                refuelStations: refuelStations,
                dumpSites: dumpSites,
                agents: agents,
                cropCost: cropCost,
                emptyCost: emptyCost,
                harvestedCost: harvestedCost,
                heuristicKind: (int)heuristic,
                defaultMaxLoad: defaultMaxLoad,
                defaultMaxFuel: defaultMaxFuel,
                defaultFuelConsumption: defaultFuelConsumption,
                dumpPreferenceFactor: (double)dumpPreferenceFactor,
                capacityFactor: (double)capacityFactor,
                harvesterFuelReserveMultiplier: (double)harvesterFuelReserveMultiplier,
                tractorFuelReserveMultiplier: (double)tractorFuelReserveMultiplier
            );

            // --- Step 4: Connect ---
            _connection = connector.Connect(request);
        }

        private void Update()
        {
            // Nothing to poll if Awake already hard-failed or the connection resolved.
            if (_connection == null || _connectionResolved)
            {
                return;
            }

            // --- Step 5: Poll the pending connection (Decision G') ---
            _connection.Poll();

            if (!_connection.IsComplete)
            {
                return;
            }

            _connectionResolved = true;

            // --- Step 6: Check for failure / relay warnings ---
            if (_connection.Failed)
            {
                HardFail(_connection.Error);
                return;
            }

            IReadOnlyList<string> warnings = _connection.Warnings;
            if (warnings != null)
            {
                for (int i = 0; i < warnings.Count; i++)
                {
                    WarnOnce($"connection_warning:{warnings[i]}", $"[Bootstrap] {warnings[i]}");
                }
            }

            // --- Step 7: Completion — bind agents, initialize driver and grid view ---
            ISimulationSession session = _connection.Session;
            Session = session;
            WorldSnapshot snapshot = session.InitialSnapshot;

            // Build the binding registry from the initial snapshot.
            var bindings = new AgentBindingRegistry();

            // Index snapshot agents by id for O(1) lookup.
            var snapshotAgentById = new Dictionary<string, PortAgentSnapshot>(snapshot.Agents.Count);
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                snapshotAgentById[snapshot.Agents[i].Id] = snapshot.Agents[i];
            }

            // Bind each AgentView whose id matches a snapshot agent.
            var boundIds = new HashSet<string>();
            for (int i = 0; i < agentViews.Length; i++)
            {
                AgentView view = agentViews[i];
                if (view == null) continue;

                if (snapshotAgentById.TryGetValue(view.AgentId, out PortAgentSnapshot agentSnapshot))
                {
                    var binding = new AgentBinding(view.AgentId, view, agentSnapshot);
                    bindings.Add(binding);
                    view.Bind(binding, Mapper);
                    boundIds.Add(view.AgentId);
                }
                else
                {
                    // Req 9.6: no matching agent in the session.
                    WarnOnce($"unbound_view:{view.AgentId}", $"[Bootstrap] AgentView '{view.AgentId}' matches no agent in the session.");
                    view.MarkUnbound();
                }
            }

            // Req 9.5: warn for any snapshot agent with no authored view.
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                string id = snapshot.Agents[i].Id;
                if (!boundIds.Contains(id))
                {
                    WarnOnce($"no_view:{id}", $"[Bootstrap] Snapshot agent '{id}' has no matching AgentView in the scene.");
                }
            }

            // Initialize the driver (enables it and subscribes to UpdateReceived).
            simulationDriver.Initialize(session, Mapper, bindings, gridView);

            // Initialize the grid view (floors, content, site markers).
            gridView.Initialize(snapshot, Mapper);
        }

        private bool ValidateRequiredReferences()
        {
            if (connector == null)
            {
                HardFail("connector is not assigned.");
                return false;
            }
            if (simulationDriver == null)
            {
                HardFail("simulationDriver is not assigned.");
                return false;
            }
            if (gridView == null)
            {
                HardFail("gridView is not assigned.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Step 1 of Decision G': resolve each SiteMarker through the mapper,
        /// validate bounds (Req 10.3) and per-kind duplicates (Req 10.4).
        /// Sorts by row-major order (Decision H).
        /// </summary>
        private bool ResolveSiteMarkers(out List<PortGridPosition> refuelStations, out List<PortGridPosition> dumpSites)
        {
            refuelStations = new List<PortGridPosition>();
            dumpSites = new List<PortGridPosition>();

            // Track occupied cells per kind for duplicate detection (Req 10.4)
            var refuelCells = new Dictionary<PortGridPosition, GameObject>();
            var dumpCells = new Dictionary<PortGridPosition, GameObject>();

            for (int i = 0; i < siteMarkers.Length; i++)
            {
                SiteMarker marker = siteMarkers[i];
                if (marker == null) continue;

                if (!marker.TryResolveCell(Mapper, out PortGridPosition cell))
                {
                    HardFail($"Site marker '{marker.gameObject.name}' maps to ({cell.X}, {cell.Y}), outside the {gridWidth}x{gridHeight} grid.");
                    refuelStations = null;
                    dumpSites = null;
                    return false;
                }

                // Bounds check (Req 10.3)
                if (!Mapper.InBounds(cell))
                {
                    HardFail($"Site marker '{marker.gameObject.name}' maps to ({cell.X}, {cell.Y}), outside the {gridWidth}x{gridHeight} grid.");
                    refuelStations = null;
                    dumpSites = null;
                    return false;
                }

                var kindCells = marker.Kind == SiteKind.Refuel ? refuelCells : dumpCells;
                if (kindCells.TryGetValue(cell, out GameObject existing))
                {
                    HardFail($"Site markers '{existing.name}' and '{marker.gameObject.name}' both map to ({cell.X}, {cell.Y}).");
                    refuelStations = null;
                    dumpSites = null;
                    return false;
                }

                kindCells[cell] = marker.gameObject;

                if (marker.Kind == SiteKind.Refuel)
                    refuelStations.Add(cell);
                else
                    dumpSites.Add(cell);
            }

            // Sort by row-major order (Decision H): lowest Y first, then lowest X
            int RowMajorCompare(PortGridPosition a, PortGridPosition b)
            {
                int cmp = a.Y.CompareTo(b.Y);
                return cmp != 0 ? cmp : a.X.CompareTo(b.X);
            }

            refuelStations.Sort(RowMajorCompare);
            dumpSites.Sort(RowMajorCompare);
            return true;
        }

        /// <summary>
        /// Step 2 of Decision G': sort agentViews by string.CompareOrdinal on AgentId,
        /// reject duplicates (Req 9.4) and empty/null ids (Req 9.1).
        /// </summary>
        private bool ValidateAndSortAgentViews()
        {
            // Validate ids first
            for (int i = 0; i < agentViews.Length; i++)
            {
                AgentView view = agentViews[i];
                if (view == null) continue;

                if (string.IsNullOrWhiteSpace(view.AgentId))
                {
                    HardFail($"AgentView on '{view.gameObject.name}' has no identifier.");
                    return false;
                }
            }

            // Sort by ordinal id
            Array.Sort(agentViews, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return string.CompareOrdinal(a.AgentId, b.AgentId);
            });

            // Check for duplicates after sorting (adjacent elements)
            for (int i = 1; i < agentViews.Length; i++)
            {
                if (agentViews[i] == null || agentViews[i - 1] == null) continue;

                if (string.CompareOrdinal(agentViews[i - 1].AgentId, agentViews[i].AgentId) == 0)
                {
                    HardFail($"Duplicate agent identifier '{agentViews[i].AgentId}' on '{agentViews[i - 1].gameObject.name}' and '{agentViews[i].gameObject.name}'.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Step 3 helper: build the sorted <see cref="PortAgentSpec"/> list from the
        /// already-sorted <see cref="agentViews"/> array. Converts the Vector2Int
        /// surrogate to PortGridPosition.
        /// </summary>
        private List<PortAgentSpec> BuildAgentSpecs()
        {
            var specs = new List<PortAgentSpec>(agentViews.Length);

            for (int i = 0; i < agentViews.Length; i++)
            {
                AgentView view = agentViews[i];
                if (view == null) continue;

                var start = new PortGridPosition(view.StartCell.x, view.StartCell.y);
                int? maxLoad = view.OverrideCapacities ? view.MaxLoad : (int?)null;
                int? maxFuel = view.OverrideCapacities ? view.MaxFuel : (int?)null;
                int? fuelConsumption = view.OverrideCapacities ? view.FuelConsumption : (int?)null;

                specs.Add(new PortAgentSpec(
                    id: view.AgentId,
                    role: view.Role,
                    start: start,
                    maxLoad: maxLoad,
                    maxFuel: maxFuel,
                    fuelConsumption: fuelConsumption
                ));
            }

            return specs;
        }

        private void HardFail(string message)
        {
            Debug.LogError($"[Bootstrap] {message}");
            InitializationFailed = true;
        }

        private void WarnOnce(string key, string message)
        {
            if (_loggedWarnings.Add(key))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
