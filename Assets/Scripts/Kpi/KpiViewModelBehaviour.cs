using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// Scene-facing MonoBehaviour wrapper around the pure <see cref="KpiProvider"/>.
    /// Put this component on a GameObject so view components can reference it in the
    /// Inspector (a <c>[SerializeField] KpiViewModelBehaviour</c> link) and read the
    /// latest <see cref="KpiViewModel"/> or subscribe to <see cref="KpiUpdated"/>.
    ///
    /// The behaviour owns no simulation logic itself: it constructs a
    /// <see cref="KpiProvider"/> from the live <see cref="ISimulationSession"/> once
    /// the connection completes, re-raises the provider's per-tick view model, caches
    /// the latest instance, and disposes the provider on teardown. This mirrors how
    /// <c>SimulationDriver</c> is authored disabled and initialised by
    /// <c>WorldBootstrapper</c> after the session resolves — nothing here calls
    /// <c>FindObjectOfType</c>, and the component holds the only reference to its
    /// <see cref="KpiProvider"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KpiViewModelBehaviour : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Pretty-print the JSON (indented). Off produces one compact line per tick, easier to grep or pipe.")]
        [SerializeField] private bool prettyPrint = true;

        // Serializer options are rebuilt whenever prettyPrint changes; cached so the
        // per-tick log path does not allocate a new options object every tick.
        private JsonSerializerOptions _jsonOptions;
        private bool _cachedPrettyPrint;

        private KpiProvider _provider;
        private KpiViewModel _latest;

        /// <summary>
        /// Raised once per processed tick with the freshly built
        /// <see cref="KpiViewModel"/>. View components can subscribe from
        /// <c>OnEnable</c>/<c>OnDisable</c> to refresh their charts. Late subscribers
        /// can seed themselves from <see cref="Latest"/> if <see cref="HasValue"/>.
        /// </summary>
        public event Action<KpiViewModel> KpiUpdated;

        /// <summary>
        /// True once at least one tick has been processed and <see cref="Latest"/>
        /// holds a real view model. False before the first update arrives.
        /// </summary>
        public bool HasValue { get; private set; }

        /// <summary>
        /// The most recently emitted <see cref="KpiViewModel"/>. Only meaningful when
        /// <see cref="HasValue"/> is true; before the first tick this is the default
        /// (empty) struct. Lets a view poll in its own <c>Update</c> instead of, or in
        /// addition to, subscribing to <see cref="KpiUpdated"/>.
        /// </summary>
        public KpiViewModel Latest => _latest;

        /// <summary>
        /// Called once by <c>WorldBootstrapper</c> after the connection completes,
        /// exactly like <c>SimulationDriver.Initialize</c>. Constructs the provider
        /// (which seeds from <see cref="ISimulationSession.InitialSnapshot"/> and
        /// self-subscribes to <see cref="ISimulationSession.UpdateReceived"/>) and
        /// wires its output through this component.
        /// </summary>
        public void Initialize(ISimulationSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            // Guard against double-initialisation: dispose any prior provider first.
            if (_provider != null)
            {
                _provider.KpiUpdated -= OnKpiUpdated;
                _provider.Dispose();
            }

            _provider = new KpiProvider(session);
            _provider.KpiUpdated += OnKpiUpdated;

            Debug.Log($"[KPI] Provider initialized. WorkableCells={_provider.WorkableCellCount} " +
                      $"Grid={_provider.Width}x{_provider.Height}. Waiting for ticks...");
        }

        private void OnKpiUpdated(KpiViewModel viewModel)
        {
            _latest = viewModel;
            HasValue = true;

            // Always emit the serialized view model per tick so the log does not
            // depend on an Inspector toggle being remembered/enabled. prettyPrint
            // still controls indented vs. compact output.
            LogViewModel(viewModel);

            KpiUpdated?.Invoke(viewModel);
        }

        /// <summary>
        /// Serializes the entire <see cref="KpiViewModel"/> to JSON and logs it, one
        /// entry per processed tick. All six members (including the full agent lists,
        /// throughput history, and per-cell field coverage) are written via their
        /// public getters. Enums are emitted as names; the tick number is prefixed for
        /// easy scanning. Called every processed tick.
        /// </summary>
        private void LogViewModel(KpiViewModel vm)
        {
            try
            {
                string json = JsonSerializer.Serialize(vm, GetJsonOptions());
                Debug.Log($"[KPI Tick {vm.Tick}] {json}");
            }
            catch (Exception ex)
            {
                // System.Text.Json can throw at runtime on read-only structs even when
                // the call compiles. Surface it instead of letting it vanish inside the
                // KpiProvider event invocation (which would produce no log at all).
                Debug.LogError($"[KPI Tick {vm.Tick}] Failed to serialize KpiViewModel: {ex}");
            }
        }

        /// <summary>
        /// Returns cached <see cref="JsonSerializerOptions"/>, rebuilding them only
        /// when <see cref="prettyPrint"/> changes so the per-tick path is allocation-free
        /// in the steady state. camelCase names, enum-as-string, and no cycle handling
        /// needed (the view model is a tree).
        /// </summary>
        private JsonSerializerOptions GetJsonOptions()
        {
            if (_jsonOptions == null || _cachedPrettyPrint != prettyPrint)
            {
                _jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = prettyPrint,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                };
                _cachedPrettyPrint = prettyPrint;
            }

            return _jsonOptions;
        }

        private void OnDestroy()
        {
            if (_provider != null)
            {
                _provider.KpiUpdated -= OnKpiUpdated;
                _provider.Dispose();
                _provider = null;
            }
        }
    }
}
