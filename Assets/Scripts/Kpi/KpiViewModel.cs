using System.Collections.Generic;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// Immutable, presentation-ready snapshot of every KPI the dashboard displays,
    /// computed as of a single simulation tick.
    ///
    /// This type is the <b>data contract</b> between the KPI computation stage
    /// (which is fed by data provided by the AgenticModel server) and the view
    /// components that render charts. It intentionally holds no chart, layout, or
    /// styling concerns — only the numbers and their meaning. Each KPI is described
    /// on its own field below; the numbered KPIs are:
    ///
    ///   1. Utilization        — % of time Productive vs. Idle vs. Inactive.
    ///   2. Throughput         — cumulative product discharged over time.
    ///   3. Fuel efficiency    — fuel consumed per ton harvested over time.
    ///   4. Field coverage     — which zones are worked and overall % complete.
    ///   5. Out-of-fuel events — count of Inactive events per agent.
    ///
    /// A new instance is produced whenever a fresh snapshot is processed; consumers
    /// should treat instances as read-only and never mutate the referenced lists.
    /// All time-based quantities are expressed in <b>ticks</b>, the simulation's
    /// discrete clock; mapping ticks to wall-clock time is presentational.
    /// </summary>
    public readonly struct KpiViewModel
    {
        /// <summary>
        /// The tick at which this view model was computed. All contained figures are
        /// current as of this tick.
        /// </summary>
        public long Tick { get; }

        // ── KPI #1: % time Productive vs. Idle vs. Inactive ────────────────────

        /// <summary>
        /// KPI #1. Per-agent utilisation breakdown (ticks Productive / Idle /
        /// Inactive). Fleet-level stacked bars are obtained by summing these across
        /// agents, optionally grouped by <c>Role</c>.
        /// </summary>
        public IReadOnlyList<AgentUtilizationKpi> AgentUtilization { get; }

        // ── KPI #2 & #3: throughput and fuel efficiency over time ──────────────

        /// <summary>
        /// KPIs #2 and #3. The full tick-indexed history of cumulative discharged
        /// product and cumulative fleet fuel consumption. Ordered by ascending
        /// <c>Tick</c>. KPI #2 plots <c>DischargedTotal</c> vs. tick; KPI #3 derives
        /// litres-per-ton from <c>FuelConsumedTotal</c> and <c>DischargedTotal</c>.
        /// </summary>
        public IReadOnlyList<KpiTimeSample> ThroughputHistory { get; }

        /// <summary>
        /// KPI #3, headline value. Cumulative fuel consumed per unit of product
        /// discharged as of <see cref="Tick"/> — i.e. the latest
        /// <c>FuelConsumedTotal</c> / <c>DischargedTotal</c>. Zero when nothing has
        /// been discharged yet (avoids divide-by-zero). Lower is more efficient.
        /// </summary>
        public double FuelPerTon { get; }

        // ── KPI #4: field coverage ─────────────────────────────────────────────

        /// <summary>
        /// KPI #4. Field coverage heatmap data and overall completion percentage.
        /// </summary>
        public FieldCoverageKpi FieldCoverage { get; }

        // ── KPI #5: out-of-fuel events per agent ───────────────────────────────

        /// <summary>
        /// KPI #5. Per-agent count of transitions into the Inactive (out-of-fuel)
        /// state. Rendered as one bar per agent.
        /// </summary>
        public IReadOnlyList<OutOfFuelEventsKpi> OutOfFuelEvents { get; }

        public KpiViewModel(
            long tick,
            IReadOnlyList<AgentUtilizationKpi> agentUtilization,
            IReadOnlyList<KpiTimeSample> throughputHistory,
            double fuelPerTon,
            FieldCoverageKpi fieldCoverage,
            IReadOnlyList<OutOfFuelEventsKpi> outOfFuelEvents)
        {
            Tick = tick;
            AgentUtilization = agentUtilization;
            ThroughputHistory = throughputHistory;
            FuelPerTon = fuelPerTon;
            FieldCoverage = fieldCoverage;
            OutOfFuelEvents = outOfFuelEvents;
        }
    }
}
