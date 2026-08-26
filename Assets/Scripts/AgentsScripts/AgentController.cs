using System;
using UnityEngine;
using System.Collections.Generic;


public enum AgentState
{
    Idle,
    Harvest,
    GoToRefuel,
    GoToMeetingPoint,
    WaitTractor,
    WaitHarvester,
    GoToDump
}

public class AgentController : MonoBehaviour
{
    protected GridManager gridManager;
    protected FieldManager fieldManager;


    [SerializeField] protected float moveSpeed=3f;
    [SerializeField] protected float arrivalTolerance=0.05f;
    [SerializeField] protected float rotationSpeed=720f;
    [SerializeField] protected float forwardOffsetY=0f;

    [SerializeField] protected int maxFuel=100;
    [SerializeField] protected int fuelConsumptionPerTile=1;

    [SerializeField] protected float refuelThreshold=0.3f;

    [SerializeField] protected int maxLoad=5;

    public string AgentId {get; protected set;}
    public int Fuel {get; protected set;}
    public int Load {get; protected set;}
    public int MaxLoad => maxLoad;

    public AgentState State{get; protected set;} = AgentState.Idle;
    public Vector2Int GridPosition {get; protected set;}

    protected List<Vector2Int> path=new List<Vector2Int>();
    protected Vector3 moveTarget;
    protected bool isMoving; 
    protected AgentState stateBeforeRefuel;

    private Vector2Int gatewayTile;
    private Vector3 externalTargetPos;
    private int externalPhase;

    protected virtual void Awake(){
        gridManager = FindObjectOfType<GridManager>();
        fieldManager = FindObjectOfType<FieldManager>();
        AgentId=gameObject.name;
        Fuel=maxFuel;
    }

    void Start()
    {
        TileData tile=gridManager.GetTileAtPosition(transform.position);
        GridPosition= tile!=null ? new Vector2Int(tile.x, tile.z) : Vector2Int.zero;
        moveTarget=transform.position;

        fieldManager.Register(this);
    }

    protected bool NeedsRefuel(){
        return Fuel <= maxFuel*refuelThreshold;
    }

    protected bool IsTravelling => isMoving || path.Count>0;

    protected void SetPathTo(Vector2Int target){
        path=GridPathfinder.FindPath(gridManager, GridPosition, target) ?? new List<Vector2Int>();
    }

    /// <summary>Gira suavemente hacia targetPosition sobre el eje Y únicamente
    /// (no inclina el modelo hacia arriba/abajo). No hace nada si ya está
    /// prácticamente sobre el destino.</summary>
    private void RotateTowardsMovement(Vector3 targetPosition){
        Vector3 direction=targetPosition-transform.position;
        direction.y=0f;
        if(direction.sqrMagnitude<0.0001f){
            return;
        }
        Quaternion desiredRotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0f, forwardOffsetY, 0f);
        transform.rotation=Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed*Time.deltaTime);
    }

    protected void StepMovement(){
        if(isMoving){
            RotateTowardsMovement(moveTarget);
            transform.position=Vector3.MoveTowards(transform.position, moveTarget, moveSpeed*Time.deltaTime);
            if(Vector3.Distance(transform.position, moveTarget) <= arrivalTolerance){
                transform.position=moveTarget;
                isMoving=false;
                Fuel=Mathf.Max(0, Fuel-fuelConsumptionPerTile);
            }
            return;
        }
        if(path.Count>0){
            GridPosition=path[0];
            path.RemoveAt(0);
            moveTarget=gridManager.GridToWorld(GridPosition);
            isMoving=true;
        }
    }

    protected void GoToRefuel(AgentState returnState){
        stateBeforeRefuel=returnState;
        Vector3? station=fieldManager.FindNearestRefuelStation(transform.position);
        if(station.HasValue){
            GoToExternalPoint(station.Value, AgentState.GoToRefuel);
        }
    }

    protected void GoToExternalPoint(Vector3 worldTarget, AgentState travellingState){
        gatewayTile=gridManager.NearestWalkableTile(worldTarget);
        SetPathTo(gatewayTile);
        externalTargetPos=worldTarget;
        externalPhase=1;
        State=travellingState;
    }

    protected bool UpdateExternalRoundTrip(Action onArrived){
        if(externalPhase==0){
            return false;
        }

        RotateTowardsMovement(externalTargetPos);
        transform.position=Vector3.MoveTowards(transform.position, externalTargetPos, moveSpeed*Time.deltaTime);
        if(Vector3.Distance(transform.position, externalTargetPos)>arrivalTolerance){
            return false;
        }
        transform.position=externalTargetPos;

        if(externalPhase==1){
            onArrived?.Invoke();
            externalTargetPos=gridManager.GridToWorld(gatewayTile);
            externalPhase=3;
            return false;
        }
        
        GridPosition=gatewayTile;
        externalPhase=0;
        return true;
    }
}