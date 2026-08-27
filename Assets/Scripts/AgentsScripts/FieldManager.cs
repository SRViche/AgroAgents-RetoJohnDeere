using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inert shell. All field coordination logic (tractor pairing, meeting-point
/// negotiation, transfer resolution, discharge accounting, nearest-target
/// searches) has moved to HarvestingCore via the ISimulationSession. This class
/// retains only its serialized fields so that existing scene references remain
/// valid until it is deleted wholesale in group 6 (task 32).
/// </summary>
public class FieldManager : MonoBehaviour
{
    [SerializeField] private List<Transform> refuelStations = new List<Transform>();
    [SerializeField] private List<Transform> dumpSites = new List<Transform>();
}
