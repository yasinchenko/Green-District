using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public sealed record MapNewDistrictOptions(
    int WidthMeters = 48,
    int HeightMeters = 48,
    int CandidateStepMeters = 4,
    int LocalRoadWidthMeters = 6,
    int RegionalRoadWidthMeters = 8,
    int ExpansionProbeDepthMeters = 8);

public sealed record MapNewDistrictResult(
    MapDistrictGridArea Area,
    IReadOnlyList<GridPosition> RegionalRoadPath,
    MapFreeSpaceIndex FreeSpace,
    MapDistrictBoundary Boundary,
    MapDistrictExpansionSpace ExpansionSpace);

public sealed class MapDistrictFounder
{
    private readonly RoadPathfinder _pathfinder = new();

    public bool TryCreateDistrict(
        MapGrid grid,
        int existingDistrictId,
        int newDistrictId,
        string name,
        MapNewDistrictOptions? options,
        out MapNewDistrictResult? result)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("District name is required.", nameof(name));
        if (existingDistrictId == newDistrictId) throw new ArgumentException("New district id must be different from source district id.", nameof(newDistrictId));

        options ??= new MapNewDistrictOptions();
        ValidateOptions(options);
        var sourceRoads = GetDistrictRoadCells(grid, existingDistrictId).ToList();
        if (sourceRoads.Count == 0)
        {
            result = null;
            return false;
        }

        foreach (var candidate in CandidateAreas(grid, newDistrictId, name, options, sourceRoads))
        {
            var localRoads = BuildLocalRoadCells(candidate).ToList();
            var regionalPath = FindRegionalConnection(grid, sourceRoads, localRoads);
            if (regionalPath.Count == 0) continue;

            ClaimDistrictArea(grid, candidate);
            if (!BuildLocalRoads(grid, candidate, localRoads, options.LocalRoadWidthMeters))
            {
                ClearDistrictArea(grid, candidate);
                continue;
            }

            if (!grid.TryBuildRoadPath(regionalPath, RoadKind.RegionalRoad, options.RegionalRoadWidthMeters, districtId: null))
            {
                ClearDistrictArea(grid, candidate);
                continue;
            }

            var boundary = MapDistrictBoundary.Build(grid, newDistrictId);
            result = new MapNewDistrictResult(
                candidate,
                regionalPath,
                MapFreeSpaceIndex.Build(grid),
                boundary,
                MapDistrictExpansionSpace.Build(grid, boundary, options.ExpansionProbeDepthMeters));
            return true;
        }

        result = null;
        return false;
    }

    private IReadOnlyList<GridPosition> FindRegionalConnection(
        MapGrid grid,
        IReadOnlyList<GridPosition> sourceRoads,
        IReadOnlyList<GridPosition> targetRoads)
    {
        var candidates = sourceRoads
            .SelectMany(source => targetRoads.Select(target => new
            {
                Source = source,
                Target = target,
                Distance = DistanceSquared(source, target)
            }))
            .OrderBy(candidate => candidate.Distance)
            .Take(16);

        foreach (var candidate in candidates)
        {
            var path = _pathfinder.FindPath(grid, candidate.Source, candidate.Target);
            if (path.Found)
            {
                return path.Cells;
            }
        }

        return Array.Empty<GridPosition>();
    }

    private static IEnumerable<MapDistrictGridArea> CandidateAreas(
        MapGrid grid,
        int newDistrictId,
        string name,
        MapNewDistrictOptions options,
        IReadOnlyList<GridPosition> sourceRoads)
    {
        var maxX = grid.WidthMeters - options.WidthMeters - 1;
        var maxY = grid.HeightMeters - options.HeightMeters - 1;
        if (maxX < 0 || maxY < 0) yield break;

        var candidates = new List<MapDistrictGridArea>();
        for (var y = 0; y <= maxY; y += options.CandidateStepMeters)
        {
            for (var x = 0; x <= maxX; x += options.CandidateStepMeters)
            {
                var area = new MapDistrictGridArea(newDistrictId, name, new GridPosition(x, y), options.WidthMeters, options.HeightMeters);
                if (CanClaimArea(grid, area))
                {
                    candidates.Add(area);
                }
            }
        }

        foreach (var area in candidates.OrderBy(area => sourceRoads.Min(source => DistanceSquared(source, area.Center))))
        {
            yield return area;
        }
    }

    private static IEnumerable<GridPosition> BuildLocalRoadCells(MapDistrictGridArea area)
    {
        var horizontalY = area.Center.Y;
        for (var x = area.MinX + 2; x <= area.MaxX - 2; x++)
        {
            yield return new GridPosition(x, horizontalY);
        }

        var verticalX = area.Center.X;
        for (var y = area.MinY + 2; y <= area.MaxY - 2; y++)
        {
            yield return new GridPosition(verticalX, y);
        }
    }

    private static bool BuildLocalRoads(
        MapGrid grid,
        MapDistrictGridArea area,
        IEnumerable<GridPosition> roadCells,
        int widthMeters)
    {
        try
        {
            foreach (var position in roadCells.Distinct())
            {
                grid.SetRoad(position, RoadKind.LocalRoad, widthMeters, area.DistrictId);
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<GridPosition> GetDistrictRoadCells(MapGrid grid, int districtId)
    {
        return grid.Cells
            .Where(cell => cell.HasRoad && (cell.DistrictId == districtId || cell.RoadDistrictId == districtId))
            .Select(cell => cell.Position);
    }

    private static void ClaimDistrictArea(MapGrid grid, MapDistrictGridArea area)
    {
        ForEachAreaCell(area, position => grid.GetCell(position).DistrictId = area.DistrictId);
    }

    private static void ClearDistrictArea(MapGrid grid, MapDistrictGridArea area)
    {
        ForEachAreaCell(area, position =>
        {
            var cell = grid.GetCell(position);
            if (cell.DistrictId == area.DistrictId)
            {
                cell.DistrictId = null;
            }
        });
    }

    private static bool CanClaimArea(MapGrid grid, MapDistrictGridArea area)
    {
        var canClaim = true;
        ForEachAreaCell(area, position =>
        {
            if (!canClaim) return;
            if (!grid.TryGetCell(position, out var cell) || cell == null)
            {
                canClaim = false;
                return;
            }

            canClaim = !cell.DistrictId.HasValue &&
                !cell.IsWater &&
                !cell.IsBlocked &&
                !cell.HasInfrastructure &&
                !cell.HasObject;
        });

        return canClaim;
    }

    private static void ForEachAreaCell(MapDistrictGridArea area, Action<GridPosition> action)
    {
        for (var y = area.MinY; y <= area.MaxY; y++)
        {
            for (var x = area.MinX; x <= area.MaxX; x++)
            {
                action(new GridPosition(x, y));
            }
        }
    }

    private static int DistanceSquared(GridPosition a, GridPosition b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static void ValidateOptions(MapNewDistrictOptions options)
    {
        if (options.WidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(options.WidthMeters));
        if (options.HeightMeters <= 0) throw new ArgumentOutOfRangeException(nameof(options.HeightMeters));
        if (options.CandidateStepMeters <= 0) throw new ArgumentOutOfRangeException(nameof(options.CandidateStepMeters));
        if (options.LocalRoadWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(options.LocalRoadWidthMeters));
        if (options.RegionalRoadWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(options.RegionalRoadWidthMeters));
        if (options.ExpansionProbeDepthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(options.ExpansionProbeDepthMeters));
    }
}
