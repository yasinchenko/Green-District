using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Map;

public enum MapDistrictFormationReason
{
    NoExpansionSpace,
    PopulationDensityPressure,
    ResourceProductionSpacePressure,
    TradeCoverageGap,
    HealthcareCoverageGap,
    LogisticsDistancePressure,
    BusinessSpecializationPressure,
    SocialPressure
}

public sealed record MapDistrictFormationOptions(
    float PopulationPerHectareThreshold = 180f,
    int MinimumDistrictFreeCells = 1800,
    int FarmCountPressureThreshold = 2,
    float MaximumUncoveredTradePopulationPercent = 35f,
    float MaximumUncoveredHealthcarePopulationPercent = 45f,
    int MaximumAverageLogisticsDistanceMeters = 90,
    int BusinessSpecializationThreshold = 6,
    float LowSupportThreshold = 45f);

public sealed record MapDistrictFormationAssessment(
    int DistrictId,
    bool ShouldCreateDistrict,
    IReadOnlyList<MapDistrictFormationReason> Reasons);

public sealed class MapDistrictFormationAnalyzer
{
    private readonly MapDistrictFormationOptions _options;

    public MapDistrictFormationAnalyzer(MapDistrictFormationOptions? options = null)
    {
        _options = options ?? new MapDistrictFormationOptions();
    }

    public MapDistrictFormationAssessment Analyze(
        WorldState world,
        MapGridGenerationResult map,
        int districtId)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (map == null) throw new ArgumentNullException(nameof(map));

        var district = world.Districts.FirstOrDefault(district => district.Id == districtId);
        if (district == null)
        {
            return new MapDistrictFormationAssessment(districtId, ShouldCreateDistrict: false, Array.Empty<MapDistrictFormationReason>());
        }

        var reasons = new List<MapDistrictFormationReason>();
        if (!map.ExpansionSpaces.TryGetValue(districtId, out var expansionSpace) || !expansionSpace.CanExpand)
        {
            reasons.Add(MapDistrictFormationReason.NoExpansionSpace);
        }

        if (map.DistrictBoundaries.TryGetValue(districtId, out var boundary))
        {
            var populationPerHectare = boundary.Cells.Count == 0
                ? 0f
                : district.Population / (boundary.Cells.Count / 10_000f);
            if (populationPerHectare >= _options.PopulationPerHectareThreshold)
            {
                reasons.Add(MapDistrictFormationReason.PopulationDensityPressure);
            }
        }

        var freeDistrictCells = map.FreeSpace[MapSpaceCategory.DistrictFreeLand]
            .Count(position => map.Grid.GetCell(position).DistrictId == districtId);
        var activeBusinesses = world.Businesses
            .Where(business => business.DistrictId == districtId && business.Status == BusinessStatus.Active)
            .ToList();
        var farmCount = activeBusinesses.Count(IsFarmBusiness);
        if (farmCount >= _options.FarmCountPressureThreshold && freeDistrictCells < _options.MinimumDistrictFreeCells)
        {
            reasons.Add(MapDistrictFormationReason.ResourceProductionSpacePressure);
        }

        if (activeBusinesses.Count >= _options.BusinessSpecializationThreshold && freeDistrictCells < _options.MinimumDistrictFreeCells)
        {
            reasons.Add(MapDistrictFormationReason.BusinessSpecializationPressure);
        }

        var coverage = new MapCoverageAnalyzer().AnalyzeDistrict(map.Grid, districtId, district.Population);
        AddCoverageReason(
            coverage,
            MapCoverageKind.Trade,
            _options.MaximumUncoveredTradePopulationPercent,
            MapDistrictFormationReason.TradeCoverageGap,
            reasons);
        AddCoverageReason(
            coverage,
            MapCoverageKind.Healthcare,
            _options.MaximumUncoveredHealthcarePopulationPercent,
            MapDistrictFormationReason.HealthcareCoverageGap,
            reasons);

        var averageLogisticsDistance = CalculateAverageLogisticsDistance(map.Grid, districtId);
        if (averageLogisticsDistance > _options.MaximumAverageLogisticsDistanceMeters)
        {
            reasons.Add(MapDistrictFormationReason.LogisticsDistancePressure);
        }

        if (district.SupportRating < _options.LowSupportThreshold || district.HasActiveCrisis)
        {
            reasons.Add(MapDistrictFormationReason.SocialPressure);
        }

        return new MapDistrictFormationAssessment(
            districtId,
            ShouldCreateDistrict: reasons.Contains(MapDistrictFormationReason.NoExpansionSpace) && reasons.Count > 1,
            new ReadOnlyCollection<MapDistrictFormationReason>(reasons.Distinct().ToList()));
    }

    private static void AddCoverageReason(
        MapDistrictCoverageReport coverage,
        MapCoverageKind kind,
        float maxUncoveredPercent,
        MapDistrictFormationReason reason,
        List<MapDistrictFormationReason> reasons)
    {
        if (coverage.Population <= 0) return;
        if (!coverage.Summaries.TryGetValue(kind, out var summary))
        {
            reasons.Add(reason);
            return;
        }

        var uncoveredPercent = summary.UncoveredPopulationEstimate / (float)coverage.Population * 100f;
        if (uncoveredPercent > maxUncoveredPercent)
        {
            reasons.Add(reason);
        }
    }

    private static bool IsFarmBusiness(Business business)
    {
        return business.Type.Contains("farm", StringComparison.OrdinalIgnoreCase) ||
            business.ProductionType.Contains("food", StringComparison.OrdinalIgnoreCase);
    }

    private static float CalculateAverageLogisticsDistance(MapGrid grid, int districtId)
    {
        var objects = grid.Objects.Values
            .Where(mapObject =>
                mapObject.DistrictId == districtId &&
                mapObject.Type is PlacedMapObjectType.Business or PlacedMapObjectType.Service or PlacedMapObjectType.GovernmentProject)
            .ToList();
        if (objects.Count == 0) return 0f;

        var regionalRoads = grid.Cells
            .Where(cell => cell.RoadKind == RoadKind.RegionalRoad)
            .Select(cell => cell.Position)
            .ToHashSet();
        if (regionalRoads.Count == 0) return float.PositiveInfinity;

        var distances = new List<int>();
        foreach (var mapObject in objects)
        {
            var distance = FindNearestRegionalRoadDistance(grid, districtId, mapObject, regionalRoads);
            if (distance.HasValue)
            {
                distances.Add(distance.Value);
            }
        }

        if (distances.Count == 0) return float.PositiveInfinity;
        return (float)distances.Average();
    }

    private static int? FindNearestRegionalRoadDistance(
        MapGrid grid,
        int districtId,
        PlacedMapObject mapObject,
        IReadOnlySet<GridPosition> regionalRoads)
    {
        var queue = new Queue<GridPosition>();
        var distances = new Dictionary<GridPosition, int>();
        foreach (var accessCell in grid.GetAccessCells(mapObject))
        {
            if (!grid.TryGetCell(accessCell, out var cell) || cell is not { HasRoad: true }) continue;
            if (!IsRoadUsableForDistrict(cell, districtId)) continue;

            queue.Enqueue(accessCell);
            distances[accessCell] = 0;
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDistance = distances[current];
            if (regionalRoads.Contains(current))
            {
                return currentDistance;
            }

            foreach (var next in Neighbors(current))
            {
                if (distances.ContainsKey(next)) continue;
                if (!grid.TryGetCell(next, out var nextCell) || nextCell is not { HasRoad: true }) continue;
                if (!IsRoadUsableForDistrict(nextCell, districtId)) continue;

                distances[next] = currentDistance + 1;
                queue.Enqueue(next);
            }
        }

        return null;
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
}
