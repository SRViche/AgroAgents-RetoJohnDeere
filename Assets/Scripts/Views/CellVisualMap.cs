using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Maps each <see cref="PortCellState"/> to its visual representation.
    /// Shared across all cells via a single asset reference on <see cref="GridView"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "AgroAgents/Cell Visual Map")]
    public sealed class CellVisualMap : MonoBehaviour
    {
        [Tooltip("One entry per PortCellState. Exactly four.")]
        [SerializeField] private CellVisual[] entries = new CellVisual[]
        {
            new CellVisual { State = PortCellState.Empty },
            new CellVisual { State = PortCellState.Crop },
            new CellVisual { State = PortCellState.Blocked },
            new CellVisual { State = PortCellState.Harvested },
        };

        [Header("Fallback")]
        [SerializeField] private Material fallbackFloorMaterial;

        /// <summary>
        /// Returns the <see cref="CellVisual"/> for the given state if an entry exists.
        /// Falls back gracefully (Req 7.6) when a state has no configured entry.
        /// </summary>
        public bool TryGet(PortCellState state, out CellVisual visual)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].State == state)
                {
                    visual = entries[i];
                    return true;
                }
            }

            visual = default;
            return false;
        }

        /// <summary>
        /// A fallback <see cref="CellVisual"/> applied when <see cref="TryGet"/> returns false.
        /// Uses the fallback floor material with no content prefab.
        /// </summary>
        public CellVisual Fallback => new CellVisual
        {
            State = default,
            FloorMaterial = fallbackFloorMaterial,
            ContentPrefab = null,
            ContentVariants = System.Array.Empty<GameObject>(),
        };
    }
}
