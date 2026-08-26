using UnityEngine;


public class TractorController : AgentController
{
    private Vector2Int meetingPoint;

    public string AssignedHarvesterId { get; private set; }

    private void Update()
    {
        if (Fuel <= 0)
        {
            return;
        }

        StepMovement();
        if (IsTravelling)
        {
            return;
        }

        switch (State)
        {
            case AgentState.Idle:
                if (NeedsRefuel())
                {
                    GoToRefuel(AgentState.Idle);
                    break;
                }
                if (Load > 0)
                {
                    GoToNearestDump();
                    break;
                }
                if (AssignedHarvesterId != null)
                {
                    SetPathTo(meetingPoint);
                    State = AgentState.GoToMeetingPoint;
                }
                break;

            case AgentState.GoToRefuel:
                if (UpdateExternalRoundTrip(() => Fuel = maxFuel))
                {
                    State = stateBeforeRefuel;
                }
                break;

            case AgentState.GoToMeetingPoint:
                if (AssignedHarvesterId == null)
                {
                    State = AgentState.Idle; // el harvester perdió el par
                    break;
                }
                if (GridPosition == meetingPoint)
                {
                    State = AgentState.WaitHarvester;
                }
                break;

            case AgentState.WaitHarvester:
                // FieldManager resuelve la transferencia.
                break;

            case AgentState.GoToDump:
                if (UpdateExternalRoundTrip(() =>
                {
                    fieldManager.AddDischarged(Load);
                    Load = 0;
                }))
                {
                    State = AgentState.Idle;
                }
                break;
        }
    }


    public void AssignToHarvester(string harvesterId, Vector2Int point)
    {
        AssignedHarvesterId = harvesterId;
        meetingPoint = point;
    }

    public void OnLoadReceived(int amount)
    {
        Load = Mathf.Min(maxLoad, Load + amount);
        AssignedHarvesterId = null;
        if (Load > 0)
        {
            GoToNearestDump();
        }
        else
        {
            State = AgentState.Idle;
        }
    }

    private void GoToNearestDump()
    {
        Vector3? dump = fieldManager.FindNearestDumpSite(transform.position);
        if (dump.HasValue)
        {
            GoToExternalPoint(dump.Value, AgentState.GoToDump);
        }
    }
}