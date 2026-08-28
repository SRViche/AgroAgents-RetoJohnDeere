namespace AgroAgents.Presentation.Simulation
{
    /// <summary>
    /// Drains wall-clock time into whole simulation ticks plus a render-time
    /// interpolation alpha. Plain C#, no UnityEngine dependency, so the whole of
    /// Requirement 3 and 4 is testable without a frame loop. Req 3.1-3.8, 4.1-4.6, 5.3.
    /// </summary>
    public sealed class TickAccumulator
    {
        private float _tickRate = 1f;
        private int _tickBudget = 1;
        private float _speedMultiplier = 1f;
        private float _accumulated;

        public TickAccumulator(float tickRate, int tickBudget, float speedMultiplier, bool startPaused)
        {
            TickRate = tickRate;
            TickBudget = tickBudget;
            SpeedMultiplier = speedMultiplier;
            IsPaused = startPaused;
        }

        /// <summary>Setter rejects &lt;= 0 and retains the previous value. Req 3.2.</summary>
        public float TickRate
        {
            get => _tickRate;
            set
            {
                if (value > 0f)
                {
                    _tickRate = value;
                }
            }
        }

        /// <summary>Setter rejects &lt; 1 and retains the previous value.</summary>
        public int TickBudget
        {
            get => _tickBudget;
            set
            {
                if (value >= 1)
                {
                    _tickBudget = value;
                }
            }
        }

        /// <summary>Setter rejects &lt;= 0 and retains the previous value. Req 4.5.</summary>
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set
            {
                if (value > 0f)
                {
                    _speedMultiplier = value;
                }
            }
        }

        public bool IsPaused { get; set; }

        /// <summary>1f / TickRate.</summary>
        public float TickInterval => 1f / _tickRate;

        /// <summary>Exposed read-only for tests and HUD.</summary>
        public float Accumulated => _accumulated;

        /// <summary>
        /// Pure function of (state, deltaSeconds, halted). Mutates only the accumulator.
        /// Never calls RequestTick() itself; it returns a count. Req 3.3-3.7, 5.3.
        /// </summary>
        public TickPlan Advance(float deltaSeconds, bool halted)
        {
            if (halted)
            {
                _accumulated = 0f; // Req 3.7: no ticks, alpha settles to 0
                return new TickPlan(0, 0f, false);
            }

            if (IsPaused)
            {
                // Req 4.1: accumulator untouched while paused.
                return new TickPlan(0, _accumulated / TickInterval, false);
            }

            _accumulated += deltaSeconds * _speedMultiplier; // Req 3.3

            float interval = TickInterval;
            int count = 0;
            while (_accumulated >= interval && count < _tickBudget) // Req 3.4
            {
                _accumulated -= interval;
                count++;
            }

            bool clamped = false;
            float maxAccumulated = interval * _tickBudget;
            if (_accumulated > maxAccumulated) // Req 3.6
            {
                _accumulated = maxAccumulated;
                clamped = true;
            }

            float alpha = Clamp01(_accumulated / interval); // Req 5.3
            return new TickPlan(count, alpha, clamped);
        }

        /// <summary>Req 4.2: returns 1 when paused, 0 otherwise. Does not touch the accumulator.</summary>
        public int RequestSingleStep()
        {
            return IsPaused ? 1 : 0;
        }

        public void Reset()
        {
            _accumulated = 0f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
