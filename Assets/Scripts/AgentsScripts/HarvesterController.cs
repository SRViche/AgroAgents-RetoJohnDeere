using UnityEngine;


public class HarvesterController : AgentController
{
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
                TryStartHarvesting();
                break;

            case AgentState.Harvest:
                if (NeedsRefuel())
                {
                    GoToRefuel(AgentState.Harvest);
                    break;
                }
                if (Load >= maxLoad)
                {
                    State = AgentState.WaitTractor;
                    fieldManager.RequestTractor(this);
                    break;
                }

                TileData tile = gridManager.GetTile(GridPosition.x, GridPosition.y);
                if (tile != null && tile.HasCrop)
                {
                    gridManager.ProcessHarvesterPass(transform.position);
                    Load++;
                }
                else
                {
                    TryStartHarvesting();
                }
                break;

            case AgentState.GoToRefuel:
                if (UpdateExternalRoundTrip(() => Fuel = maxFuel))
                {
                    State = stateBeforeRefuel;
                }
                break;

            case AgentState.WaitTractor:
                
                break;
        }
    }

    private void TryStartHarvesting()
    {
        Vector2Int? cropTile = fieldManager.FindNearestCropTile(GridPosition);
        if (cropTile.HasValue)
        {
            SetPathTo(cropTile.Value);
            State = AgentState.Harvest;
        }
        else
        {
            State = AgentState.Idle;
        }
    }


    public void OnLoadTransferred(int amountRemoved)
    {
        Load = Mathf.Max(0, Load - amountRemoved);
        State = AgentState.Idle;
    }
}   