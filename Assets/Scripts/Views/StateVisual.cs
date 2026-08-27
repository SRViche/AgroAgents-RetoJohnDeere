using System;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Visual definition for a single <see cref="PortStateId"/>: material, tint,
    /// and optional badge prefab.
    /// </summary>
    [Serializable]
    public struct StateVisual
    {
        public PortStateId State;
        public Material Material;
        public Color Tint;

        /// <summary>
        /// Optional badge prefab displayed near the agent when in this state.
        /// Null is valid — no badge is shown.
        /// </summary>
        public GameObject Badge;
    }
}
