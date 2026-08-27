using System.Collections.Generic;
using AgroAgents.Presentation.Mapping;
using AgroAgents.Presentation.Views;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Simulation
{
    /// <summary>
    /// Owns the single <see cref="ISimulationSession"/> and drives the tick loop
    /// each Unity frame. Nothing else in the project holds a session reference, and
    /// nothing in this class ever names <c>SimulationWorld</c>.
    /// Req 2.4, 2.6, 2.7, 3.1, 3.2, 3.7, 4.1-4.6, 5.3.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationDriver : MonoBehaviour
    {
        [Header("Tick")]
        [Min(0.0001f)]
        [Tooltip("Ticks per second of unscaled real time.")]
        [SerializeField] private float tickRate = 4f;

        [Range(1, 64)]
        [Tooltip("Maximum Tick() calls in one Unity frame.")]
        [SerializeField] private int tickBudget = 4;

        [Min(0.0001f)]
        [Tooltip("Scales simulation time only. Never affects per-tick outcomes.")]
        [SerializeField] private float speedMultiplier = 1f;

        [SerializeField] private bool startPaused;

        [Header("Debug controls")]
        [SerializeField] private KeyCode pauseKey = KeyCode.P;

        [Tooltip("Advances exactly one tick while paused.")]
        [SerializeField] private KeyCode stepKey = KeyCode.Period;

        private TickAccumulator _accumulator;
        private GridView _gridView;
        private bool _lastHalted;
        private int _pendingSingleStep;
        private readonly HashSet<string> _loggedValidationWarnings = new HashSet<string>();

        public ISimulationSession Session { get; private set; }
        public CoordinateMapper Mapper { get; private set; }
        public AgentBindingRegistry Bindings { get; private set; }
        public float InterpolationAlpha { get; private set; }
        public int DischargedTotal { get; private set; }

        public TickAccumulator Accumulator => _accumulator;

        public bool IsPaused
        {
            get => _accumulator != null && _accumulator.IsPaused;
            set
            {
                if (_accumulator != null)
                {
                    _accumulator.IsPaused = value;
                }
            }
        }

        /// <summary>
        /// Called once by WorldBootstrapper after <see cref="ISimulationConnection.IsComplete"/>.
        /// Enables the component (which is authored disabled) and subscribes to
        /// <see cref="ISimulationSession.UpdateReceived"/>.
        /// </summary>
        public void Initialize(ISimulationSession session, CoordinateMapper mapper,
                               AgentBindingRegistry bindings, GridView gridView)
        {
            Session = session;
            Mapper = mapper;
            Bindings = bindings;
            _gridView = gridView;

            _accumulator = new TickAccumulator(tickRate, tickBudget, speedMultiplier, startPaused);
            _lastHalted = session.InitialSnapshot.IsHalted;
            DischargedTotal = session.InitialSnapshot.DischargedTotal;

            session.UpdateReceived += OnUpdateReceived;
            enabled = true;
        }

        /// <summary>Req 4.2: advances exactly one tick while paused.</summary>
        public void StepOneTick()
        {
            _pendingSingleStep = _accumulator.RequestSingleStep();
        }

        /// <summary>Req 3.1, 3.2: changes tick rate; non-positive values are rejected.</summary>
        public void SetTickRate(float value)
        {
            _accumulator.TickRate = value;
        }

        /// <summary>Req 4.4, 4.5, 4.6: changes speed multiplier; non-positive values are rejected.</summary>
        public void SetSpeedMultiplier(float value)
        {
            _accumulator.SpeedMultiplier = value;
        }

        private void Update()
        {
            if (Session == null)
            {
                return;
            }

            // Debug controls
            if (Input.GetKeyDown(pauseKey))
            {
                IsPaused = !IsPaused;
            }

            if (Input.GetKeyDown(stepKey))
            {
                StepOneTick();
            }

            float dt = Time.unscaledDeltaTime;
            TickPlan plan = _accumulator.Advance(dt, _lastHalted);

            int ticks = plan.TickCount;
            if (_pendingSingleStep > 0)
            {
                ticks = _pendingSingleStep;
                _pendingSingleStep = 0;
            }

            for (int i = 0; i < ticks; i++)
            {
                Session.RequestTick();
                if (_lastHalted)
                {
                    break;
                }
            }

            InterpolationAlpha = plan.InterpolationAlpha;

            // Render all bound agent views.
            IReadOnlyList<AgentBinding> bindings = Bindings.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                AgentView view = bindings[i].View;
                if (view != null)
                {
                    view.Render(InterpolationAlpha, dt);
                }
            }
        }

        private void OnUpdateReceived(WorldUpdate update)
        {
            Bindings.ApplyUpdate(update);
            _gridView.OnUpdateReceived(update);
            DischargedTotal = update.DischargedTotal;
            _lastHalted = update.IsHalted;
        }

        private void OnValidate()
        {
            if (tickRate <= 0f)
            {
                WarnOnceValidation("tickRate", "[SimulationDriver] Non-positive tickRate ignored; retaining previous value.");
            }
            if (speedMultiplier <= 0f)
            {
                WarnOnceValidation("speedMultiplier", "[SimulationDriver] Non-positive speedMultiplier ignored; retaining previous value.");
            }
            if (tickBudget < 1)
            {
                WarnOnceValidation("tickBudget", "[SimulationDriver] Non-positive tickBudget ignored; retaining previous value.");
            }

            if (_accumulator != null)
            {
                _accumulator.TickRate = tickRate;
                _accumulator.TickBudget = tickBudget;
                _accumulator.SpeedMultiplier = speedMultiplier;
            }
        }

        private void OnDestroy()
        {
            if (Session != null)
            {
                Session.UpdateReceived -= OnUpdateReceived;
            }
        }

        private void WarnOnceValidation(string key, string message)
        {
            if (_loggedValidationWarnings.Add(key))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
