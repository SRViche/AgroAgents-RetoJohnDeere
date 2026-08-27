using System;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Visual definition for a single <see cref="PortCellState"/>: floor material
    /// and optional content prefab with variants.
    /// </summary>
    [Serializable]
    public struct CellVisual
    {
        public PortCellState State;
        public Material FloorMaterial;

        /// <summary>
        /// Primary content prefab instantiated for this cell state.
        /// Null is valid (Req 7.8): the cell renders floor material only.
        /// </summary>
        public GameObject ContentPrefab;

        /// <summary>
        /// Additional content variants. Selection is deterministic:
        /// <c>flatIndex % variants.Length</c>, never <c>UnityEngine.Random</c>.
        /// </summary>
        public GameObject[] ContentVariants;
    }
}
