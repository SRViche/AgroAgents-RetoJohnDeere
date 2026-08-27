using System.Collections.Generic;

namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// Everything a connector needs to open a session. One shape for every
    /// adapter; an adapter uses the subset it understands.
    /// </summary>
    public sealed class SessionRequest
    {
        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        public double CropDensity { get; }
        public double BlockedDensity { get; }

        /// <summary>Null when generating.</summary>
        public string AuthoredGridText { get; }

        public IReadOnlyList<PortGridPosition> RefuelStations { get; }
        public IReadOnlyList<PortGridPosition> DumpSites { get; }

        /// <summary>Sorted by ordinal id.</summary>
        public IReadOnlyList<PortAgentSpec> Agents { get; }

        public int CropCost { get; }
        public int EmptyCost { get; }
        public int HarvestedCost { get; }

        /// <summary>Mirrors <c>HarvestingCore.HeuristicKind</c>, int-backed.</summary>
        public int HeuristicKind { get; }

        public int DefaultMaxLoad { get; }
        public int DefaultMaxFuel { get; }
        public int DefaultFuelConsumption { get; }
        public double DumpPreferenceFactor { get; }
        public double CapacityFactor { get; }
        public double HarvesterFuelReserveMultiplier { get; }
        public double TractorFuelReserveMultiplier { get; }

        public SessionRequest(
            int width,
            int height,
            int seed,
            double cropDensity,
            double blockedDensity,
            string authoredGridText,
            IReadOnlyList<PortGridPosition> refuelStations,
            IReadOnlyList<PortGridPosition> dumpSites,
            IReadOnlyList<PortAgentSpec> agents,
            int cropCost,
            int emptyCost,
            int harvestedCost,
            int heuristicKind,
            int defaultMaxLoad,
            int defaultMaxFuel,
            int defaultFuelConsumption,
            double dumpPreferenceFactor,
            double capacityFactor,
            double harvesterFuelReserveMultiplier,
            double tractorFuelReserveMultiplier)
        {
            Width = width;
            Height = height;
            Seed = seed;
            CropDensity = cropDensity;
            BlockedDensity = blockedDensity;
            AuthoredGridText = authoredGridText;
            RefuelStations = refuelStations;
            DumpSites = dumpSites;
            Agents = agents;
            CropCost = cropCost;
            EmptyCost = emptyCost;
            HarvestedCost = harvestedCost;
            HeuristicKind = heuristicKind;
            DefaultMaxLoad = defaultMaxLoad;
            DefaultMaxFuel = defaultMaxFuel;
            DefaultFuelConsumption = defaultFuelConsumption;
            DumpPreferenceFactor = dumpPreferenceFactor;
            CapacityFactor = capacityFactor;
            HarvesterFuelReserveMultiplier = harvesterFuelReserveMultiplier;
            TractorFuelReserveMultiplier = tractorFuelReserveMultiplier;
        }
    }
}
