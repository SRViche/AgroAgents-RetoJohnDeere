using System.Collections.Generic;
using AgroAgents.SimulationPort;
using HarvestingCore.Agents;
using HarvestingCore.World;

namespace AgroAgents.InMemoryAdapter
{
    /// <summary>
    /// Every translation between a <c>HarvestingCore</c> type and its mirrored
    /// port DTO. Lives entirely inside this assembly (Decision B1); nothing
    /// upstream of the port ever sees a <c>HarvestingCore</c> type.
    /// </summary>
    internal static class Mappings
    {
        /// <summary>Mirrors <see cref="CellState"/> member-for-member (Property 22).</summary>
        internal static PortCellState MapCellState(CellState state) => state switch
        {
            CellState.Empty => PortCellState.Empty,
            CellState.Crop => PortCellState.Crop,
            CellState.Blocked => PortCellState.Blocked,
            CellState.Harvested => PortCellState.Harvested,
        };

        /// <summary>Mirrors <see cref="StateId"/> member-for-member (Property 22).</summary>
        internal static PortStateId MapStateId(StateId state) => state switch
        {
            StateId.Idle => PortStateId.Idle,
            StateId.Harvest => PortStateId.Harvest,
            StateId.GoToRefuel => PortStateId.GoToRefuel,
            StateId.GoToDump => PortStateId.GoToDump,
            StateId.GoToMeetingPoint => PortStateId.GoToMeetingPoint,
            StateId.WaitTractor => PortStateId.WaitTractor,
            StateId.WaitHarvester => PortStateId.WaitHarvester,
            StateId.Inactive => PortStateId.Inactive,
        };

        /// <summary>Mirrors <see cref="AgentRole"/> member-for-member.</summary>
        internal static PortAgentRole MapAgentRole(AgentRole role) => role switch
        {
            AgentRole.Harvester => PortAgentRole.Harvester,
            AgentRole.Tractor => PortAgentRole.Tractor,
        };

        /// <summary>Reverse direction: a port role back to <see cref="AgentRole"/>.</summary>
        internal static AgentRole MapPortAgentRole(PortAgentRole role) => role switch
        {
            PortAgentRole.Harvester => AgentRole.Harvester,
            PortAgentRole.Tractor => AgentRole.Tractor,
        };

        /// <summary>Forward direction: <see cref="GridPosition"/> to its port mirror.</summary>
        internal static PortGridPosition MapPosition(GridPosition position) =>
            new PortGridPosition(position.X, position.Y);

        /// <summary>Reverse direction: a port position back to <see cref="GridPosition"/>.</summary>
        internal static GridPosition ToCorePosition(PortGridPosition position) =>
            new GridPosition(position.X, position.Y);

        private static PortAgentSnapshot MapAgentSnapshot(Agent agent) => new PortAgentSnapshot(
            agent.Id,
            MapAgentRole(agent.Role),
            MapPosition(agent.Position),
            MapStateId(agent.CurrentState),
            agent.Fuel,
            agent.Load,
            agent.MaxLoad,
            agent.PathInvalidatedThisTick,
            agent.MeetingPoint.HasValue ? MapPosition(agent.MeetingPoint.Value) : (PortGridPosition?)null);

        /// <summary>Maps every agent in registration order to its port mirror.</summary>
        internal static IReadOnlyList<PortAgentSnapshot> MapAgents(IReadOnlyList<Agent> agents)
        {
            var mapped = new PortAgentSnapshot[agents.Count];
            for (int i = 0; i < agents.Count; i++)
            {
                mapped[i] = MapAgentSnapshot(agents[i]);
            }
            return mapped;
        }
    }
}
