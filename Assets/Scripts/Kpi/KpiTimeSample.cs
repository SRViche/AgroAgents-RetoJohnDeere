namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// A single point in a tick-indexed time series, capturing the cumulative
    /// figures that KPIs #2 and #3 plot over time.
    ///
    /// Intention: KPI #2 ("Harvest throughput") plots <see cref="DischargedTotal"/>
    /// against <see cref="Tick"/> to show the real business result — how much
    /// product has been harvested and discharged so far. KPI #3
    /// ("Fuel consumed per ton harvested") uses <see cref="FuelConsumedTotal"/>
    /// together with <see cref="DischargedTotal"/> to show energy efficiency
    /// (litres per ton) and its evolution.
    ///
    /// Both totals are <b>cumulative</b> (monotonically non-decreasing) values as
    /// of <see cref="Tick"/>, taken directly from the server snapshot. Storing the
    /// raw cumulative values (rather than per-tick deltas) keeps this contract
    /// unambiguous: any rate or ratio the view needs can be derived from two
    /// samples without loss of information.
    /// </summary>
    public readonly struct KpiTimeSample
    {
        /// <summary>The tick this sample corresponds to (the simulation's discrete clock, X axis).</summary>
        public long Tick { get; }

        /// <summary>
        /// Cumulative product discharged as of <see cref="Tick"/>. Mirrors the
        /// server's <c>dischargedTotal</c>. Serves as the Y value for KPI #2 and as
        /// the denominator (tons harvested) for KPI #3.
        /// </summary>
        public int DischargedTotal { get; }

        /// <summary>
        /// Cumulative fuel consumed by all agents combined as of <see cref="Tick"/>.
        /// Provided by the server as a fleet-wide total. Serves as the numerator
        /// (fuel) for the KPI #3 efficiency ratio.
        /// </summary>
        public int FuelConsumedTotal { get; }

        public KpiTimeSample(long tick, int dischargedTotal, int fuelConsumedTotal)
        {
            Tick = tick;
            DischargedTotal = dischargedTotal;
            FuelConsumedTotal = fuelConsumedTotal;
        }
    }
}
