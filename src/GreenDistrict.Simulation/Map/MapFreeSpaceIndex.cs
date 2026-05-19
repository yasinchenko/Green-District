using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public enum MapSpaceCategory
{
    DistrictFreeLand,
    FutureDistrictReserve,
    LogisticsReserve,
    Water,
    Blocked,
    Occupied
}

public sealed class MapFreeSpaceIndex
{
    private readonly IReadOnlyDictionary<MapSpaceCategory, IReadOnlyList<GridPosition>> _cellsByCategory;

    private MapFreeSpaceIndex(IReadOnlyDictionary<MapSpaceCategory, IReadOnlyList<GridPosition>> cellsByCategory)
    {
        _cellsByCategory = cellsByCategory;
    }

    public IReadOnlyDictionary<MapSpaceCategory, IReadOnlyList<GridPosition>> CellsByCategory => _cellsByCategory;

    public IReadOnlyList<GridPosition> this[MapSpaceCategory category] =>
        _cellsByCategory.TryGetValue(category, out var cells) ? cells : System.Array.Empty<GridPosition>();

    public int Count(MapSpaceCategory category) => this[category].Count;

    public static MapFreeSpaceIndex Build(MapGrid grid)
    {
        var buckets = System.Enum
            .GetValues<MapSpaceCategory>()
            .ToDictionary(category => category, _ => new List<GridPosition>());

        foreach (var cell in grid.Cells)
        {
            buckets[Classify(cell)].Add(cell.Position);
        }

        return new MapFreeSpaceIndex(new ReadOnlyDictionary<MapSpaceCategory, IReadOnlyList<GridPosition>>(
            buckets.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<GridPosition>)pair.Value.AsReadOnly())));
    }

    private static MapSpaceCategory Classify(MapCell cell)
    {
        if (cell.IsWater) return MapSpaceCategory.Water;
        if (cell.IsBlocked) return MapSpaceCategory.Blocked;
        if (cell.HasRoad && !cell.DistrictId.HasValue) return MapSpaceCategory.LogisticsReserve;
        if (cell.HasObject || cell.HasInfrastructure) return MapSpaceCategory.Occupied;
        return cell.DistrictId.HasValue
            ? MapSpaceCategory.DistrictFreeLand
            : MapSpaceCategory.FutureDistrictReserve;
    }
}
