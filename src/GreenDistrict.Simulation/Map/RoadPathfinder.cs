using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public sealed record RoadPathOptions(
    bool AllowBridges = true,
    float BridgeCost = 8f,
    float ExistingRoadCost = 0.35f,
    float LandCost = 1f);

public sealed record RoadPathResult(IReadOnlyList<GridPosition> Cells, float Cost)
{
    public bool Found => Cells.Count > 0;
}

public sealed class RoadPathfinder
{
    private static readonly (int Dx, int Dy)[] Directions =
    {
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0)
    };

    public RoadPathResult FindPath(
        MapGrid grid,
        GridPosition start,
        GridPosition goal,
        RoadPathOptions? options = null)
    {
        options ??= new RoadPathOptions();
        if (!CanUseEndpoint(grid, start) || !CanUseEndpoint(grid, goal))
        {
            return new RoadPathResult(Array.Empty<GridPosition>(), 0f);
        }

        var frontier = new PriorityQueue<GridPosition, float>();
        var cameFrom = new Dictionary<GridPosition, GridPosition>();
        var costSoFar = new Dictionary<GridPosition, float> { [start] = 0f };
        frontier.Enqueue(start, 0f);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == goal)
            {
                return new RoadPathResult(ReconstructPath(cameFrom, start, goal), costSoFar[goal]);
            }

            foreach (var next in Neighbors(current))
            {
                if (!CanTraverse(grid, next, options)) continue;

                var newCost = costSoFar[current] + MovementCost(grid.GetCell(next), options);
                if (costSoFar.TryGetValue(next, out var existingCost) && newCost >= existingCost) continue;

                costSoFar[next] = newCost;
                var priority = newCost + Heuristic(next, goal);
                frontier.Enqueue(next, priority);
                cameFrom[next] = current;
            }
        }

        return new RoadPathResult(Array.Empty<GridPosition>(), 0f);
    }

    private static bool CanUseEndpoint(MapGrid grid, GridPosition position)
    {
        return grid.TryGetCell(position, out var cell) &&
            cell is { IsWater: false, IsBlocked: false, HasObject: false };
    }

    private static bool CanTraverse(MapGrid grid, GridPosition position, RoadPathOptions options)
    {
        if (!grid.TryGetCell(position, out var cell) || cell == null) return false;
        if (cell.IsBlocked || cell.HasObject) return false;
        if (cell.IsWater && !options.AllowBridges) return false;
        return true;
    }

    private static float MovementCost(MapCell cell, RoadPathOptions options)
    {
        if (cell.HasRoad) return Math.Max(0.01f, options.ExistingRoadCost);
        if (cell.IsWater) return Math.Max(0.01f, options.BridgeCost);
        return Math.Max(0.01f, options.LandCost);
    }

    private static IEnumerable<GridPosition> Neighbors(GridPosition position)
    {
        foreach (var (dx, dy) in Directions)
        {
            yield return position.Offset(dx, dy);
        }
    }

    private static float Heuristic(GridPosition current, GridPosition goal)
    {
        return Math.Abs(current.X - goal.X) + Math.Abs(current.Y - goal.Y);
    }

    private static IReadOnlyList<GridPosition> ReconstructPath(
        IReadOnlyDictionary<GridPosition, GridPosition> cameFrom,
        GridPosition start,
        GridPosition goal)
    {
        var path = new List<GridPosition> { goal };
        var current = goal;
        while (current != start)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
