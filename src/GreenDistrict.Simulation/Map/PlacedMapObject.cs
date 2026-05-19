using System;
using System.Collections.Generic;

namespace GreenDistrict.Simulation.Map;

[Flags]
public enum MapAccessSide
{
    None = 0,
    North = 1,
    East = 2,
    South = 4,
    West = 8,
    Any = North | East | South | West
}

public enum PlacedMapObjectType
{
    Building,
    Business,
    Housing,
    GovernmentProject,
    Service,
    Park,
    Marker
}

public enum MapObjectEntityKind
{
    None,
    Business,
    GovernmentProject,
    HousingUnit,
    GameEvent
}

public sealed class PlacedMapObject
{
    public PlacedMapObject(
        string id,
        PlacedMapObjectType type,
        int? districtId,
        GridPosition position,
        int widthMeters,
        int lengthMeters,
        int rotationDegrees = 0,
        MapAccessSide accessSides = MapAccessSide.Any,
        string? assetKey = null,
        MapObjectEntityKind entityKind = MapObjectEntityKind.None,
        int? entityId = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Object id is required.", nameof(id));
        if (widthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(widthMeters));
        if (lengthMeters <= 0) throw new ArgumentOutOfRangeException(nameof(lengthMeters));

        Id = id;
        Type = type;
        DistrictId = districtId;
        Position = position;
        WidthMeters = widthMeters;
        LengthMeters = lengthMeters;
        RotationDegrees = NormalizeRotation(rotationDegrees);
        AccessSides = accessSides;
        AssetKey = assetKey ?? DefaultAssetKey(type);
        EntityKind = entityKind;
        EntityId = entityId;
    }

    public string Id { get; }
    public PlacedMapObjectType Type { get; }
    public int? DistrictId { get; }
    public GridPosition Position { get; private set; }
    public int WidthMeters { get; }
    public int LengthMeters { get; }
    public int RotationDegrees { get; }
    public MapAccessSide AccessSides { get; }
    public string AssetKey { get; }
    public MapObjectEntityKind EntityKind { get; }
    public int? EntityId { get; }

    public int FootprintWidth => RotationDegrees is 90 or 270 ? LengthMeters : WidthMeters;
    public int FootprintLength => RotationDegrees is 90 or 270 ? WidthMeters : LengthMeters;

    public IEnumerable<GridPosition> FootprintCells()
    {
        for (var y = 0; y < FootprintLength; y++)
        {
            for (var x = 0; x < FootprintWidth; x++)
            {
                yield return Position.Offset(x, y);
            }
        }
    }

    public PlacedMapObject MoveTo(GridPosition position)
    {
        return PlaceAt(position);
    }

    public PlacedMapObject PlaceAt(
        GridPosition position,
        int? rotationDegrees = null,
        MapAccessSide? accessSides = null)
    {
        return new PlacedMapObject(
            Id,
            Type,
            DistrictId,
            position,
            WidthMeters,
            LengthMeters,
            rotationDegrees ?? RotationDegrees,
            accessSides ?? AccessSides,
            AssetKey,
            EntityKind,
            EntityId);
    }

    private static int NormalizeRotation(int rotationDegrees)
    {
        var normalized = ((rotationDegrees % 360) + 360) % 360;
        return normalized is 0 or 90 or 180 or 270
            ? normalized
            : throw new ArgumentOutOfRangeException(nameof(rotationDegrees), "Rotation must be 0, 90, 180, or 270 degrees.");
    }

    private static string DefaultAssetKey(PlacedMapObjectType type)
    {
        return type.ToString().ToLowerInvariant();
    }
}
