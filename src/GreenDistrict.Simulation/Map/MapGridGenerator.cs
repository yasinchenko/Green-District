using System;
using System.Collections.Generic;
using System.Linq;
using GreenDistrict.Simulation.Core;

namespace GreenDistrict.Simulation.Map;

public sealed record MapGridGenerationOptions(
    int WidthMeters = 240,
    int HeightMeters = 160,
    int PaddingMeters = 8,
    int DistrictGapMeters = 8,
    int LocalRoadWidthMeters = 6,
    int RegionalRoadWidthMeters = 8,
    int AccessRoadWidthMeters = 4,
    int ExpansionProbeDepthMeters = 8,
    int MaxWidthMeters = 480,
    int MaxHeightMeters = 320)
{
    public int AreaMeters => WidthMeters * HeightMeters;
    public int MaxAreaMeters => MaxWidthMeters * MaxHeightMeters;

    public void Validate()
    {
        if (WidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(WidthMeters));
        if (HeightMeters <= 0) throw new ArgumentOutOfRangeException(nameof(HeightMeters));
        if (MaxWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(MaxWidthMeters));
        if (MaxHeightMeters <= 0) throw new ArgumentOutOfRangeException(nameof(MaxHeightMeters));
        if (PaddingMeters < 0) throw new ArgumentOutOfRangeException(nameof(PaddingMeters));
        if (DistrictGapMeters < 0) throw new ArgumentOutOfRangeException(nameof(DistrictGapMeters));
        if (LocalRoadWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(LocalRoadWidthMeters));
        if (RegionalRoadWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(RegionalRoadWidthMeters));
        if (AccessRoadWidthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(AccessRoadWidthMeters));
        if (ExpansionProbeDepthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(ExpansionProbeDepthMeters));

        if (WidthMeters > MaxWidthMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(WidthMeters), "Map width cannot exceed the configured maximum width.");
        }

        if (HeightMeters > MaxHeightMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(HeightMeters), "Map height cannot exceed the configured maximum height.");
        }
    }
}

public sealed record MapDistrictGridArea(
    int DistrictId,
    string Name,
    GridPosition Origin,
    int WidthMeters,
    int HeightMeters)
{
    public GridPosition Center => new(Origin.X + WidthMeters / 2, Origin.Y + HeightMeters / 2);
    public int MinX => Origin.X;
    public int MinY => Origin.Y;
    public int MaxX => Origin.X + WidthMeters - 1;
    public int MaxY => Origin.Y + HeightMeters - 1;

    public bool Contains(GridPosition position)
    {
        return position.X >= MinX && position.Y >= MinY && position.X <= MaxX && position.Y <= MaxY;
    }
}

public sealed record MapGridGenerationResult(
    MapGrid Grid,
    IReadOnlyDictionary<int, MapDistrictGridArea> DistrictAreas,
    MapFreeSpaceIndex FreeSpace,
    IReadOnlyDictionary<int, MapDistrictBoundary> DistrictBoundaries,
    IReadOnlyDictionary<int, MapDistrictExpansionSpace> ExpansionSpaces);

public sealed class MapGridGenerator
{
    private const float TargetWaterShare = 0.20f;

    private readonly RoadPathfinder _pathfinder = new();
    private readonly MapObjectSizeCatalog _sizeCatalog;

    public MapGridGenerator(MapObjectSizeCatalog? sizeCatalog = null)
    {
        _sizeCatalog = sizeCatalog ?? MapObjectSizeCatalog.LoadConfiguredOrDefaults();
    }

    public MapGridGenerationResult Generate(WorldState world, MapGridGenerationOptions? options = null)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));

        options ??= new MapGridGenerationOptions();
        options.Validate();
        var grid = new MapGrid(options.WidthMeters, options.HeightMeters);
        ApplyBaseTerrain(grid, world.SimulationSeed);

        var areas = LayoutDistricts(grid, world.Districts.OrderBy(district => district.Id).ToList(), options, world.SimulationSeed)
            .ToDictionary(area => area.DistrictId);

        foreach (var area in areas.Values)
        {
            MarkDistrictCells(grid, area);
        }

        EnsureTargetWaterShare(grid, world.SimulationSeed);
        PlaceWorldObjects(grid, world, areas, options);
        BuildRegionalRoads(grid, areas.Values.OrderBy(area => area.Center.X).ThenBy(area => area.Center.Y).ToList(), options);

        var boundaries = areas.Keys.ToDictionary(districtId => districtId, districtId => MapDistrictBoundary.Build(grid, districtId));
        var expansionSpaces = boundaries.ToDictionary(
            pair => pair.Key,
            pair => MapDistrictExpansionSpace.Build(grid, pair.Value, options.ExpansionProbeDepthMeters));

        return new MapGridGenerationResult(grid, areas, MapFreeSpaceIndex.Build(grid), boundaries, expansionSpaces);
    }

    private static void ApplyBaseTerrain(MapGrid grid, int seed)
    {
        var terrainSeed = seed == 0 ? 17_231 : StableHash(seed, 17_231, 73);
        var startY = Math.Max(1, (int)MathF.Round(grid.HeightMeters * StableRange(terrainSeed, 11, 0.04f, 0.12f)));
        var endY = Math.Min(grid.HeightMeters - 2, (int)MathF.Round(grid.HeightMeters * StableRange(terrainSeed, 23, 0.88f, 0.98f)));
        if (endY <= startY) return;

        var centerRatio = StableRange(terrainSeed, 37, 0.66f, 0.84f);
        var primaryPhase = StableRange(terrainSeed, 41, 0f, MathF.PI * 2f);
        var secondaryPhase = StableRange(terrainSeed, 43, 0f, MathF.PI * 2f);
        var widthPhase = StableRange(terrainSeed, 47, 0f, MathF.PI * 2f);
        var riverLean = StableRange(terrainSeed, 53, -0.10f, 0.10f);
        var waterTarget = Math.Clamp(
            (int)MathF.Round(grid.WidthMeters * grid.HeightMeters * TargetWaterShare),
            1,
            grid.WidthMeters * grid.HeightMeters);
        var riverLength = Math.Max(1, endY - startY + 1);
        var targetHalfWidth = Math.Max(8f, waterTarget / (riverLength * 2f));
        var candidates = new List<(GridPosition Position, float Score)>(grid.WidthMeters * riverLength);

        for (var y = 0; y < grid.HeightMeters; y++)
        {
            if (y < startY || y > endY) continue;

            var t = (y - startY) / (float)Math.Max(1, endY - startY);
            var taper = MathF.Sin(MathF.PI * t);
            var rowNoise = StableTerrainNoise(terrainSeed, y, 0) - 0.5f;
            var center = grid.WidthMeters * (centerRatio + (t - 0.5f) * riverLean)
                + MathF.Sin(t * MathF.PI * 2.2f + primaryPhase) * grid.WidthMeters * 0.075f
                + MathF.Sin(t * MathF.PI * 5.1f + secondaryPhase) * grid.WidthMeters * 0.035f
                + rowNoise * grid.WidthMeters * 0.018f;
            var halfWidth = targetHalfWidth * (0.62f + taper * 0.76f)
                + MathF.Sin(t * MathF.PI * 7.0f + widthPhase) * grid.WidthMeters * 0.018f
                + rowNoise * grid.WidthMeters * 0.011f;

            for (var x = 0; x < grid.WidthMeters; x++)
            {
                var bankNoise = StableTerrainNoise(terrainSeed, x, y) - 0.5f;
                var widthNoise = StableTerrainNoise(terrainSeed, x + 31, y + 17) - 0.5f;
                var distance = MathF.Abs(x - center) / Math.Max(1f, halfWidth);
                var score = distance + bankNoise * 0.30f + widthNoise * 0.18f;
                candidates.Add((new GridPosition(x, y), score));
            }
        }

        foreach (var candidate in candidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => StableHash(terrainSeed, candidate.Position.X, candidate.Position.Y))
            .Take(waterTarget))
        {
            grid.SetSurface(candidate.Position, MapSurfaceType.Water);
        }
    }

    private static float StableTerrainNoise(int seed, int x, int y)
    {
        return StableHash(seed, x, y) / (float)int.MaxValue;
    }

    private static float StableRange(int seed, int salt, float min, float max)
    {
        return min + (max - min) * StableTerrainNoise(seed, salt, seed ^ salt);
    }

    private static IReadOnlyList<MapDistrictGridArea> LayoutDistricts(
        MapGrid grid,
        IReadOnlyList<District> districts,
        MapGridGenerationOptions options,
        int seed)
    {
        if (districts.Count == 0) return Array.Empty<MapDistrictGridArea>();
        if (seed != 0)
        {
            var seeded = TryLayoutDistrictsFromSeed(grid, districts, options, seed);
            if (seeded != null) return seeded;
        }

        return LayoutDistrictsRegular(districts, options);
    }

    private static IReadOnlyList<MapDistrictGridArea> LayoutDistrictsRegular(
        IReadOnlyList<District> districts,
        MapGridGenerationOptions options)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(districts.Count)));
        var rows = Math.Max(1, (int)Math.Ceiling(districts.Count / (float)columns));
        var (cellWidth, cellHeight) = CalculateDistrictAreaSize(districts.Count, options);
        var availableWidth = options.WidthMeters - options.PaddingMeters * 2;
        var availableHeight = options.HeightMeters - options.PaddingMeters * 2;
        var strideX = columns <= 1 ? 0 : (availableWidth - cellWidth) / (columns - 1);
        var strideY = rows <= 1 ? 0 : (availableHeight - cellHeight) / (rows - 1);

        var result = new List<MapDistrictGridArea>();
        for (var i = 0; i < districts.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var origin = new GridPosition(
                options.PaddingMeters + column * Math.Max(cellWidth + options.DistrictGapMeters, strideX),
                options.PaddingMeters + row * Math.Max(cellHeight + options.DistrictGapMeters, strideY));

            result.Add(new MapDistrictGridArea(districts[i].Id, districts[i].Name, origin, cellWidth, cellHeight));
        }

        return result;
    }

    private static IReadOnlyList<MapDistrictGridArea>? TryLayoutDistrictsFromSeed(
        MapGrid grid,
        IReadOnlyList<District> districts,
        MapGridGenerationOptions options,
        int seed)
    {
        var (areaWidth, areaHeight) = CalculateDistrictAreaSize(districts.Count, options);
        var maxX = grid.WidthMeters - areaWidth - options.PaddingMeters;
        var maxY = grid.HeightMeters - areaHeight - options.PaddingMeters;
        if (maxX < options.PaddingMeters || maxY < options.PaddingMeters) return null;

        var layoutSeed = StableHash(seed, 29_711, districts.Count);
        var step = Math.Max(4, Math.Min(areaWidth, areaHeight) / 6);
        var candidateLists = districts
            .Select(district => CandidateDistrictAreas(district, areaWidth, areaHeight, options, maxX, maxY, step)
                .OrderBy(area => StableHash(layoutSeed, area.Origin.X + district.Id * 101, area.Origin.Y + district.Id * 503))
                .Take(80)
                .ToList())
            .ToList();

        if (candidateLists.Any(candidates => candidates.Count == 0)) return null;

        var selected = new List<MapDistrictGridArea>();
        return TrySelectDistrictAreas(candidateLists, 0, options.DistrictGapMeters, selected)
            ? selected
            : null;
    }

    private static (int Width, int Height) CalculateDistrictAreaSize(int districtCount, MapGridGenerationOptions options)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(districtCount)));
        var rows = Math.Max(1, (int)Math.Ceiling(districtCount / (float)columns));
        var availableWidth = options.WidthMeters - options.PaddingMeters * 2 - options.DistrictGapMeters * (columns - 1);
        var availableHeight = options.HeightMeters - options.PaddingMeters * 2 - options.DistrictGapMeters * (rows - 1);
        var width = Math.Max(42, (int)MathF.Floor(availableWidth / (columns * 1.35f)));
        var height = Math.Max(42, (int)MathF.Floor(availableHeight / (rows * 1.20f)));
        return (Math.Min(availableWidth, width), Math.Min(availableHeight, height));
    }

    private static IEnumerable<MapDistrictGridArea> CandidateDistrictAreas(
        District district,
        int widthMeters,
        int heightMeters,
        MapGridGenerationOptions options,
        int maxX,
        int maxY,
        int stepMeters)
    {
        for (var y = options.PaddingMeters; y <= maxY; y += stepMeters)
        {
            for (var x = options.PaddingMeters; x <= maxX; x += stepMeters)
            {
                yield return new MapDistrictGridArea(district.Id, district.Name, new GridPosition(x, y), widthMeters, heightMeters);
            }
        }

        yield return new MapDistrictGridArea(district.Id, district.Name, new GridPosition(maxX, maxY), widthMeters, heightMeters);
    }

    private static bool DistrictAreasIntersectWithGap(MapDistrictGridArea first, MapDistrictGridArea second, int gapMeters)
    {
        return first.MinX - gapMeters <= second.MaxX &&
            first.MaxX + gapMeters >= second.MinX &&
            first.MinY - gapMeters <= second.MaxY &&
            first.MaxY + gapMeters >= second.MinY;
    }

    private static bool TrySelectDistrictAreas(
        IReadOnlyList<IReadOnlyList<MapDistrictGridArea>> candidateLists,
        int index,
        int gapMeters,
        List<MapDistrictGridArea> selected)
    {
        if (index >= candidateLists.Count) return true;

        foreach (var candidate in candidateLists[index])
        {
            if (selected.Any(existing => DistrictAreasIntersectWithGap(existing, candidate, gapMeters))) continue;

            selected.Add(candidate);
            if (TrySelectDistrictAreas(candidateLists, index + 1, gapMeters, selected))
            {
                return true;
            }

            selected.RemoveAt(selected.Count - 1);
        }

        return false;
    }

    private static void MarkDistrictCells(MapGrid grid, MapDistrictGridArea area)
    {
        for (var y = area.MinY; y <= area.MaxY; y++)
        {
            for (var x = area.MinX; x <= area.MaxX; x++)
            {
                var position = new GridPosition(x, y);
                if (!grid.TryGetCell(position, out var cell) || cell == null) continue;
                cell.DistrictId = area.DistrictId;
                if (cell.IsWater)
                {
                    grid.SetSurface(position, MapSurfaceType.Land);
                }
            }
        }
    }

    private static void EnsureTargetWaterShare(MapGrid grid, int seed)
    {
        var target = Math.Clamp(
            (int)MathF.Round(grid.WidthMeters * grid.HeightMeters * TargetWaterShare),
            1,
            grid.WidthMeters * grid.HeightMeters);
        var current = grid.Cells.Count(cell => cell.Surface == MapSurfaceType.Water);
        if (current >= target) return;

        var terrainSeed = seed == 0 ? 17_231 : StableHash(seed, 17_231, 73);
        var candidates = grid.Cells
            .Where(cell => !cell.DistrictId.HasValue && cell.Surface == MapSurfaceType.Land)
            .Select(cell => new
            {
                cell.Position,
                Score = (HasAdjacentWater(grid, cell.Position) ? 0f : 0.65f) +
                    StableTerrainNoise(terrainSeed, cell.Position.X + 97, cell.Position.Y + 193) * 0.35f
            })
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => StableHash(terrainSeed, candidate.Position.X, candidate.Position.Y))
            .Take(target - current)
            .ToList();

        foreach (var candidate in candidates)
        {
            grid.SetSurface(candidate.Position, MapSurfaceType.Water);
        }
    }

    private static bool HasAdjacentWater(MapGrid grid, GridPosition position)
    {
        return grid.TryGetCell(position.Offset(0, -1), out var north) && north is { IsWater: true } ||
            grid.TryGetCell(position.Offset(1, 0), out var east) && east is { IsWater: true } ||
            grid.TryGetCell(position.Offset(0, 1), out var south) && south is { IsWater: true } ||
            grid.TryGetCell(position.Offset(-1, 0), out var west) && west is { IsWater: true };
    }

    private void BuildRegionalRoads(
        MapGrid grid,
        IReadOnlyList<MapDistrictGridArea> areas,
        MapGridGenerationOptions options)
    {
        for (var i = 0; i < areas.Count - 1; i++)
        {
            var from = FindRoadGateway(grid, areas[i], areas[i + 1].Center) ?? areas[i].Center;
            var to = FindRoadGateway(grid, areas[i + 1], areas[i].Center) ?? areas[i + 1].Center;
            TryBuildPath(grid, from, to, RoadKind.RegionalRoad, options.RegionalRoadWidthMeters, districtId: null, area: null);
        }
    }

    private void PlaceWorldObjects(
        MapGrid grid,
        WorldState world,
        IReadOnlyDictionary<int, MapDistrictGridArea> areas,
        MapGridGenerationOptions options)
    {
        var worldSeed = world.SimulationSeed;
        foreach (var business in world.Businesses
            .Where(business => business.Status == BusinessStatus.Active && business.DistrictId.HasValue)
            .OrderBy(business => business.Id))
        {
            if (!business.DistrictId.HasValue || !areas.TryGetValue(business.DistrictId.Value, out var area)) continue;

            var objectKey = BusinessObjectKey(business);
            var mapObject = CreateObject(
                $"business:{business.Id}",
                objectKey,
                area.DistrictId,
                area.Origin,
                MapObjectEntityKind.Business,
                business.Id);
            TryPlaceAccessibleObject(grid, mapObject, area, options, StableHash(worldSeed, business.Id, 41));
        }

        foreach (var housing in world.HousingUnits
            .Where(housing => housing.DistrictId.HasValue)
            .OrderBy(housing => housing.Id))
        {
            if (!housing.DistrictId.HasValue || !areas.TryGetValue(housing.DistrictId.Value, out var area)) continue;

            var objectKey = housing.Capacity >= 3 ? "house.medium" : "house.small";
            var mapObject = CreateObject(
                $"housing:{housing.Id}",
                objectKey,
                area.DistrictId,
                area.Origin,
                MapObjectEntityKind.HousingUnit,
                housing.Id);
            TryPlaceAccessibleObject(grid, mapObject, area, options, StableHash(worldSeed, housing.Id, 53));
        }

        foreach (var project in world.Projects
            .Where(project => project.DistrictId.HasValue)
            .OrderBy(project => project.Id))
        {
            if (!project.DistrictId.HasValue || !areas.TryGetValue(project.DistrictId.Value, out var area)) continue;

            var objectKey = ProjectObjectKey(project.Type);
            var mapObject = CreateObject(
                $"project:{project.Id}",
                objectKey,
                area.DistrictId,
                area.Origin,
                MapObjectEntityKind.GovernmentProject,
                project.Id);
            TryPlaceAccessibleObject(grid, mapObject, area, options, StableHash(worldSeed, project.Id, 67));
        }

        foreach (var gameEvent in world.Events
            .Where(gameEvent => !gameEvent.IsResolved)
            .OrderBy(gameEvent => gameEvent.Id))
        {
            if (gameEvent.HasTargetEntity &&
                TryPlaceTargetedEventMarker(grid, gameEvent, worldSeed, areas))
            {
                continue;
            }

            var districtId = EventDistrictId(gameEvent);
            if (!districtId.HasValue || !areas.TryGetValue(districtId.Value, out var area)) continue;

            var mapObject = CreateObject(
                $"event:{gameEvent.Id}",
                EventObjectKey(gameEvent.Type),
                area.DistrictId,
                area.Origin,
                MapObjectEntityKind.GameEvent,
                gameEvent.Id);
            TryPlaceMarkerObject(grid, mapObject, area, StableHash(worldSeed, gameEvent.Id, 79));
        }
    }

    private PlacedMapObject CreateObject(
        string objectId,
        string objectKey,
        int districtId,
        GridPosition position,
        MapObjectEntityKind entityKind,
        int entityId)
    {
        var definition = _sizeCatalog.TryGet(objectKey, out var found)
            ? found!
            : _sizeCatalog.Get("house.small");

        return new PlacedMapObject(
            objectId,
            definition.Type,
            districtId,
            position,
            definition.WidthMeters,
            definition.LengthMeters,
            rotationDegrees: 0,
            definition.AccessSides,
            definition.AssetKey,
            entityKind,
            entityId);
    }

    private bool TryPlaceAccessibleObject(
        MapGrid grid,
        PlacedMapObject template,
        MapDistrictGridArea area,
        MapGridGenerationOptions options,
        int seed)
    {
        foreach (var candidate in CandidateObjectPlacements(grid, template, area, seed))
        {
            if (!grid.TryPlaceObject(candidate)) continue;

            if (grid.HasRoadAccess(candidate) ||
                TryBuildStarterRoad(grid, candidate, area, options) ||
                TryBuildAccessRoad(grid, candidate, area, options))
            {
                return true;
            }

            grid.RemoveObject(candidate.Id);
        }

        return false;
    }

    private bool TryPlaceMarkerObject(
        MapGrid grid,
        PlacedMapObject template,
        MapDistrictGridArea area,
        int seed)
    {
        foreach (var candidate in CandidateObjectPlacements(grid, template, area, seed))
        {
            if (grid.TryPlaceObject(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryPlaceTargetedEventMarker(
        MapGrid grid,
        GameEvent gameEvent,
        int worldSeed,
        IReadOnlyDictionary<int, MapDistrictGridArea> areas)
    {
        if (!gameEvent.TargetEntityId.HasValue) return false;

        var target = grid.Objects.Values.FirstOrDefault(mapObject =>
            mapObject.EntityKind == gameEvent.TargetEntityKind &&
            mapObject.EntityId == gameEvent.TargetEntityId);
        if (target == null || !target.DistrictId.HasValue) return false;
        if (!areas.TryGetValue(target.DistrictId.Value, out var area)) return false;

        var marker = CreateObject(
            $"event:{gameEvent.Id}",
            EventObjectKey(gameEvent.Type),
            area.DistrictId,
            target.Position,
            MapObjectEntityKind.GameEvent,
            gameEvent.Id);

        foreach (var candidate in CandidateMarkerPlacementsNearObject(marker, target, area)
            .OrderBy(candidate => StableHash(worldSeed, gameEvent.Id, candidate.Position.X, candidate.Position.Y)))
        {
            if (grid.TryPlaceObject(candidate))
            {
                return true;
            }
        }

        return TryPlaceMarkerObject(grid, marker, area, StableHash(worldSeed, gameEvent.Id, 89));
    }

    private static IEnumerable<PlacedMapObject> CandidateMarkerPlacementsNearObject(
        PlacedMapObject marker,
        PlacedMapObject target,
        MapDistrictGridArea area)
    {
        var offsets = new[]
        {
            new GridPosition(target.Position.X + target.FootprintWidth, target.Position.Y),
            new GridPosition(target.Position.X - marker.FootprintWidth, target.Position.Y),
            new GridPosition(target.Position.X, target.Position.Y + target.FootprintLength),
            new GridPosition(target.Position.X, target.Position.Y - marker.FootprintLength),
            new GridPosition(target.Position.X + target.FootprintWidth, target.Position.Y + target.FootprintLength),
            new GridPosition(target.Position.X - marker.FootprintWidth, target.Position.Y - marker.FootprintLength)
        };

        foreach (var position in offsets)
        {
            if (!area.Contains(position) ||
                !area.Contains(position.Offset(marker.FootprintWidth - 1, marker.FootprintLength - 1)))
            {
                continue;
            }

            yield return marker.PlaceAt(position, accessSides: MapAccessSide.None);
        }
    }

    private IEnumerable<PlacedMapObject> CandidateObjectPlacements(
        MapGrid grid,
        PlacedMapObject template,
        MapDistrictGridArea area,
        int seed)
    {
        var nearRoads = grid.Cells
            .Where(cell => cell.DistrictId == area.DistrictId && cell.HasRoad && !cell.IsWater)
            .OrderBy(cell => StableHash(seed, cell.Position.X, cell.Position.Y))
            .ToList();

        foreach (var road in nearRoads)
        {
            foreach (var candidate in PlacementsAroundRoad(road.Position, template, area))
            {
                yield return template.PlaceAt(
                    candidate.Position,
                    accessSides: OrientedAccessSides(template.AccessSides, candidate.AccessSide));
            }
        }

        var stepX = Math.Max(3, template.FootprintWidth / 2);
        var stepY = Math.Max(3, template.FootprintLength / 2);
        for (var y = area.MinY + 2; y <= area.MaxY - template.FootprintLength - 1; y += stepY)
        {
            for (var x = area.MinX + 2; x <= area.MaxX - template.FootprintWidth - 1; x += stepX)
            {
                foreach (var accessSide in CandidateAccessSides(template.AccessSides))
                {
                    yield return template.PlaceAt(new GridPosition(x, y), accessSides: accessSide);
                }
            }
        }
    }

    private static IEnumerable<MapAccessSide> CandidateAccessSides(MapAccessSide allowedSides)
    {
        if (allowedSides == MapAccessSide.None)
        {
            yield return MapAccessSide.None;
            yield break;
        }

        var sides = new[] { MapAccessSide.South, MapAccessSide.North, MapAccessSide.East, MapAccessSide.West };
        foreach (var side in sides)
        {
            if (allowedSides.HasFlag(side))
            {
                yield return side;
            }
        }
    }

    private static IEnumerable<(GridPosition Position, MapAccessSide AccessSide)> PlacementsAroundRoad(
        GridPosition road,
        PlacedMapObject template,
        MapDistrictGridArea area)
    {
        var candidates = new[]
        {
            (Position: new GridPosition(road.X - template.FootprintWidth / 2, road.Y - template.FootprintLength), AccessSide: MapAccessSide.South),
            (Position: new GridPosition(road.X - template.FootprintWidth / 2, road.Y + 1), AccessSide: MapAccessSide.North),
            (Position: new GridPosition(road.X - template.FootprintWidth, road.Y - template.FootprintLength / 2), AccessSide: MapAccessSide.East),
            (Position: new GridPosition(road.X + 1, road.Y - template.FootprintLength / 2), AccessSide: MapAccessSide.West)
        };

        foreach (var candidate in candidates)
        {
            if (area.Contains(candidate.Position) &&
                area.Contains(candidate.Position.Offset(template.FootprintWidth - 1, template.FootprintLength - 1)))
            {
                yield return candidate;
            }
        }
    }

    private static MapAccessSide OrientedAccessSides(MapAccessSide allowedSides, MapAccessSide preferredSide)
    {
        if (allowedSides == MapAccessSide.None) return MapAccessSide.None;
        return allowedSides.HasFlag(preferredSide) ? preferredSide : allowedSides;
    }

    private bool TryBuildStarterRoad(
        MapGrid grid,
        PlacedMapObject mapObject,
        MapDistrictGridArea area,
        MapGridGenerationOptions options)
    {
        if (grid.Cells.Any(cell => cell.DistrictId == area.DistrictId && cell.HasRoad && !cell.IsWater))
        {
            return false;
        }

        foreach (var segment in StarterRoadSegments(mapObject, area))
        {
            if (grid.TryBuildRoadPath(segment, RoadKind.LocalRoad, options.LocalRoadWidthMeters, area.DistrictId) &&
                grid.HasRoadAccess(mapObject))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IReadOnlyList<GridPosition>> StarterRoadSegments(
        PlacedMapObject mapObject,
        MapDistrictGridArea area)
    {
        var accessCells = AccessCellsBySide(mapObject)
            .Select(group => new
            {
                group.Side,
                Cells = group.Cells
                    .Where(area.Contains)
                    .OrderBy(position => position.X)
                    .ThenBy(position => position.Y)
                    .ToList()
            })
            .Where(group => group.Cells.Count > 0)
            .OrderByDescending(group => group.Cells.Count)
            .ThenBy(group => group.Side)
            .ToList();

        foreach (var group in accessCells)
        {
            var cells = ContinuousCellsAroundCenter(group.Cells);
            if (cells.Count > 0)
            {
                yield return cells;
            }
        }
    }

    private static IEnumerable<(MapAccessSide Side, IReadOnlyList<GridPosition> Cells)> AccessCellsBySide(PlacedMapObject mapObject)
    {
        var x0 = mapObject.Position.X;
        var y0 = mapObject.Position.Y;
        var x1 = x0 + mapObject.FootprintWidth - 1;
        var y1 = y0 + mapObject.FootprintLength - 1;

        if (mapObject.AccessSides.HasFlag(MapAccessSide.South))
        {
            yield return (
                MapAccessSide.South,
                Enumerable.Range(x0, mapObject.FootprintWidth).Select(x => new GridPosition(x, y1 + 1)).ToList());
        }

        if (mapObject.AccessSides.HasFlag(MapAccessSide.North))
        {
            yield return (
                MapAccessSide.North,
                Enumerable.Range(x0, mapObject.FootprintWidth).Select(x => new GridPosition(x, y0 - 1)).ToList());
        }

        if (mapObject.AccessSides.HasFlag(MapAccessSide.East))
        {
            yield return (
                MapAccessSide.East,
                Enumerable.Range(y0, mapObject.FootprintLength).Select(y => new GridPosition(x1 + 1, y)).ToList());
        }

        if (mapObject.AccessSides.HasFlag(MapAccessSide.West))
        {
            yield return (
                MapAccessSide.West,
                Enumerable.Range(y0, mapObject.FootprintLength).Select(y => new GridPosition(x0 - 1, y)).ToList());
        }
    }

    private static IReadOnlyList<GridPosition> ContinuousCellsAroundCenter(IReadOnlyList<GridPosition> cells)
    {
        if (cells.Count == 0) return Array.Empty<GridPosition>();

        var maxLength = Math.Min(cells.Count, 14);
        var start = Math.Max(0, (cells.Count - maxLength) / 2);
        return cells.Skip(start).Take(maxLength).ToList();
    }

    private bool TryBuildAccessRoad(
        MapGrid grid,
        PlacedMapObject mapObject,
        MapDistrictGridArea area,
        MapGridGenerationOptions options)
    {
        var roadStarts = grid.Cells
            .Where(cell => cell.DistrictId == area.DistrictId && cell.HasRoad && !cell.IsWater)
            .OrderBy(cell => DistanceToObject(cell.Position, mapObject))
            .Take(10)
            .Select(cell => cell.Position)
            .ToList();

        var accessCells = grid.GetAccessCells(mapObject)
            .Where(position =>
                area.Contains(position) &&
                grid.TryGetCell(position, out var cell) &&
                cell is { IsWater: false, IsBlocked: false, HasObject: false })
            .OrderBy(position => roadStarts.Count == 0 ? 0 : roadStarts.Min(start => DistanceSquared(start, position)))
            .ToList();

        foreach (var accessCell in accessCells)
        {
            foreach (var start in roadStarts)
            {
                var result = _pathfinder.FindPath(grid, start, accessCell, new RoadPathOptions());
                if (!result.Found) continue;

                var path = result.Cells.Where(area.Contains).ToList();
                if (grid.TryBuildRoadPath(path, RoadKind.AccessRoad, options.AccessRoadWidthMeters, area.DistrictId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryBuildPath(
        MapGrid grid,
        GridPosition start,
        GridPosition goal,
        RoadKind kind,
        int widthMeters,
        int? districtId,
        MapDistrictGridArea? area)
    {
        var result = _pathfinder.FindPath(grid, start, goal, new RoadPathOptions());
        if (!result.Found) return false;

        var path = area == null ? result.Cells : result.Cells.Where(area.Contains).ToList();
        return grid.TryBuildRoadPath(path, kind, widthMeters, districtId);
    }

    private static GridPosition? FindRoadGateway(MapGrid grid, MapDistrictGridArea area, GridPosition target)
    {
        return grid.Cells
            .Where(cell => cell.DistrictId == area.DistrictId && cell.HasRoad && !cell.IsWater)
            .OrderBy(cell => DistanceSquared(cell.Position, target))
            .Select(cell => (GridPosition?)cell.Position)
            .FirstOrDefault();
    }

    private static int DistanceSquared(GridPosition a, GridPosition b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static int DistanceToObject(GridPosition position, PlacedMapObject mapObject)
    {
        var center = new GridPosition(
            mapObject.Position.X + mapObject.FootprintWidth / 2,
            mapObject.Position.Y + mapObject.FootprintLength / 2);
        return DistanceSquared(position, center);
    }

    private static int StableHash(int seed, int x, int y)
    {
        unchecked
        {
            var value = seed;
            value = value * 397 ^ x;
            value = value * 397 ^ y;
            return value & 0x7fffffff;
        }
    }

    private static int StableHash(int seed, int x, int y, int z)
    {
        return StableHash(StableHash(seed, x, y), z, 97);
    }

    private static string BusinessObjectKey(Business business)
    {
        if (ContainsTerm(business.Type, business.ProductionType, business.Name, "farm", "food")) return "farm";
        if (ContainsTerm(business.Type, business.ProductionType, business.Name, "factory", "workshop", "industry", "goods")) return "workshop";
        if (ContainsTerm(business.Type, business.ProductionType, business.Name, "clinic", "health", "medical")) return "clinic";
        if (ContainsTerm(business.Type, business.ProductionType, business.Name, "shop", "trade", "store")) return "shop";
        return "shop";
    }

    private static string ProjectObjectKey(ProjectType type)
    {
        return type switch
        {
            ProjectType.Clinic => "clinic",
            ProjectType.School => "school",
            ProjectType.Police => "police",
            ProjectType.Housing => "house.medium",
            ProjectType.Park => "park.small",
            _ => "house.small"
        };
    }

    private static int? EventDistrictId(GameEvent gameEvent)
    {
        return gameEvent.Choices
            .Select(choice => choice.DistrictId)
            .FirstOrDefault(districtId => districtId.HasValue);
    }

    private static string EventObjectKey(EventType type)
    {
        return type switch
        {
            EventType.Crisis => "marker.crisis",
            EventType.Decision => "marker.decision",
            _ => "marker.event"
        };
    }

    private static bool ContainsTerm(string value1, string value2, string value3, params string[] terms)
    {
        return terms.Any(term =>
            value1.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            value2.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            value3.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
