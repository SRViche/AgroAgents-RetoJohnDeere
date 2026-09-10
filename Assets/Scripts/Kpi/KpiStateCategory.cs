namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// The three mutually-exclusive buckets into which every agent FSM state is
    /// classified for KPI #1 ("% Productive vs. Idle vs. Inactive").
    ///
    /// The mapping from <c>PortStateId</c> to a category is a KPI concern, not a
    /// simulation concern, and is defined as follows:
    ///
    ///   Productive : Harvest, GoToDump          — the agent is directly moving
    ///                                             product toward being discharged.
    ///   Idle       : Idle, GoToRefuel,           — the agent is active (has fuel,
    ///                GoToMeetingPoint,             is ticking) but not currently
    ///                WaitTractor, WaitHarvester    producing business value.
    ///   Inactive   : Inactive                    — the agent is out of fuel and
    ///                                             cannot act until refuelled.
    ///
    /// Every tick an agent is observed, it contributes exactly one tick to exactly
    /// one of these categories. The three category tick-counts for a given agent
    /// therefore sum to the total number of ticks that agent has been observed.
    /// </summary>
    public enum KpiStateCategory
    {
        /// <summary>Directly producing business value (Harvest, GoToDump).</summary>
        Productive,

        /// <summary>Active but not producing (Idle, GoToRefuel, GoToMeetingPoint, WaitTractor, WaitHarvester).</summary>
        Idle,

        /// <summary>Out of fuel and unable to act (Inactive).</summary>
        Inactive
    }
}
