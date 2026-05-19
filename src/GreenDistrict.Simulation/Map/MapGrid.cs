using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public sealed class MapGrid
{
    private const int DefaultObjectClearanceMeters = 1;

    private readonly MapCell[] _cells;
    private readonly Dictionary<string, PlacedMapObject> _objects = new();

    public MapGrid(int widthMeters, int heightMeters, float cellSizeMeters = 1f)
    {
        if (widthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(widthMeters));
        if (heightMeters <= 0) throw new ArgumentOutOfRangeException(nameof(heightMeters));
        if (cellSizeMeters <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));

        WidthMeters = widthMeters;
        HeightMeters = heightMeters;
        CellSizeMeters = cellSizeMeters;
        _cells = new MapCell[widthMeters * heightMeters];

        for (var y = 0; y < heightMeters; y++)
        {
            for (var x = 0; x < widthMeters; x++)
            {
                _cells[IndexOf(x, y)] = new MapCell(new GridPosition(x, y));
            }
        }
    }

    public int WidthMeters { get; }
    public int HeightMeters { get; }
    public float CellSizeMeters { get; }
    public IReadOnlyDictionary<string, PlacedMapObject> Objects => _objects;

    public bool InBounds(GridPosition position)
    {
        return position.X >= 0 && position.Y >= 0 && position.X < WidthMeters && position.Y < HeightMeters;
    }

    public MapCell GetCell(GridPosition position)
    {
        if (!InBounds(position)) throw new ArgumentOutOfRangeException(nameof(position));
        return _cells[IndexOf(position.X, position.Y)];
    }

    public bool TryGetCell(GridPosition position, out MapCell? cell)
    {
        if (!InBounds(position))
        {
            cell = null;
            return false;
        }

        cell = GetCell(position);
        return true;
    }

    public IEnumerable<MapCell> Cells => _cells;

    public GridPosition WorldToGrid(MapPoint point)
    {
        return new GridPosition(
            (int)MathF.Floor(point.X / CellSizeMeters),
            (int)MathF.Floor(point.Y / CellSizeMeters));
    }

    public MapPoint GridToWorldOrigin(GridPosition position)
    {
        return new MapPoint(position.X * CellSizeMeters, position.Y * CellSizeMeters);
    }

    public MapPoint GridToWorldCenter(GridPosition position)
    {
        return new MapPoint(
            (position.X + 0.5f) * CellSizeMeters,
            (position.Y + 0.5f) * CellSizeMeters);
    }

    public void SetSurface(GridPosition position, MapSurfaceType surface)
    {
        GetCell(position).Surface = surface;
    }

    public string GetSurfaceAssetKey(GridPosition position)
    {
        var cell = GetCell(position);
        if (cell.Surface != MapSurfaceType.Land) return cell.SurfaceAssetKey;

        return HasAdjacentWater(position) ? "terrain.shoreline" : cell.SurfaceAssetKey;
    }

    public void SetInfrastructure(GridPosition position, MapInfrastructureType infrastructure)
    {
        var cell = GetCell(position);
        if (infrastructure == MapInfrastructureType.Bridge && !cell.CanBuildBridge)
        {
            throw new InvalidOperationException("Bridge cells can only be placed over water without occupying objects.");
        }

        if (infrastructure is MapInfrastructureType.Road or MapInfrastructureType.Intersection && !cell.CanBuildRoad)
        {
            throw new InvalidOperationException("Road cells can only be placed on unblocked cells without occupying objects.");
        }

        cell.Infrastructure = infrastructure;
        cell.RoadKind = infrastructure switch
        {
            MapInfrastructureType.Road => RoadKind.LocalRoad,
            MapInfrastructureType.Bridge => RoadKind.Bridge,
            MapInfrastructureType.Intersection => RoadKind.LocalRoad,
            _ => null
        };
        cell.RoadWidthMeters = cell.RoadKind.HasValue ? 6 : 0;
        cell.RoadConnections = RoadDirection.None;
        cell.RoadDistrictId = null;
        RefreshRoadConnectionsAround(position);
    }

    public void SetRoad(
        GridPosition position,
        RoadKind kind,
        int widthMeters,
        int? districtId = null)
    {
        if (widthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(widthMeters));

        var cell = GetCell(position);
        if (cell.HasObject)
        {
            throw new InvalidOperationException("Road cells cannot be placed over occupying objects.");
        }

        if (cell.HasRoad)
        {
            cell.RoadKind = cell.IsWater ? RoadKind.Bridge : cell.RoadKind;
            cell.RoadWidthMeters = Math.Max(cell.RoadWidthMeters, widthMeters);
            cell.RoadDistrictId ??= districtId;
            cell.Infrastructure = cell.RoadKind == RoadKind.Bridge ? MapInfrastructureType.Bridge : MapInfrastructureType.Road;
            RefreshRoadConnectionsAround(position);
            return;
        }

        if (kind == RoadKind.Bridge)
        {
            if (!cell.CanBuildBridge)
            {
                throw new InvalidOperationException("Bridge cells can only be placed over water without occupying objects.");
            }
        }
        else if (!cell.CanBuildRoad)
        {
            throw new InvalidOperationException("Road cells can only be placed on land without occupying objects.");
        }

        cell.RoadKind = kind;
        cell.RoadWidthMeters = widthMeters;
        cell.RoadDistrictId = districtId;
        cell.Infrastructure = kind == RoadKind.Bridge ? MapInfrastructureType.Bridge : MapInfrastructureType.Road;
        RefreshRoadConnectionsAround(position);
    }

    public bool TryBuildRoadPath(
        IEnumerable<GridPosition> path,
        RoadKind kind,
        int widthMeters,
        int? districtId = null)
    {
        var cells = path.ToList();
        if (cells.Count == 0) return false;
        if (!TryGetCell(cells[0], out var start) || start is { IsWater: true }) return false;
        if (!TryGetCell(cells[^1], out var end) || end is { IsWater: true }) return false;
        if (cells.Any(position => !TryGetCell(position, out var cell) || cell == null || cell.IsBlocked || cell.HasObject))
        {
            return false;
        }

        try
        {
            foreach (var position in cells)
            {
                var cell = GetCell(position);
                SetRoad(position, cell.IsWater ? RoadKind.Bridge : kind, widthMeters, districtId);
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }

    public void RefreshRoadConnections()
    {
        foreach (var cell in _cells.Where(cell => cell.HasRoad))
        {
            RefreshRoadConnections(cell.Position);
        }
    }

    public bool CanPlaceObject(PlacedMapObject mapObject)
    {
        var hasDistrictCells = mapObject.DistrictId.HasValue && _cells.Any(cell => cell.DistrictId.HasValue);
        return mapObject.FootprintCells().All(position =>
            TryGetCell(position, out var cell) &&
            cell is { CanBuild: true } &&
            (!hasDistrictCells || cell.DistrictId == mapObject.DistrictId) &&
            HasObjectClearance(mapObject, position, DefaultObjectClearanceMeters));
    }

    public bool TryPlaceObject(PlacedMapObject mapObject)
    {
        if (_objects.ContainsKey(mapObject.Id)) return false;
        if (!CanPlaceObject(mapObject)) return false;

        foreach (var position in mapObject.FootprintCells())
        {
            var cell = GetCell(position);
            cell.ObjectLayer = LayerFor(mapObject.Type);
            cell.ObjectId = mapObject.Id;
        }

        _objects.Add(mapObject.Id, mapObject);
        return true;
    }

    public bool RemoveObject(string objectId)
    {
        if (!_objects.Remove(objectId)) return false;

        foreach (var cell in _cells.Where(cell => cell.ObjectId == objectId))
        {
            cell.ObjectLayer = MapObjectLayerType.None;
            cell.ObjectId = null;
        }

        return true;
    }

    public bool HasRoadAccess(PlacedMapObject mapObject)
    {
        return GetAccessCells(mapObject).Any(position =>
            TryGetCell(position, out var cell) &&
            cell is { HasRoadAccess: true });
    }

    public IEnumerable<GridPosition> GetAccessCells(PlacedMapObject mapObject)
    {
        var x0 = mapObject.Position.X;
        var y0 = mapObject.Position.Y;
        var x1 = x0 + mapObject.FootprintWidth - 1;
        var y1 = y0 + mapObject.FootprintLength - 1;

        if (mapObject.AccessSides.HasFlag(MapAccessSide.North))
        {
            for (var x = x0; x <= x1; x++) yield return new GridPosition(x, y0 - 1);
        }

        if (mapObject.AccessSides.HasFlag(MapAccessSide.South))
        {
            for (var x = x0; x <= x1; x++) yield return new GridPosition(x, y1 + 1);
        }

        if (mapObject.AccessSides.HasFlag(MapAccessSide.West))
        {
            for (var y = y0; y <= y1; y++) yield return new GridPosition(x0 - 1, y);
        }

        if (mapObject.AccessSides.HasFlag(MapAccessSide.East))
        {
            for (var y = y0; y <= y1; y++) yield return new GridPosition(x1 + 1, y);
        }
    }

    private int IndexOf(int x, int y) => y * WidthMeters + x;

    private bool HasAdjacentWater(GridPosition position)
    {
        return TryGetCell(position.Offset(0, -1), out var north) && north is { IsWater: true } ||
            TryGetCell(position.Offset(1, 0), out var east) && east is { IsWater: true } ||
            TryGetCell(position.Offset(0, 1), out var south) && south is { IsWater: true } ||
            TryGetCell(position.Offset(-1, 0), out var west) && west is { IsWater: true };
    }

    private bool HasObjectClearance(PlacedMapObject mapObject, GridPosition position, int clearanceMeters)
    {
        if (clearanceMeters <= 0) return true;

        for (var y = position.Y - clearanceMeters; y <= position.Y + clearanceMeters; y++)
        {
            for (var x = position.X - clearanceMeters; x <= position.X + clearanceMeters; x++)
            {
                var neighbor = new GridPosition(x, y);
                if (!TryGetCell(neighbor, out var cell) || cell == null) continue;
                if (!cell.HasObject || cell.ObjectId == mapObject.Id) continue;

                return false;
            }
        }

        return true;
    }

    private void RefreshRoadConnectionsAround(GridPosition position)
    {
        RefreshRoadConnections(position);
        RefreshRoadConnections(position.Offset(0, -1));
        RefreshRoadConnections(position.Offset(1, 0));
        RefreshRoadConnections(position.Offset(0, 1));
        RefreshRoadConnections(position.Offset(-1, 0));
    }

    private void RefreshRoadConnections(GridPosition position)
    {
        if (!TryGetCell(position, out var cell) || cell is not { HasRoad: true }) return;

        var connections = RoadDirection.None;
        if (IsConnectableRoad(position, position.Offset(0, -1))) connections |= RoadDirection.North;
        if (IsConnectableRoad(position, position.Offset(1, 0))) connections |= RoadDirection.East;
        if (IsConnectableRoad(position, position.Offset(0, 1))) connections |= RoadDirection.South;
        if (IsConnectableRoad(position, position.Offset(-1, 0))) connections |= RoadDirection.West;

        cell.RoadConnections = connections;
        if (cell.RoadKind != RoadKind.Bridge && connections.Count() >= 3)
        {
            cell.Infrastructure = MapInfrastructureType.Intersection;
        }
        else
        {
            cell.Infrastructure = cell.RoadKind == RoadKind.Bridge ? MapInfrastructureType.Bridge : MapInfrastructureType.Road;
        }
    }

    private bool IsConnectableRoad(GridPosition from, GridPosition to)
    {
        if (!TryGetCell(from, out var fromCell) || fromCell is not { HasRoad: true }) return false;
        if (!TryGetCell(to, out var toCell) || toCell is not { HasRoad: true }) return false;

        if (fromCell.RoadKind == RoadKind.Bridge && toCell.RoadKind == RoadKind.Bridge) return true;
        if (fromCell.RoadKind == RoadKind.Bridge) return !fromCell.IsWater || !toCell.IsWater;
        if (toCell.RoadKind == RoadKind.Bridge) return !fromCell.IsWater || !toCell.IsWater;
        return true;
    }

    private static MapObjectLayerType LayerFor(PlacedMapObjectType type)
    {
        return type switch
        {
            PlacedMapObjectType.GovernmentProject => MapObjectLayerType.ConstructionProject,
            PlacedMapObjectType.Marker => MapObjectLayerType.EventMarker,
            _ => MapObjectLayerType.Building
        };
    }
}
