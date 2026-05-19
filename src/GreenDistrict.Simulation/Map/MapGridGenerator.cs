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
        ApplyBaseTerrain(grid);

        var areas = LayoutDistricts(world.Districts.OrderBy(district => district.Id).ToList(), options)
            .ToDictionary(area => area.DistrictId);

        foreach (var area in areas.Values)
        {
            MarkDistrictCells(grid, area);
        }

        PlaceWorldObjects(grid, world, areas, options);
        BuildRegionalRoads(grid, areas.Values.OrderBy(area => area.Center.X).ThenBy(area => area.Center.Y).ToList(), options);

        var boundaries = areas.Keys.ToDictionary(districtId => districtId, districtId => MapDistrictBoundary.Build(grid, districtId));
        var expansionSpaces = boundaries.ToDictionary(
            pair => pair.Key,
            pair => MapDistrictExpansionSpace.Build(grid, pair.Value, options.ExpansionProbeDepthMeters));

        return new MapGridGenerationResult(grid, areas, MapFreeSpaceIndex.Build(grid), boundaries, expansionSpaces);
    }

    private static void ApplyBaseTerrain(MapGrid grid)
    {
        var startY = Math.Max(2, (int)MathF.Round(grid.HeightMeters * 0.15f));
        var endY = Math.Min(grid.HeightMeters - 3, (int)MathF.Round(grid.HeightMeters * 0.90f));
        if (endY <= startY) return;

        for (var y = 0; y < grid.HeightMeters; y++)
        {
            if (y < startY || y > endY) continue;

            var t = (y - startY) / (float)Math.Max(1, endY - startY);
            var taper = MathF.Sin(MathF.PI * t);
            var rowNoise = StableTerrainNoise(y, 0) - 0.5f;
            var center = grid.WidthMeters * 0.82f
                + MathF.Sin(t * MathF.PI * 2.2f + 0.35f) * grid.WidthMeters * 0.035f
                + MathF.Sin(t * MathF.PI * 5.1f + 1.2f) * grid.WidthMeters * 0.014f
                + rowNoise * grid.WidthMeters * 0.018f;
            var baseHalfWidth = Math.Max(10f, grid.WidthMeters * 0.058f);
            var halfWidth = baseHalfWidth * (0.34f + taper * 0.86f)
                + MathF.Sin(t * MathF.PI * 7.0f + 0.8f) * grid.WidthMeters * 0.012f
                + rowNoise * grid.WidthMeters * 0.011f;
            var leftBank = (int)MathF.Floor(center - halfWidth);
            var rightBank = (int)MathF.Ceiling(center + halfWidth);

            for (var x = 0; x < grid.WidthMeters; x++)
            {
                var bankNoise = StableTerrainNoise(x, y) - 0.5f;
                var localLeft = leftBank + (int)MathF.Round(bankNoise * 3f);
                var localRight = rightBank + (int)MathF.Round((StableTerrainNoise(x + 31, y + 17) - 0.5f) * 3f);
                if (x >= localLeft && x <= localRight)
                {
                    grid.SetSurface(new GridPosition(x, y), MapSurfaceType.Water);
                }
            }
        }
    }

    private static float StableTerrainNoise(int x, int y)
    {
        return StableHash(17_231, x, y) / (float)int.MaxValue;
    }

    private static IReadOnlyList<MapDistrictGridArea> LayoutDistricts(
        IReadOnlyList<District> districts,
        MapGridGenerationOptions options)
    {
        if (districts.Count == 0) return Array.Empty<MapDistrictGridArea>();

        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(districts.Count)));
        var rows = Math.Max(1, (int)Math.Ceiling(districts.Count / (float)columns));
        var availableWidth = options.WidthMeters - options.PaddingMeters * 2 - options.DistrictGapMeters * (columns - 1);
        var availableHeight = options.HeightMeters - options.PaddingMeters * 2 - options.DistrictGapMeters * (rows - 1);
        var cellWidth = Math.Max(24, availableWidth / columns);
        var cellHeight = Math.Max(24, availableHeight / rows);

        var result = new List<MapDistrictGridArea>();
        for (var i = 0; i < districts.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var origin = new GridPosition(
                options.PaddingMeters + column * (cellWidth + options.DistrictGapMeters),
                options.PaddingMeters + row * (cellHeight + options.DistrictGapMeters));

            result.Add(new MapDistrictGridArea(districts[i].Id, districts[i].Name, origin, cellWidth, cellHeight));
        }

        return result;
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
            }
        }
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
            TryPlaceAccessibleObject(grid, mapObject, area, options, business.Id * 41);
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
            TryPlaceAccessibleObject(grid, mapObject, area, options, housing.Id * 53);
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
            TryPlaceAccessibleObject(grid, mapObject, area, options, project.Id * 67);
        }

        foreach (var gameEvent in world.Events
            .Where(gameEvent => !gameEvent.IsResolved)
            .OrderBy(gameEvent => gameEvent.Id))
        {
            var districtId = EventDistrictId(gameEvent);
            if (!districtId.HasValue || !areas.TryGetValue(districtId.Value, out var area)) continue;

            var mapObject = CreateObject(
                $"event:{gameEvent.Id}",
                EventObjectKey(gameEvent.Type),
                area.DistrictId,
                area.Origin,
                MapObjectEntityKind.GameEvent,
                gameEvent.Id);
            TryPlaceMarkerObject(grid, mapObject, area, gameEvent.Id * 79);
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
