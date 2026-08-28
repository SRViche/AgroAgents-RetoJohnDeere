using System;
using System.Collections.Generic;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Views
{
    /// <summary>
    /// Maps each <see cref="PortStateId"/> to its visual representation.
    /// Shared across agent views via a single asset reference on <see cref="AgentView"/>.
    /// A missing entry applies the magenta <see cref="Fallback"/> and logs one warning
    /// per state per session (Req 8.5).
    /// </summary>
    [CreateAssetMenu(menuName = "AgroAgents/State Visual Map")]
    public sealed class StateVisualMap : ScriptableObject
    {
        [Header("Per-state visuals")]
        [Tooltip("One entry per PortStateId. Missing entries fall back and log once.")]
        [SerializeField] private StateVisual[] entries = new StateVisual[]
        {
            new StateVisual { State = PortStateId.Idle, Tint = Color.gray },
            new StateVisual { State = PortStateId.Harvest, Tint = Color.green },
            new StateVisual { State = PortStateId.GoToRefuel, Tint = new Color(1f, 0.75f, 0f) },
            new StateVisual { State = PortStateId.GoToDump, Tint = new Color(0.6f, 0.4f, 0.2f) },
            new StateVisual { State = PortStateId.GoToMeetingPoint, Tint = Color.cyan },
            new StateVisual { State = PortStateId.WaitTractor, Tint = Color.blue },
            new StateVisual { State = PortStateId.WaitHarvester, Tint = Color.blue },
            new StateVisual { State = PortStateId.Inactive, Tint = new Color(0.5f, 0.1f, 0.1f) },
        };

        [Header("Fallback (Req 8.5)")]
        [SerializeField] private Material fallbackMaterial;
        [SerializeField] private Color fallbackTint = Color.magenta;

        private readonly HashSet<PortStateId> _warnedStates = new HashSet<PortStateId>();

        /// <summary>
        /// Returns the <see cref="StateVisual"/> for the given state if an entry exists.
        /// </summary>
        public bool TryGet(PortStateId state, out StateVisual visual)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].State == state)
                {
                    visual = entries[i];
                    return true;
                }
            }

            if (_warnedStates.Add(state))
            {
                Debug.LogWarning($"[StateVisualMap] No entry for PortStateId.{state}. Applying fallback.");
            }

            visual = default;
            return false;
        }

        /// <summary>
        /// A fallback <see cref="StateVisual"/> applied when <see cref="TryGet"/> returns false.
        /// Uses the fallback material with a magenta tint (Req 8.5).
        /// </summary>
        public StateVisual Fallback => new StateVisual
        {
            State = default,
            Material = fallbackMaterial,
            Tint = fallbackTint,
            Badge = null,
        };

        /// <summary>
        /// Returns all <see cref="PortStateId"/> values that have no entry in this map.
        /// Useful for editor validation.
        /// </summary>
        public IReadOnlyList<PortStateId> MissingStates()
        {
            var allStates = (PortStateId[])Enum.GetValues(typeof(PortStateId));
            var missing = new List<PortStateId>();

            foreach (var state in allStates)
            {
                bool found = false;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].State == state)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    missing.Add(state);
                }
            }

            return missing;
        }

        /// <summary>
        /// Resets the per-session warning state so warnings will fire again on the next session.
        /// </summary>
        public void ResetWarnings()
        {
            _warnedStates.Clear();
        }
    }
}
