using System;
using System.Collections.Generic;
using AgroAgents.SimulationPort;
using HarvestingCore;
using HarvestingCore.Agents;
using HarvestingCore.Configuration;
using HarvestingCore.World;

namespace AgroAgents.InMemoryAdapter
{
    /// <summary>
    /// Completes synchronously on its first <see cref="Poll"/>: builds
    /// <see cref="SimulationConfig"/>, a <see cref="DeterministicRandom"/>, the
    /// <see cref="WorldModel"/> (generated or parsed), validates and registers
    /// agents in sorted order, calls <c>RedistributeAreas()</c> once, and wraps the
    /// result in an <see cref="InMemorySimulationSession"/> — or fails with the
    /// message shapes from the design's Error Handling table (Decision G').
    /// </summary>
    internal sealed class InMemorySimulationConnection : ISimulationConnection
    {
        private readonly SessionRequest _request;

        public bool IsComplete { get; private set; }
        public bool Failed { get; private set; }
        public string Error { get; private set; }
        public IReadOnlyList<string> Warnings { get; private set; } = Array.Empty<string>();
        public ISimulationSession Session { get; private set; }

        internal InMemorySimulationConnection(SessionRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>A second call after <see cref="IsComplete"/> is already true is a no-op.</summary>
        public void Poll()
        {
            if (IsComplete)
            {
                return;
            }

            SimulationWorld world = TryBuildWorld(_request, out string error, out List<string> warnings);
            Warnings = warnings;

            if (world == null)
            {
                Failed = true;
                Error = error;
            }
            else
            {
                Session = new InMemorySimulationSession(world);
            }

            IsComplete = true;
        }

        /// <summary>
        /// Unity-free, testable in the <c>dotnet</c> host. Returns null and fills
        /// <paramref name="error"/> instead of throwing.
        /// </summary>
        internal static SimulationWorld TryBuildWorld(SessionRequest request, out string error, out List<string> warnings)
        {
            warnings = new List<string>();
            error = null;

            // Req 11.2, 11.3: SimulationConfig inside try/catch.
            SimulationConfig config;
            try
            {
                config = new SimulationConfig(
                    dumpPreferenceFactor: request.DumpPreferenceFactor,
                    capacityFactor: request.CapacityFactor,
                    harvesterFuelReserveMultiplier: request.HarvesterFuelReserveMultiplier,
                    tractorFuelReserveMultiplier: request.TractorFuelReserveMultiplier,
                    cropCost: request.CropCost,
                    emptyCost: request.EmptyCost,
                    harvestedCost: request.HarvestedCost,
                    heuristic: (HeuristicKind)request.HeuristicKind,
                    defaultMaxLoad: request.DefaultMaxLoad,
                    defaultMaxFuel: request.DefaultMaxFuel,
                    defaultFuelConsumption: request.DefaultFuelConsumption,
                    seed: request.Seed,
                    cropDensity: request.CropDensity,
                    blockedDensity: request.BlockedDensity);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                error = "Invalid configuration: " + ex.Message;
                return null;
            }

            // Req 11.2: IRandomSource as DeterministicRandom(seed).
            IRandomSource random = new DeterministicRandom(request.Seed);

            List<GridPosition> refuel = MapPositions(request.RefuelStations);
            List<GridPosition> dumps = MapPositions(request.DumpSites);

            // Req 11.4, 11.7: WorldModel, generated or parsed.
            bool isAuthored = request.AuthoredGridText != null;
            WorldModel model;
            if (isAuthored)
            {
                try
                {
                    model = WorldModel.Parse(request.AuthoredGridText, refuel, dumps);
                }
                catch (ArgumentException ex)
                {
                    error = "Authored grid: " + ex.Message;
                    return null;
                }

                if (model.Width != request.Width || model.Height != request.Height)
                {
                    warnings.Add(
                        "Authored grid dimensions " + model.Width + "x" + model.Height +
                        " differ from configured " + request.Width + "x" + request.Height +
                        "; parsed dimensions win.");
                }

                CollectBlockedSiteWarnings(model, refuel, dumps, warnings);
            }
            else
            {
                model = new WorldModel(request.Width, request.Height, refuel, dumps);
            }

            var world = new SimulationWorld(model, config, random);

            if (!isAuthored)
            {
                world.GenerateGrid();
            }

            // Req 9.4 (defence in depth against the caller's own pre-validation):
            // reject duplicate ids before constructing or registering anything.
            var seenIds = new HashSet<string>();
            foreach (var spec in request.Agents)
            {
                if (!seenIds.Add(spec.Id))
                {
                    error = "Duplicate agent identifier '" + spec.Id + "'.";
                    return null;
                }
            }

            // Req 9.7: validate each agent's start cell against the now-known grid.
            foreach (var spec in request.Agents)
            {
                GridPosition corePos = Mappings.ToCorePosition(spec.Start);
                if (!model.InBounds(corePos))
                {
                    error = "Agent '" + spec.Id + "' start position " + corePos + " is out of bounds.";
                    return null;
                }
                if (model.CellAt(corePos).State == CellState.Blocked)
                {
                    error = "Agent '" + spec.Id + "' start position " + corePos + " is Blocked.";
                    return null;
                }
            }

            // Req 9.1, 9.2: construct and register in sorted order, independent of
            // the order the request happened to list them in.
            var sortedSpecs = new List<PortAgentSpec>(request.Agents);
            sortedSpecs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            foreach (var spec in sortedSpecs)
            {
                GridPosition corePos = Mappings.ToCorePosition(spec.Start);
                Agent agent;
                try
                {
                    agent = Mappings.MapPortAgentRole(spec.Role) == AgentRole.Harvester
                        ? new Harvester(spec.Id, corePos, model, config, spec.MaxLoad, spec.MaxFuel, spec.FuelConsumption)
                        : (Agent)new Tractor(spec.Id, corePos, model, config, spec.MaxLoad, spec.MaxFuel, spec.FuelConsumption);
                }
                catch (ArgumentException ex)
                {
                    error = "Core rejected agent '" + spec.Id + "': " + ex.Message;
                    return null;
                }

                world.Register(agent);
            }

            // Req 9.8: exactly once.
            world.RedistributeAreas();

            return world;
        }

        private static List<GridPosition> MapPositions(IReadOnlyList<PortGridPosition> positions)
        {
            var mapped = new List<GridPosition>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                mapped.Add(Mappings.ToCorePosition(positions[i]));
            }
            return mapped;
        }

        private static void CollectBlockedSiteWarnings(
            WorldModel model, List<GridPosition> refuel, List<GridPosition> dumps, List<string> warnings)
        {
            foreach (var pos in refuel)
            {
                if (model.CellAt(pos).State == CellState.Blocked)
                {
                    warnings.Add("Authored grid places Blocked on refuel station cell " + pos + ".");
                }
            }
            foreach (var pos in dumps)
            {
                if (model.CellAt(pos).State == CellState.Blocked)
                {
                    warnings.Add("Authored grid places Blocked on dump site cell " + pos + ".");
                }
            }
        }
    }
}
