using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public enum MapCoverageKind
{
    Trade,
    Healthcare,
    Education,
    Safety,
    Recreation,
    ResourceProduction
}

public sealed record MapCoverageProfile(
    MapCoverageKind Kind,
    int RadiusMeters,
    int WalkAccessMeters,
    bool CoversPopulation);

public sealed record MapObjectCoverage(
    string ObjectId,
    string AssetKey,
    MapCoverageKind Kind,
    IReadOnlySet<GridPosition> CoveredCells,
    int CoveredCellCount,
    float CoveragePercent,
    int CoveredPopulationEstimate);

public sealed record MapCoverageSummary(
    MapCoverageKind Kind,
    int CoveredCells,
    int TotalCoverableCells,
    float CoveragePercent,
    int CoveredPopulationEstimate,
    int UncoveredPopulationEstimate);

public sealed record MapDistrictCoverageReport(
    int DistrictId,
    int Population,
    int TotalCoverableCells,
    IReadOnlyList<MapObjectCoverage> Objects,
    IReadOnlyDictionary<MapCoverageKind, MapCoverageSummary> Summaries);

public sealed class MapCoverageProfileCatalog
{
    private readonly List<(string AssetKeyPrefix, MapCoverageProfile Profile)> _profiles;

    public MapCoverageProfileCatalog(IEnumerable<(string AssetKeyPrefix, MapCoverageProfile Profile)> profiles)
    {
        _profiles = profiles.ToList();
    }

    public static MapCoverageProfileCatalog Defaults { get; } = new(new[]
    {
        ("business.shop", new MapCoverageProfile(MapCoverageKind.Trade, RadiusMeters: 90, WalkAccessMeters: 8, CoversPopulation: true)),
        ("service.clinic", new MapCoverageProfile(MapCoverageKind.Healthcare, RadiusMeters: 130, WalkAccessMeters: 8, CoversPopulation: true)),
        ("service.school", new MapCoverageProfile(MapCoverageKind.Education, RadiusMeters: 170, WalkAccessMeters: 8, CoversPopulation: true)),
        ("service.police", new MapCoverageProfile(MapCoverageKind.Safety, RadiusMeters: 150, WalkAccessMeters: 8, CoversPopulation: true)),
        ("park.", new MapCoverageProfile(MapCoverageKind.Recreation, RadiusMeters: 100, WalkAccessMeters: 8, CoversPopulation: true)),
        ("business.farm", new MapCoverageProfile(MapCoverageKind.ResourceProduction, RadiusMeters: 0, WalkAccessMeters: 0, CoversPopulation: false))
    });

    public bool TryGet(string assetKey, out MapCoverageProfile profile)
    {
        var match = _profiles.FirstOrDefault(item =>
            assetKey.StartsWith(item.AssetKeyPrefix, StringComparison.OrdinalIgnoreCase));
        if (match.AssetKeyPrefix != null)
        {
            profile = match.Profile;
            return true;
        }

        profile = default!;
        return false;
    }
}

public sealed class MapCoverageAnalyzer
{
    private readonly MapCoverageProfileCatalog _profiles;

    public MapCoverageAnalyzer(MapCoverageProfileCatalog? profiles = null)
    {
        _profiles = profiles ?? MapCoverageProfileCatalog.Defaults;
    }

    public MapDistrictCoverageReport AnalyzeDistrict(MapGrid grid, int districtId, int population)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));

        var coverableCells = grid.Cells
            .Where(cell => IsCoverableDistrictCell(cell, districtId))
            .Select(cell => cell.Position)
            .ToHashSet();
        var objectCoverage = new List<MapObjectCoverage>();
        var cellsByKind = Enum
            .GetValues<MapCoverageKind>()
            .ToDictionary(kind => kind, _ => new HashSet<GridPosition>());

        foreach (var mapObject in grid.Objects.Values.Where(mapObject => mapObject.DistrictId == districtId))
        {
            if (!_profiles.TryGet(mapObject.AssetKey, out var profile)) continue;

            var coveredCells = profile.CoversPopulation
                ? GetCoveredCells(grid, districtId, mapObject, profile, coverableCells)
                : new HashSet<GridPosition>();
            var coveragePercent = Percent(coveredCells.Count, coverableCells.Count);
            var populationEstimate = EstimatePopulation(population, coveragePercent);
            objectCoverage.Add(new MapObjectCoverage(
                mapObject.Id,
                mapObject.AssetKey,
                profile.Kind,
                coveredCells,
                coveredCells.Count,
                coveragePercent,
                populationEstimate));

            foreach (var cell in coveredCells)
            {
                cellsByKind[profile.Kind].Add(cell);
            }
        }

        var summaries = cellsByKind
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    var coveragePercent = Percent(pair.Value.Count, coverableCells.Count);
                    var coveredPopulation = EstimatePopulation(population, coveragePercent);
                    return new MapCoverageSummary(
                        pair.Key,
                        pair.Value.Count,
                        coverableCells.Count,
                        coveragePercent,
                        coveredPopulation,
                        Math.Max(0, population - coveredPopulation));
                });

        return new MapDistrictCoverageReport(
            districtId,
            population,
            coverableCells.Count,
            objectCoverage.AsReadOnly(),
            new ReadOnlyDictionary<MapCoverageKind, MapCoverageSummary>(summaries));
    }

    private static HashSet<GridPosition> GetCoveredCells(
        MapGrid grid,
        int districtId,
        PlacedMapObject mapObject,
        MapCoverageProfile profile,
        IReadOnlySet<GridPosition> coverableCells)
    {
        var roadDistances = GetReachableRoadDistances(grid, districtId, mapObject, profile.RadiusMeters);
        var result = new HashSet<GridPosition>();
        foreach (var road in roadDistances.Keys)
        {
            for (var y = road.Y - profile.WalkAccessMeters; y <= road.Y + profile.WalkAccessMeters; y++)
            {
                for (var x = road.X - profile.WalkAccessMeters; x <= road.X + profile.WalkAccessMeters; x++)
                {
                    var position = new GridPosition(x, y);
                    if (ManhattanDistance(road, position) <= profile.WalkAccessMeters && coverableCells.Contains(position))
                    {
                        result.Add(position);
                    }
                }
            }
        }

        return result;
    }

    private static Dictionary<GridPosition, int> GetReachableRoadDistances(
        MapGrid grid,
        int districtId,
        PlacedMapObject mapObject,
        int radiusMeters)
    {
        var distances = new Dictionary<GridPosition, int>();
        var queue = new Queue<GridPosition>();
        foreach (var accessCell in grid.GetAccessCells(mapObject))
        {
            if (!grid.TryGetCell(accessCell, out var cell) || cell is not { HasRoad: true }) continue;
            if (!IsRoadUsableForDistrict(cell, districtId)) continue;

            distances[accessCell] = 0;
            queue.Enqueue(accessCell);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDistance = distances[current];
            if (currentDistance >= radiusMeters) continue;

            foreach (var next in Neighbors(current))
            {
                if (!grid.TryGetCell(next, out var nextCell) || nextCell is not { HasRoad: true }) continue;
                if (!IsRoadUsableForDistrict(nextCell, districtId)) continue;

                var nextDistance = currentDistance + 1;
                if (nextDistance > radiusMeters) continue;
                if (distances.TryGetValue(next, out var existingDistance) && existingDistance <= nextDistance) continue;

                distances[next] = nextDistance;
                queue.Enqueue(next);
            }
        }

        return distances;
    }

    private static bool IsCoverableDistrictCell(MapCell cell, int districtId)
    {
        return cell.DistrictId == districtId &&
            !cell.IsWater &&
            !cell.IsBlocked &&
            !cell.HasInfrastructure;
    }

    private static bool IsRoadUsableForDistrict(MapCell cell, int districtId)
    {
        return cell.DistrictId == districtId || cell.RoadDistrictId == districtId || !cell.RoadDistrictId.HasValue;
    }

    private static IEnumerable<GridPosition> Neighbors(GridPosition position)
    {
        yield return position.Offset(0, -1);
        yield return position.Offset(1, 0);
        yield return position.Offset(0, 1);
        yield return position.Offset(-1, 0);
    }

    private static int ManhattanDistance(GridPosition a, GridPosition b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private static float Percent(int value, int total)
    {
        return total <= 0 ? 0f : value / (float)total * 100f;
    }

    private static int EstimatePopulation(int population, float coveragePercent)
    {
        return (int)MathF.Round(population * Math.Clamp(coveragePercent, 0f, 100f) / 100f);
    }
}
