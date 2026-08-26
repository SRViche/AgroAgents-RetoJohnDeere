using System.Collections.Generic;
using UnityEngine;


public static class GridPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    public static List<Vector2Int> FindPath(GridManager gridManager, Vector2Int start, Vector2Int goal)
    {
        if (start == goal)
        {
            return new List<Vector2Int>();
        }

        var frontier = new Queue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal)
            {
                break;
            }

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int next = current + Directions[i];
                if (cameFrom.ContainsKey(next))
                {
                    continue;
                }

                TileData tile = gridManager.GetTile(next.x, next.y);
                if (tile == null || !tile.IsWalkable)
                {
                    continue;
                }

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
        {
            return null;
        }

        var result = new List<Vector2Int>();
        Vector2Int step = goal;
        while (step != start)
        {
            result.Add(step);
            step = cameFrom[step];
        }
        result.Reverse();
        return result;
    }
}