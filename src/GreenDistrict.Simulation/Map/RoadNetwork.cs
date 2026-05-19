using System;

namespace GreenDistrict.Simulation.Map;

public enum RoadKind
{
    LocalRoad,
    RegionalRoad,
    AccessRoad,
    Bridge
}

[Flags]
public enum RoadDirection
{
    None = 0,
    North = 1,
    East = 2,
    South = 4,
    West = 8
}

public enum RoadTileKind
{
    None,
    End,
    Straight,
    Turn,
    TJunction,
    Cross,
    Isolated
}

public static class RoadDirectionExtensions
{
    public static int Count(this RoadDirection directions)
    {
        var value = (int)directions;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    public static bool IsStraight(this RoadDirection directions)
    {
        return directions == (RoadDirection.North | RoadDirection.South)
            || directions == (RoadDirection.East | RoadDirection.West);
    }

    public static RoadTileKind ToTileKind(this RoadDirection directions)
    {
        return directions.Count() switch
        {
            0 => RoadTileKind.Isolated,
            1 => RoadTileKind.End,
            2 => directions.IsStraight() ? RoadTileKind.Straight : RoadTileKind.Turn,
            3 => RoadTileKind.TJunction,
            4 => RoadTileKind.Cross,
            _ => RoadTileKind.None
        };
    }
}
