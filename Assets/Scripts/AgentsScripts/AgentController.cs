using UnityEngine;

/// <summary>
/// Inert shell. All simulation logic has moved to HarvestingCore via the
/// ISimulationSession. This class retains only its serialized fields so that
/// existing scene references and prefabs remain valid until it is deleted
/// wholesale in group 6 (task 32).
/// </summary>
public class AgentController : MonoBehaviour
{
    [SerializeField] protected float moveSpeed = 3f;
    [SerializeField] protected float arrivalTolerance = 0.05f;
    [SerializeField] protected float rotationSpeed = 720f;
    [SerializeField] protected float forwardOffsetY = 0f;

    [SerializeField] protected int maxFuel = 100;
    [SerializeField] protected int fuelConsumptionPerTile = 1;

    [SerializeField] protected float refuelThreshold = 0.3f;

    [SerializeField] protected int maxLoad = 5;

    public string AgentId { get; protected set; }
}
