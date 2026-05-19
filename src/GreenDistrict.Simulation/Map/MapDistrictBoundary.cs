using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GreenDistrict.Simulation.Map;

public enum MapEdgeDirection
{
    North,
    East,
    South,
    West
}

public sealed class MapDistrictBoundary
{
    private readonly IReadOnlySet<GridPosition> _cells;
    private readonly IReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>> _edgeCells;

    private MapDistrictBoundary(
        int districtId,
        IReadOnlySet<GridPosition> cells,
        IReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>> edgeCells)
    {
        DistrictId = districtId;
        _cells = cells;
        _edgeCells = edgeCells;
    }

    public int DistrictId { get; }
    public IReadOnlySet<GridPosition> Cells => _cells;
    public IReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>> EdgeCells => _edgeCells;
    public IReadOnlyList<GridPosition> this[MapEdgeDirection direction] => _edgeCells[direction];

    public bool Contains(GridPosition position) => _cells.Contains(position);

    public static MapDistrictBoundary Build(MapGrid grid, int districtId)
    {
        var cells = grid.Cells
            .Where(cell => cell.DistrictId == districtId)
            .Select(cell => cell.Position)
            .ToHashSet();
        var edgeCells = System.Enum
            .GetValues<MapEdgeDirection>()
            .ToDictionary(direction => direction, _ => new List<GridPosition>());

        foreach (var position in cells)
        {
            AddEdgeIfOutside(grid, districtId, position, MapEdgeDirection.North, position.Offset(0, -1), edgeCells);
            AddEdgeIfOutside(grid, districtId, position, MapEdgeDirection.East, position.Offset(1, 0), edgeCells);
            AddEdgeIfOutside(grid, districtId, position, MapEdgeDirection.South, position.Offset(0, 1), edgeCells);
            AddEdgeIfOutside(grid, districtId, position, MapEdgeDirection.West, position.Offset(-1, 0), edgeCells);
        }

        return new MapDistrictBoundary(
            districtId,
            cells,
            new ReadOnlyDictionary<MapEdgeDirection, IReadOnlyList<GridPosition>>(
                edgeCells.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<GridPosition>)pair.Value.Distinct().ToList().AsReadOnly())));
    }

    private static void AddEdgeIfOutside(
        MapGrid grid,
        int districtId,
        GridPosition position,
        MapEdgeDirection direction,
        GridPosition neighbor,
        IDictionary<MapEdgeDirection, List<GridPosition>> edgeCells)
    {
        if (!grid.TryGetCell(neighbor, out var neighborCell) || neighborCell?.DistrictId != districtId)
        {
            edgeCells[direction].Add(position);
        }
    }
}
