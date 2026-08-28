using AgroAgents.Presentation.Mapping;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Authoring
{
    /// <summary>
    /// One refuel station or dump site, authored on a marker GameObject. Resolves to a
    /// <see cref="PortGridPosition"/> either from this transform's world position via
    /// <see cref="CoordinateMapper.TryToGrid"/>, or from an authored explicit cell.
    /// <c>WorldBootstrapper</c> collects these into <c>SessionRequest.RefuelStations</c>/
    /// <c>DumpSites</c> (Decision H). Req 10.1-10.3.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SiteMarker : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Refuel station or dump site. Passed to SessionRequest.RefuelStations or DumpSites.")]
        private SiteKind kind = SiteKind.Refuel;

        [SerializeField]
        [Tooltip("Off resolves the cell from this transform's world position via the CoordinateMapper.")]
        private bool useExplicitCell = false;

        [SerializeField]
        private Vector2Int explicitCell = new Vector2Int(0, 0);

        public SiteKind Kind => kind;

        /// <summary>
        /// Resolves this marker's grid cell. Returns the authored explicit cell when
        /// <see cref="useExplicitCell"/> is set; otherwise resolves the transform's
        /// world position through <paramref name="mapper"/>, which can fail (false,
        /// default) when the position falls outside the grid. Req 10.1, 10.2, 10.3.
        /// </summary>
        public bool TryResolveCell(CoordinateMapper mapper, out PortGridPosition cell)
        {
            if (useExplicitCell)
            {
                cell = new PortGridPosition(explicitCell.x, explicitCell.y);
                return true;
            }

            return mapper.TryToGrid(transform.position, out cell);
        }
    }
}
