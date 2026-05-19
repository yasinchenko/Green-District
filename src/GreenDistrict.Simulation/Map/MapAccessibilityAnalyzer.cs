using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Map;

public sealed record MapEntityAccessibility(
    MapObjectEntityKind EntityKind,
    int EntityId,
    string ObjectId,
    int? DistrictId,
    bool HasRoadAccess,
    float CoveragePercent);

public sealed record MapDistrictAccessibility(
    int DistrictId,
    IReadOnlyDictionary<MapCoverageKind, MapCoverageSummary> CoverageSummaries);

public sealed class MapAccessibilityReport
{
    private readonly IReadOnlyDictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility> _entities;
    private readonly IReadOnlyDictionary<int, MapDistrictAccessibility> _districts;

    public MapAccessibilityReport(
        IReadOnlyDictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility> entities,
        IReadOnlyDictionary<int, MapDistrictAccessibility> districts)
    {
        _entities = entities;
        _districts = districts;
    }

    public IReadOnlyDictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility> Entities => _entities;
    public IReadOnlyDictionary<int, MapDistrictAccessibility> Districts => _districts;

    public bool IsEntityAccessible(MapObjectEntityKind kind, int entityId)
    {
        return !_entities.TryGetValue((kind, entityId), out var entity) || entity.HasRoadAccess;
    }

    public bool IsBusinessAccessible(int businessId)
    {
        return IsEntityAccessible(MapObjectEntityKind.Business, businessId);
    }

    public float GetDistrictCoveragePercent(int? districtId, MapCoverageKind kind)
    {
        if (!districtId.HasValue) return 100f;
        if (!_districts.TryGetValue(districtId.Value, out var district)) return 0f;
        return district.CoverageSummaries.TryGetValue(kind, out var summary) ? summary.CoveragePercent : 0f;
    }
}

public sealed class MapAccessibilityAnalyzer
{
    private readonly MapCoverageAnalyzer _coverageAnalyzer;

    public MapAccessibilityAnalyzer(MapCoverageAnalyzer? coverageAnalyzer = null)
    {
        _coverageAnalyzer = coverageAnalyzer ?? new MapCoverageAnalyzer();
    }

    public MapAccessibilityReport Analyze(WorldState world, MapGridGenerationResult map)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (map == null) throw new ArgumentNullException(nameof(map));

        var districtReports = new Dictionary<int, MapDistrictCoverageReport>();
        foreach (var district in world.Districts)
        {
            districtReports[district.Id] = _coverageAnalyzer.AnalyzeDistrict(map.Grid, district.Id, district.Population);
        }

        var objectCoverageById = districtReports.Values
            .SelectMany(report => report.Objects)
            .GroupBy(coverage => coverage.ObjectId)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var entities = new Dictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility>();
        foreach (var mapObject in map.Grid.Objects.Values.Where(mapObject => mapObject.EntityId.HasValue))
        {
            objectCoverageById.TryGetValue(mapObject.Id, out var coverage);
            var entity = new MapEntityAccessibility(
                mapObject.EntityKind,
                mapObject.EntityId!.Value,
                mapObject.Id,
                mapObject.DistrictId,
                map.Grid.HasRoadAccess(mapObject),
                coverage?.CoveragePercent ?? 0f);
            entities[(entity.EntityKind, entity.EntityId)] = entity;
        }

        var districts = districtReports.ToDictionary(
            pair => pair.Key,
            pair => new MapDistrictAccessibility(
                pair.Key,
                new ReadOnlyDictionary<MapCoverageKind, MapCoverageSummary>(pair.Value.Summaries.ToDictionary(
                    summary => summary.Key,
                    summary => summary.Value))));

        return new MapAccessibilityReport(
            new ReadOnlyDictionary<(MapObjectEntityKind Kind, int Id), MapEntityAccessibility>(entities),
            new ReadOnlyDictionary<int, MapDistrictAccessibility>(districts));
    }
}
