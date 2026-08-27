namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// One authored agent to register when a session is opened. Optional
    /// tunables fall back to a <see cref="SessionRequest"/>'s defaults when null.
    /// </summary>
    public sealed class PortAgentSpec
    {
        public string Id { get; }
        public PortAgentRole Role { get; }
        public PortGridPosition Start { get; }
        public int? MaxLoad { get; }
        public int? MaxFuel { get; }
        public int? FuelConsumption { get; }

        public PortAgentSpec(
            string id,
            PortAgentRole role,
            PortGridPosition start,
            int? maxLoad,
            int? maxFuel,
            int? fuelConsumption)
        {
            Id = id;
            Role = role;
            Start = start;
            MaxLoad = maxLoad;
            MaxFuel = maxFuel;
            FuelConsumption = fuelConsumption;
        }
    }
}
