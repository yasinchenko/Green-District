namespace GreenDistrict.Simulation.Map;

public readonly record struct GridPosition(int X, int Y)
{
    public GridPosition Offset(int dx, int dy) => new(X + dx, Y + dy);
}
