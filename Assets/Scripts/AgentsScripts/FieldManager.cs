using System;
using System.Collections.Generic;
using UnityEngine;


public class FieldManager : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;

    [Header("Puntos fijos fuera del grid")]
    [SerializeField] private List<Transform> refuelStations = new List<Transform>();
    [SerializeField] private List<Transform> dumpSites = new List<Transform>();

    private readonly List<HarvesterController> harvesters = new List<HarvesterController>();
    private readonly List<TractorController> tractors = new List<TractorController>();
    private readonly Dictionary<string, TractorController> harvesterToTractor =
        new Dictionary<string, TractorController>();

    public int TotalDischarged { get; private set; }

    public void Register(AgentController agent)
    {
        if (agent is HarvesterController harvester)
        {
            harvesters.Add(harvester);
        }
        else if (agent is TractorController tractor)
        {
            tractors.Add(tractor);
        }
    }

    private void Update()
    {
        ResolveTransfers();
    }

   
    private void ResolveTransfers()
    {
        for (int i = 0; i < harvesters.Count; i++)
        {
            HarvesterController harvester = harvesters[i];
            if (harvester.State != AgentState.WaitTractor)
            {
                continue;
            }
            if (!harvesterToTractor.TryGetValue(harvester.AgentId, out TractorController tractor))
            {
                continue;
            }
            if (tractor.State != AgentState.WaitHarvester || tractor.GridPosition != harvester.GridPosition)
            {
                continue;
            }

            int freeCapacity = tractor.MaxLoad - tractor.Load;
            int accepted = Mathf.Min(harvester.Load, freeCapacity);
            tractor.OnLoadReceived(accepted);
            harvester.OnLoadTransferred(accepted);
            harvesterToTractor.Remove(harvester.AgentId);
        }
    }


    public void RequestTractor(HarvesterController harvester)
    {
        TractorController best = null;
        int bestDist = int.MaxValue;

        for (int i = 0; i < tractors.Count; i++)
        {
            TractorController candidate = tractors[i];
            if (candidate.State != AgentState.Idle || candidate.AssignedHarvesterId != null)
            {
                continue;
            }

            int dist = ManhattanDistance(candidate.GridPosition, harvester.GridPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        if (best == null)
        {
            return;
        }

        best.AssignToHarvester(harvester.AgentId, harvester.GridPosition);
        harvesterToTractor[harvester.AgentId] = best;
    }

    public Vector2Int? FindNearestCropTile(Vector2Int from)
    {
        Vector2Int? best = null;
        int bestDist = int.MaxValue;

        for (int x = 0; x < gridManager.Width; x++)
        {
            for (int z = 0; z < gridManager.Height; z++)
            {
                TileData tile = gridManager.GetTile(x, z);
                if (tile == null || !tile.IsWalkable || !tile.HasCrop)
                {
                    continue;
                }

                int dist = ManhattanDistance(new Vector2Int(x, z), from);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = new Vector2Int(x, z);
                }
            }
        }

        return best;
    }


    public Vector3? FindNearestRefuelStation(Vector3 from)
    {
        return FindNearestTransform(from, refuelStations);
    }

    /// <summary>Dump site más cercano en distancia de mundo.</summary>
    public Vector3? FindNearestDumpSite(Vector3 from)
    {
        return FindNearestTransform(from, dumpSites);
    }

    public void AddDischarged(int amount)
    {
        TotalDischarged += amount;
    }

    private static Vector3? FindNearestTransform(Vector3 from, List<Transform> points)
    {
        Vector3? best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null)
            {
                continue;
            }

            float dist = Vector3.Distance(points[i].position, from);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = points[i].position;
            }
        }

        return best;
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}