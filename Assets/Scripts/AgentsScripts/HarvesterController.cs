using UnityEngine;

/// <summary>
/// Inert shell. All harvesting logic has moved to HarvestingCore via the
/// ISimulationSession. This class retains only its serialized fields so that
/// existing scene references and prefabs remain valid until it is deleted
/// wholesale in group 6 (task 32).
/// </summary>
public class HarvesterController : AgentController
{
}
