using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Renders the tile grid from <see cref="WorldSnapshot"/> and applies per-tick
    /// diffs from <see cref="WorldUpdate.ChangedCells"/>. Full implementation is
    /// task 27; this stub provides the public surface other components need now.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridView : MonoBehaviour
    {
        /// <summary>
        /// Called once by WorldBootstrapper after the session is ready.
        /// Instantiates floor tiles and content from the initial snapshot.
        /// </summary>
        public void Initialize(WorldSnapshot snapshot, Mapping.CoordinateMapper mapper)
        {
            // Full implementation in task 27.
        }

        /// <summary>
        /// Called by SimulationDriver on every WorldUpdate. Applies
        /// <see cref="WorldUpdate.ChangedCells"/> to the rendered grid.
        /// </summary>
        public void OnUpdateReceived(WorldUpdate update)
        {
            // Full implementation in task 27.
        }
    }
}
