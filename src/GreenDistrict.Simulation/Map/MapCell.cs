namespace GreenDistrict.Simulation.Map;

public enum MapSurfaceType
{
    Land,
    Water,
    Park,
    Blocked
}

public enum MapInfrastructureType
{
    None,
    Road,
    Bridge,
    Intersection
}

public enum MapObjectLayerType
{
    None,
    Building,
    ConstructionProject,
    EventMarker
}

public sealed class MapCell
{
    public MapCell(GridPosition position)
    {
        Position = position;
    }

    public GridPosition Position { get; }
    public int? DistrictId { get; set; }
    public MapSurfaceType Surface { get; set; } = MapSurfaceType.Land;
    public MapInfrastructureType Infrastructure { get; set; } = MapInfrastructureType.None;
    public RoadKind? RoadKind { get; set; }
    public int RoadWidthMeters { get; set; }
    public RoadDirection RoadConnections { get; set; } = RoadDirection.None;
    public int? RoadDistrictId { get; set; }
    public MapObjectLayerType ObjectLayer { get; set; } = MapObjectLayerType.None;
    public string? ObjectId { get; set; }

    public bool HasObject => ObjectLayer != MapObjectLayerType.None || !string.IsNullOrWhiteSpace(ObjectId);
    public bool HasInfrastructure => Infrastructure != MapInfrastructureType.None;
    public bool HasRoad => RoadKind.HasValue;
    public bool IsWater => Surface == MapSurfaceType.Water;
    public bool IsBlocked => Surface == MapSurfaceType.Blocked;
    public bool CanBuild => !IsWater && !IsBlocked && !HasInfrastructure && !HasObject;
    public bool CanBuildRoad => !IsWater && !IsBlocked && !HasInfrastructure && !HasObject;
    public bool CanBuildBridge => IsWater && !HasInfrastructure && !HasObject;
    public bool HasRoadAccess => HasRoad;
    public string SurfaceAssetKey => Surface switch
    {
        MapSurfaceType.Water => "terrain.water",
        MapSurfaceType.Park => "terrain.park",
        MapSurfaceType.Blocked => "terrain.blocked",
        _ => "terrain.grass"
    };

    public RoadTileKind RoadTileKind => HasRoad ? RoadConnections.ToTileKind() : GreenDistrict.Simulation.Map.RoadTileKind.None;
    public string? RoadAssetKey => RoadKind switch
    {
        null => null,
        GreenDistrict.Simulation.Map.RoadKind.Bridge => $"bridge.{RoadTileKind.ToString().ToLowerInvariant()}",
        _ => $"road.{RoadTileKind.ToString().ToLowerInvariant()}"
    };
}
