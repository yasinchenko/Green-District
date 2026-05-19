using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public sealed class MapDistrictExpansionSpace
{
    private readonly IReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>> _freeCellsByDirection;

    private MapDistrictExpansionSpace(
        int districtId,
        IReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>> freeCellsByDirection)
    {
        DistrictId = districtId;
        _freeCellsByDirection = freeCellsByDirection;
    }

    public int DistrictId { get; }
    public IReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>> FreeCellsByDirection => _freeCellsByDirection;
    public IReadOnlyList<GridPosition> this[MapEdgeDirection direction] => _freeCellsByDirection[direction];
    public bool CanExpand => _freeCellsByDirection.Values.Any(cells => cells.Count > 0);

    public bool CanExpandTo(MapEdgeDirection direction) => _freeCellsByDirection[direction].Count > 0;

    public static MapDistrictExpansionSpace Build(MapGrid grid, MapDistrictBoundary boundary, int depthMeters = 8)
    {
        if (depthMeters <= 0) throw new System.ArgumentOutOfRangeException(nameof(depthMeters));

        var freeCells = System.Enum
            .GetValues<MapEdgeDirection>()
            .ToDictionary(direction => direction, _ => new HashSet<GridPosition>());

        foreach (var direction in System.Enum.GetValues<MapEdgeDirection>())
        {
            foreach (var edgeCell in boundary[direction])
            {
                for (var distance = 1; distance <= depthMeters; distance++)
                {
                    var candidate = Offset(edgeCell, direction, distance);
                    if (!grid.TryGetCell(candidate, out var cell) || cell == null) break;
                    if (cell.DistrictId.HasValue) break;

                    if (IsFreeExpansionCell(cell))
                    {
                        freeCells[direction].Add(candidate);
                    }
                    else if (cell.IsWater || cell.IsBlocked || cell.HasObject)
                    {
                        break;
                    }
                }
            }
        }

        return new MapDistrictExpansionSpace(
            boundary.DistrictId,
            new ReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>>(
                freeCells.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<GridPosition>)pair.Value.OrderBy(position => position.Y).ThenBy(position => position.X).ToList().AsReadOnly())));
    }

    private static bool IsFreeExpansionCell(MapCell cell)
    {
        return !cell.DistrictId.HasValue &&
            !cell.IsWater &&
            !cell.IsBlocked &&
            !cell.HasObject &&
            !cell.HasInfrastructure;
    }

    private static GridPosition Offset(GridPosition position, MapEdgeDirection direction, int distance)
    {
        return direction switch
        {
            MapEdgeDirection.North => position.Offset(0, -distance),
            MapEdgeDirection.East => position.Offset(distance, 0),
            MapEdgeDirection.South => position.Offset(0, distance),
            _ => position.Offset(-distance, 0)
        };
    }
}
