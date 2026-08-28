using AgroAgents.Presentation.Mapping;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Renders the tile grid from <see cref="WorldSnapshot"/> and applies per-tick
    /// diffs from <see cref="WorldUpdate.ChangedCells"/>. No shadow array, no polling:
    /// the adapter computes <c>ChangedCells</c> once and delivers it here (Decision E).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridView : MonoBehaviour
    {
        // --- Prefabs ---
        [Header("Prefabs")]
        [Tooltip("One instance per Cell in WorldSnapshot.Cells.")]
        [SerializeField] private GameObject floorPrefab;

        [SerializeField] private CellVisualMap cellVisualMap;

        [Tooltip("Rendered at each WorldSnapshot.RefuelStations position.")]
        [SerializeField] private GameObject refuelMarkerPrefab;

        [SerializeField] private GameObject dumpMarkerPrefab;

        // --- Hierarchy ---
        [Header("Hierarchy")]
        [Tooltip("Null parents floors to this transform.")]
        [SerializeField] private Transform floorParent;

        [SerializeField] private Transform contentParent;

        // --- Rendering ---
        [Header("Rendering")]
        [SerializeField] private float contentYOffset;

        [Tooltip("On assigns sharedMaterial to avoid one material instance per tile.")]
        [SerializeField] private bool useSharedMaterial = true;

        // --- Runtime state (not serialized) ---
        private CoordinateMapper _mapper;
        private int _width;
        private int _height;

        // Per-cell tracking for floor renderers and content instances.
        private Renderer[] _floorRenderers;
        private GameObject[] _contentInstances;
        private PortCellState[] _renderedStates;

        /// <summary>
        /// Called once by <see cref="Authoring.WorldBootstrapper"/> after the session
        /// is ready. Instantiates floor tiles and content from the initial snapshot.
        /// Req 7.1 - 7.3, 10.6.
        /// </summary>
        public void Initialize(WorldSnapshot snapshot, CoordinateMapper mapper)
        {
            _mapper = mapper;
            _width = snapshot.Width;
            _height = snapshot.Height;

            int cellCount = _width * _height;
            _floorRenderers = new Renderer[cellCount];
            _contentInstances = new GameObject[cellCount];
            _renderedStates = new PortCellState[cellCount];

            // Instantiate one floor tile per cell and apply initial visuals.
            for (int i = 0; i < snapshot.Cells.Count; i++)
            {
                PortCellSnapshot cell = snapshot.Cells[i];
                int flatIndex = cell.Position.Y * _width + cell.Position.X;

                Vector3 worldPos = _mapper.ToWorld(cell.Position);
                Transform parent = floorParent != null ? floorParent : transform;
                GameObject floor = Instantiate(floorPrefab, worldPos, Quaternion.identity, parent);

                Renderer rend = floor.GetComponentInChildren<Renderer>();
                _floorRenderers[flatIndex] = rend;
                _renderedStates[flatIndex] = cell.State;

                ApplyFloorMaterial(rend, cell.State);
                SpawnContent(flatIndex, cell.State);
            }

            // Refuel station markers.
            if (refuelMarkerPrefab != null)
            {
                for (int i = 0; i < snapshot.RefuelStations.Count; i++)
                {
                    Vector3 pos = _mapper.ToWorld(snapshot.RefuelStations[i], contentYOffset);
                    Transform parent = contentParent != null ? contentParent : transform;
                    Instantiate(refuelMarkerPrefab, pos, Quaternion.identity, parent);
                }
            }

            // Dump site markers.
            if (dumpMarkerPrefab != null)
            {
                for (int i = 0; i < snapshot.DumpSites.Count; i++)
                {
                    Vector3 pos = _mapper.ToWorld(snapshot.DumpSites[i], contentYOffset);
                    Transform parent = contentParent != null ? contentParent : transform;
                    Instantiate(dumpMarkerPrefab, pos, Quaternion.identity, parent);
                }
            }
        }

        /// <summary>
        /// Called by <see cref="Simulation.SimulationDriver"/> on every
        /// <see cref="WorldUpdate"/>. Applies <see cref="WorldUpdate.ChangedCells"/>
        /// directly — no polling, no local shadow array. Req 7.4, 7.5.
        /// </summary>
        public void OnUpdateReceived(WorldUpdate update)
        {
            for (int i = 0; i < update.ChangedCells.Count; i++)
            {
                PortCellSnapshot changed = update.ChangedCells[i];
                int flatIndex = changed.Position.Y * _width + changed.Position.X;

                PortCellState previousState = _renderedStates[flatIndex];
                _renderedStates[flatIndex] = changed.State;

                ApplyFloorMaterial(_floorRenderers[flatIndex], changed.State);

                // Crop → Harvested: destroy the crop content instance.
                if (previousState == PortCellState.Crop && changed.State == PortCellState.Harvested)
                {
                    if (_contentInstances[flatIndex] != null)
                    {
                        Destroy(_contentInstances[flatIndex]);
                        _contentInstances[flatIndex] = null;
                    }
                }
                else if (previousState != changed.State)
                {
                    // Any other state transition: destroy old content, spawn new.
                    if (_contentInstances[flatIndex] != null)
                    {
                        Destroy(_contentInstances[flatIndex]);
                        _contentInstances[flatIndex] = null;
                    }
                    SpawnContent(flatIndex, changed.State);
                }
            }
        }

        /// <summary>
        /// Test seam: returns the currently rendered state at the given flat index.
        /// </summary>
        public PortCellState RenderedStateAt(int flatIndex)
        {
            return _renderedStates[flatIndex];
        }

        private void ApplyFloorMaterial(Renderer rend, PortCellState state)
        {
            if (rend == null) return;

            Material mat;
            if (cellVisualMap.TryGet(state, out CellVisual visual))
            {
                mat = visual.FloorMaterial;
            }
            else
            {
                mat = cellVisualMap.Fallback.FloorMaterial;
            }

            if (mat == null) return;

            if (useSharedMaterial)
                rend.sharedMaterial = mat;
            else
                rend.material = mat;
        }

        private void SpawnContent(int flatIndex, PortCellState state)
        {
            if (!cellVisualMap.TryGet(state, out CellVisual visual)) return;

            // Determine which prefab to use: variants take priority via deterministic selection.
            GameObject prefab = null;
            if (visual.ContentVariants != null && visual.ContentVariants.Length > 0)
            {
                prefab = visual.ContentVariants[flatIndex % visual.ContentVariants.Length];
            }
            else if (visual.ContentPrefab != null)
            {
                prefab = visual.ContentPrefab;
            }

            if (prefab == null) return;

            // Compute world position from flatIndex.
            int x = flatIndex % _width;
            int y = flatIndex / _width;
            PortGridPosition pos = new PortGridPosition(x, y);
            Vector3 worldPos = _mapper.ToWorld(pos, contentYOffset);

            Transform parent = contentParent != null ? contentParent : transform;
            GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity, parent);
            _contentInstances[flatIndex] = instance;
        }
    }
}
