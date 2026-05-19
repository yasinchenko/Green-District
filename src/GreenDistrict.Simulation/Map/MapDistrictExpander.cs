using System.Linq;

namespace GreenDistrict.Simulation.Map;

public sealed record MapDistrictExpansionResult(
    int DistrictId,
    MapEdgeDirection Direction,
    int AddedCells,
    bool RoadNetworkUpdated,
    MapFreeSpaceIndex FreeSpace,
    MapDistrictBoundary Boundary,
    MapDistrictExpansionSpace ExpansionSpace,
    MapDistrictCoverageReport Coverage);

public sealed class MapDistrictExpander
{
    private readonly RoadPathfinder _pathfinder = new();

    public bool TryExpandDistrict(
        MapGrid grid,
        int districtId,
        MapEdgeDirection direction,
        int depthMeters,
        int population,
        out MapDistrictExpansionResult? result)
    {
        if (grid == null) throw new System.ArgumentNullException(nameof(grid));
        if (depthMeters <= 0) throw new System.ArgumentOutOfRangeException(nameof(depthMeters));

        var boundary = MapDistrictBoundary.Build(grid, districtId);
        var expansionSpace = MapDistrictExpansionSpace.Build(grid, boundary, depthMeters);
        var cellsToClaim = expansionSpace[direction];
        if (cellsToClaim.Count == 0)
        {
            result = null;
            return false;
        }

        foreach (var position in cellsToClaim)
        {
            grid.GetCell(position).DistrictId = districtId;
        }

        var roadNetworkUpdated = TryBuildExpansionRoad(grid, districtId, cellsToClaim);
        var updatedBoundary = MapDistrictBoundary.Build(grid, districtId);
        result = new MapDistrictExpansionResult(
            districtId,
            direction,
            cellsToClaim.Count,
            roadNetworkUpdated,
            MapFreeSpaceIndex.Build(grid),
            updatedBoundary,
            MapDistrictExpansionSpace.Build(grid, updatedBoundary, depthMeters),
            new MapCoverageAnalyzer().AnalyzeDistrict(grid, districtId, population));
        return true;
    }

    public bool TryExpandDistrict(
        MapGrid grid,
        int districtId,
        MapEdgeDirection direction,
        int depthMeters,
        out MapDistrictExpansionResult? result)
    {
        return TryExpandDistrict(grid, districtId, direction, depthMeters, population: 0, out result);
    }

    private bool TryBuildExpansionRoad(
        MapGrid grid,
        int districtId,
        System.Collections.Generic.IReadOnlyList<GridPosition> expandedCells)
    {
        var roadStarts = grid.Cells
            .Where(cell => cell.HasRoad && (cell.DistrictId == districtId || cell.RoadDistrictId == districtId))
            .Select(cell => cell.Position)
            .ToList();
        if (roadStarts.Count == 0 || expandedCells.Count == 0) return false;

        var target = expandedCells
            .Where(position =>
                grid.TryGetCell(position, out var cell) &&
                cell is { IsWater: false, IsBlocked: false, HasObject: false, HasRoad: false })
            .OrderBy(position => expandedCells.Sum(cell => DistanceSquared(position, cell)))
            .FirstOrDefault();
        if (!grid.TryGetCell(target, out var targetCell) || targetCell is not { HasRoad: false }) return false;

        foreach (var start in roadStarts.OrderBy(start => DistanceSquared(start, target)).Take(8))
        {
            var path = _pathfinder.FindPath(grid, start, target, new RoadPathOptions(AllowBridges: false));
            if (!path.Found) continue;

            var districtPath = path.Cells
                .Where(position =>
                    grid.TryGetCell(position, out var cell) &&
                    cell != null &&
                    (cell.DistrictId == districtId || cell.RoadDistrictId == districtId))
                .ToList();
            if (districtPath.Count == 0) continue;
            if (grid.TryBuildRoadPath(districtPath, RoadKind.LocalRoad, widthMeters: 6, districtId))
            {
                return true;
            }
        }

        return false;
    }

    private static int DistanceSquared(GridPosition a, GridPosition b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
