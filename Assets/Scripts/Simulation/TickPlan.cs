namespace AgroAgents.Presentation.Simulation
{
    /// <summary>
    /// Result of one <see cref="TickAccumulator.Advance"/> call: how many ticks the
    /// caller should execute this frame, and the interpolation alpha to render with
    /// once they have all run. Req 3.1-3.8, 5.3.
    /// </summary>
    public readonly struct TickPlan
    {
        /// <summary>Ticks to execute this frame, in [0, TickBudget].</summary>
        public int TickCount { get; }

        /// <summary>Accumulator remainder over TickInterval after the loop, in [0, 1]. Req 5.3.</summary>
        public float InterpolationAlpha { get; }

        /// <summary>True when the accumulator had to be clamped this call. Req 3.6.</summary>
        public bool Clamped { get; }

        public TickPlan(int tickCount, float interpolationAlpha, bool clamped)
        {
            TickCount = tickCount;
            InterpolationAlpha = interpolationAlpha;
            Clamped = clamped;
        }
    }
}
